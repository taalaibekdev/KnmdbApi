using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Serialization.Metadata;
using Knmdb.TrackAndTrace.Models.Requests;
using Knmdb.TrackAndTrace.Models.Responses;
using Knmdb.TrackAndTrace.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Knmdb.TrackAndTrace;

/// <summary>
/// Клиент API KNMDB (Track and Trace) — единая точка входа в SDK.
/// </summary>
/// <remarks>
/// <para>
/// Один экземпляр обслуживает любое количество пользователей: состояние входа хранится
/// в отдельных сессиях (<see cref="KnddbSession"/>), а не в самом клиенте.
/// </para>
/// <para>Работает в ASP.NET Core, .NET MAUI, WPF, WinForms, Avalonia, консольных и фоновых
/// приложениях на .NET 10.</para>
/// <para><b>Быстрый старт без внедрения зависимостей:</b></para>
/// <code>
/// await using var client = KnddbApiClient.Create(KnddbEnvironment.Test);
///
/// await client.SignInAsync("login", "password");
///
/// var stakeholders = await client.GetAllStakeholdersAsync();
/// </code>
/// <para><b>Быстрый старт в ASP.NET Core:</b></para>
/// <code>
/// builder.Services.AddKnddbTrackAndTrace(o => o.Environment = KnddbEnvironment.Production);
///
/// public async Task&lt;IActionResult&gt; Get(KnddbApiClient client)
///     =&gt; Ok(await client.GetAllStakeholdersAsync());
/// </code>
/// <para><b>Смена пользователя «на ходу»:</b></para>
/// <code>
/// await client.SignInAsync("pharmacist", "secret", sessionKey: "user:42");
/// await client.SignInAsync("warehouse", "secret", sessionKey: "user:77");
///
/// await client.UseSessionAsync("user:42");        // дальше запросы идут от аптекаря
/// var stock = await client.GetStockInheldListAsync();
///
/// await using (client.BeginUserScope("user:77"))  // временно — от кладовщика
/// {
///     await client.GetStockInheldListAsync();
/// }
/// </code>
/// </remarks>
public sealed class KnddbApiClient : IDisposable
{
    private const string TrackAndTracePath = "api/TrackAndTrace/";

    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly KnddbClientOptions _options;
    private readonly KnddbCurrentUser _currentUser;
    private readonly IKnddbSessionStore _sessionStore;
    private readonly IKnddbCredentialStore _credentialStore;
    private readonly KnddbTokenStore _tokenStore;
    private readonly KnddbTransport _transport;
    private readonly ILogger<KnddbApiClient> _logger;

    /// <summary>
    /// Создаёт клиент.
    /// </summary>
    /// <param name="httpClient">
    /// HTTP-клиент. Владение остаётся за вызывающей стороной. Удобно создавать через
    /// <see cref="KnddbClientOptions.CreateHttpClient"/>.
    /// </param>
    /// <param name="options">
    /// Настройки. Если не заданы, используется тестовый контур и адрес из <paramref name="httpClient"/>.
    /// </param>
    /// <param name="loggerFactory">Фабрика журналирования (необязательно).</param>
    /// <exception cref="ArgumentNullException">Не задан <paramref name="httpClient"/>.</exception>
    public KnddbApiClient(
        HttpClient httpClient,
        KnddbClientOptions? options = null,
        ILoggerFactory? loggerFactory = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        _httpClient = httpClient;
        _options = options ?? new KnddbClientOptions { BaseAddress = httpClient.BaseAddress };

        if (_options.BaseAddress is null)
        {
            _options.BaseAddress = httpClient.BaseAddress;
        }

        _options.Validate();

        var loggers = loggerFactory ?? NullLoggerFactory.Instance;

        _currentUser = new KnddbCurrentUser();
        _sessionStore = new InMemoryKnddbSessionStore();
        _credentialStore = new InMemoryKnddbCredentialStore();

        _tokenStore = new KnddbTokenStore(
            () => _httpClient,
            () => _options,
            _sessionStore,
            _credentialStore,
            loggers.CreateLogger<KnddbTokenStore>());

        // Резервный источник текущего пользователя: сессия по ключу из настроек.
        // Нужен для сценария «вход выполнен, ключ сессии не переключался».
        _transport = new KnddbTransport(
            () => _httpClient,
            () => _options,
            _tokenStore,
            _currentUser,
            () => _sessionStore.Get(_options.DefaultSessionKey));

        _logger = loggers.CreateLogger<KnddbApiClient>();
    }

    /// <summary>
    /// Создаёт самостоятельный клиент: SDK сам создаёт и освобождает <see cref="HttpClient"/>.
    /// </summary>
    /// <param name="environment">Контур: тестовый или боевой.</param>
    /// <param name="configure">Дополнительная настройка параметров (необязательно).</param>
    /// <param name="handler">Пользовательский HTTP-обработчик (необязательно).</param>
    /// <returns>Клиент; освобождайте его через <c>await using</c> или <c>using</c>.</returns>
    /// <remarks>
    /// <code>
    /// await using var client = KnddbApiClient.Create(KnddbEnvironment.Production);
    /// </code>
    /// </remarks>
    public static KnddbApiClient Create(
        KnddbEnvironment environment = KnddbEnvironment.Test,
        Action<KnddbClientOptions>? configure = null,
        HttpMessageHandler? handler = null)
    {
        var options = new KnddbClientOptions { Environment = environment };
        configure?.Invoke(options);

        return new KnddbApiClient(
            options.CreateHttpClient(handler),
            options,
            loggerFactory: null,
            ownsHttpClient: true);
    }

    /// <summary>
    /// Создаёт клиент, который сам освобождает переданный <see cref="HttpClient"/>.
    /// </summary>
    /// <param name="httpClient">HTTP-клиент.</param>
    /// <param name="options">Настройки.</param>
    /// <param name="loggerFactory">Фабрика журналирования.</param>
    /// <param name="ownsHttpClient">Освобождать ли клиент при <see cref="Dispose"/>.</param>
    internal KnddbApiClient(
        HttpClient httpClient,
        KnddbClientOptions options,
        ILoggerFactory? loggerFactory,
        bool ownsHttpClient)
        : this(httpClient, options, loggerFactory)
    {
        _ownsHttpClient = ownsHttpClient;
    }

    // ---------------------------------------------------------------------
    // Состояние входа
    // ---------------------------------------------------------------------

    /// <summary>
    /// Текущий пользователь (без обращения к сети).
    /// </summary>
    /// <remarks><see langword="null"/>, если вход не выполнен.</remarks>
    public KnddbSession? CurrentSession => _transport.GetCurrentSession();

    /// <summary>Логин текущего пользователя или <see langword="null"/>.</summary>
    public string? CurrentUserName => CurrentSession?.UserName;

    /// <summary>Ключ текущей сессии.</summary>
    public string CurrentSessionKey => _transport.ResolveSessionKey();

    /// <summary>
    /// Признак того, что можно выполнить запрос: есть действующий токен либо сохранённые учётные данные.
    /// </summary>
    public bool IsAuthenticated
    {
        get
        {
            var session = CurrentSession;
            return session is not null
                   && (session.IsAccessTokenValid(_options.TokenExpirationMargin)
                       || session.RefreshToken is not null
                       || session.Credentials is not null);
        }
    }

    /// <summary>
    /// Контур, к которому подключён клиент.
    /// </summary>
    public KnddbEnvironment? Environment => _options.ResolvedEnvironment;

    /// <summary>Адрес сервера, к которому обращается клиент.</summary>
    public Uri BaseAddress => _options.EffectiveBaseAddress;

    /// <summary>
    /// Выполняет вход и делает сессию текущей.
    /// </summary>
    /// <param name="userName">Логин KNMDB.</param>
    /// <param name="password">Пароль KNMDB.</param>
    /// <param name="sessionKey">
    /// Ключ сессии (идентификатор пользователя внутри SDK). Если не задан — используется ключ текущего пользователя.
    /// </param>
    /// <param name="storeCredentials">
    /// Сохранять ли логин и пароль в памяти для автоматического продления сессии.
    /// Укажите <see langword="false"/>, если приложение не должно держать пароль в памяти.
    /// </param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Сессия с действующими токенами.</returns>
    /// <exception cref="KnddbAuthenticationException">Логин или пароль отклонён сервером.</exception>
    public async Task<KnddbSession> SignInAsync(
        string userName,
        string password,
        string? sessionKey = null,
        bool storeCredentials = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);
        ArgumentNullException.ThrowIfNull(password);

        var key = sessionKey ?? _transport.ResolveSessionKey();
        var session = await _tokenStore
            .SignInAsync(key, Models.Auth.KnddbCredentials.Create(userName, password), storeCredentials, cancellationToken)
            .ConfigureAwait(false);

        _currentUser.Current = session;
        return session;
    }

    /// <summary>
    /// Переключает SDK на уже существующую сессию — смена пользователя без обращения к сети.
    /// </summary>
    /// <param name="sessionKey">Ключ сессии.</param>
    /// <returns>Сессия, ставшая текущей.</returns>
    /// <exception cref="KnddbConfigurationException">Сессия не найдена: сначала выполните вход.</exception>
    public KnddbSession UseSession(string sessionKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionKey);

        var session = _sessionStore.Get(sessionKey)
                      ?? throw new KnddbConfigurationException(
                          $"Сессия '{sessionKey}' не найдена. Сначала выполните вход: SignInAsync(...) " +
                          "или UseSessionAsync(sessionKey, credentials).");

        _currentUser.Current = session;
        return session;
    }

    /// <summary>
    /// Выполняет вход под указанным ключом и делает пользователя текущим.
    /// </summary>
    /// <param name="sessionKey">Ключ сессии.</param>
    /// <param name="credentials">
    /// Учётные данные. Если не заданы, используются сохранённые для этого ключа или данные по умолчанию.
    /// </param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Сессия с действующими токенами.</returns>
    public async Task<KnddbSession> UseSessionAsync(
        string sessionKey,
        Models.Auth.KnddbCredentials? credentials = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionKey);

        var session = await GetOrSignInAsync(sessionKey, credentials, cancellationToken).ConfigureAwait(false);

        _currentUser.Current = session;
        return session;
    }

    /// <summary>
    /// Начинает область, внутри которой запросы выполняются от имени указанного пользователя.
    /// </summary>
    /// <param name="sessionKey">Ключ существующей сессии.</param>
    /// <returns>Область, восстанавливающая прежнего пользователя при освобождении.</returns>
    public KnddbUserScope BeginUserScope(string sessionKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionKey);

        // Прежний пользователь запоминается внутри конструктора — до переключения.
        return new KnddbUserScope(_currentUser, ResolveSession(sessionKey));
    }

    /// <summary>
    /// Начинает область от имени другого пользователя, выполняя вход при необходимости.
    /// </summary>
    /// <param name="sessionKey">Ключ сессии.</param>
    /// <param name="credentials">Учётные данные (если входа ещё не было).</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Область, восстанавливающая прежнего пользователя при освобождении.</returns>
    public async Task<KnddbUserScope> BeginUserScopeAsync(
        string sessionKey,
        Models.Auth.KnddbCredentials? credentials = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionKey);

        var session = await GetOrSignInAsync(sessionKey, credentials, cancellationToken).ConfigureAwait(false);

        // Прежний пользователь запоминается внутри конструктора — до переключения.
        return new KnddbUserScope(_currentUser, session);
    }

    /// <summary>
    /// Возвращает сессию по ключу или <see langword="null"/>.
    /// </summary>
    /// <param name="sessionKey">Ключ сессии.</param>
    /// <returns>Сессия либо <see langword="null"/>.</returns>
    public KnddbSession? FindSession(string sessionKey) => _sessionStore.Get(sessionKey);

    private KnddbSession ResolveSession(string sessionKey)
        => _sessionStore.Get(sessionKey)
           ?? throw new KnddbConfigurationException(
               $"Сессия '{sessionKey}' не найдена. Сначала выполните вход: SignInAsync(...) " +
               "или UseSessionAsync(sessionKey, credentials).");

    private async Task<KnddbSession> GetOrSignInAsync(
        string sessionKey,
        Models.Auth.KnddbCredentials? credentials,
        CancellationToken cancellationToken)
    {
        var existing = _sessionStore.Get(sessionKey);

        if (credentials is null && existing is not null && existing.IsAccessTokenValid(_options.TokenExpirationMargin))
        {
            return existing;
        }

        return await _tokenStore
            .SignInAsync(sessionKey, credentials, storeCredentials: true, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Завершает текущую сессию: отзывает токены на сервере (best effort) и очищает их в памяти SDK.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача завершения выхода.</returns>
    /// <remarks>Ошибка серверного вызова не прерывает выход: локальные токены удаляются в любом случае.</remarks>
    public Task SignOutAsync(CancellationToken cancellationToken = default)
        => SignOutAsync(_transport.ResolveSessionKey(), cancellationToken);

    /// <summary>
    /// Завершает указанную сессию.
    /// </summary>
    /// <param name="sessionKey">Ключ сессии.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача завершения выхода.</returns>
    public async Task SignOutAsync(string sessionKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionKey);

        var session = _sessionStore.Get(sessionKey);
        if (session is not null && session.IsAuthenticated)
        {
            try
            {
                await _transport.SendWithoutResponseAsync(
                    static () => new HttpRequestMessage(HttpMethod.Post, "connect/logout"),
                    HttpMethod.Post,
                    "connect/logout",
                    requireAuthentication: true,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (KnddbApiException ex)
            {
                _logger.LogDebug(ex, "Сессия '{SessionKey}': вызов /connect/logout завершился ошибкой.", sessionKey);
            }
        }

        if (string.Equals(CurrentSession?.Key, sessionKey, StringComparison.Ordinal))
        {
            _currentUser.Current = null;
        }

        _tokenStore.SignOut(sessionKey);
    }

    // ---------------------------------------------------------------------
    // Методы API
    // ---------------------------------------------------------------------

    /// <summary>
    /// Список всех организаций (стейкхолдеров) системы.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Список организаций с кодами для других методов.</returns>
    /// <remarks>HTTP: <c>GET /api/TrackAndTrace/GetAllStakeholders</c>. Роль: <c>ApiGetAllStakeholders</c>.</remarks>
    public Task<GetAllStakeholdersResponse?> GetAllStakeholdersAsync(CancellationToken cancellationToken = default)
        => _transport.SendAsync(
            static () => new HttpRequestMessage(HttpMethod.Get, TrackAndTracePath + "GetAllStakeholders"),
            HttpMethod.Get,
            "/" + TrackAndTracePath + "GetAllStakeholders",
            KnddbJson.TypeInfo<GetAllStakeholdersResponse>(),
            requireAuthentication: true,
            cancellationToken);

    /// <summary>
    /// Список поддерживаемых типов QR-кодов (форматов Data Matrix).
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Типы QR-кодов с идентификаторами для поля <c>qrTypeId</c>.</returns>
    /// <remarks>
    /// <para>
    /// HTTP: <c>GET /api/TrackAndTrace/GetSupportedQRTypes</c>.
    /// </para>
    /// <para>
    /// <b>Устаревший метод (deprecated).</b> Исключён из API департамента лекарственных средств,
    /// поэтому метод и связанные с ним типы (<c>GetSupportedQRTypesResponse</c>, <c>QRTypeInfo</c>)
    /// будут удалены из SDK, когда окончательно исчезнут из API. Новую интеграцию на него
    /// не закладывайте: значение <c>qrTypeId</c> для деклараций согласуйте с департаментом
    /// лекарственных средств и зафиксируйте как константу в конфигурации приложения.
    /// </para>
    /// </remarks>
    [Obsolete(
        "Метод GetSupportedQRTypes исключён из API департамента лекарственных средств " +
        "и будет удалён из SDK. Значение qrTypeId согласуйте с департаментом и задайте в конфигурации.",
        error: false,
        DiagnosticId = "KNMDB0001")]
    public Task<GetSupportedQRTypesResponse?> GetSupportedQrTypesAsync(CancellationToken cancellationToken = default)
        => _transport.SendAsync(
            static () => new HttpRequestMessage(HttpMethod.Get, TrackAndTracePath + "GetSupportedQRTypes"),
            HttpMethod.Get,
            "/" + TrackAndTracePath + "GetSupportedQRTypes",
            KnddbJson.TypeInfo<GetSupportedQRTypesResponse>(),
            requireAuthentication: true,
            cancellationToken);

    /// <summary>
    /// Справочник лекарственных средств, изменённых после указанной даты.
    /// </summary>
    /// <param name="request">Запрос с необязательной нижней границей даты изменения.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Список препаратов и их количество.</returns>
    /// <remarks>
    /// HTTP: <c>POST /api/TrackAndTrace/GetMedicineList</c>. Роль: <c>ApiGetMedicineList</c>.
    /// Для инкрементальной синхронизации передавайте максимальное значение <c>LastUpdate</c>
    /// из предыдущего ответа.
    /// </remarks>
    public Task<GetMedicineListResponse?> GetMedicineListAsync(
        GetMedicineListRequest? request = null,
        CancellationToken cancellationToken = default)
        => PostAsync(
            "GetMedicineList",
            request ?? new GetMedicineListRequest(),
            KnddbJson.TypeInfo<GetMedicineListRequest>(),
            KnddbJson.TypeInfo<GetMedicineListResponse>(),
            cancellationToken);

    /// <summary>
    /// Проверяет лекарственное средство по QR-коду (Data Matrix).
    /// </summary>
    /// <param name="qrCode">Содержимое QR-кода.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Полные сведения об упаковке.</returns>
    /// <remarks>
    /// HTTP: <c>POST /api/TrackAndTrace/ProductInquiryQRCode</c>. <b>Авторизация не требуется.</b>
    /// Если упаковка не найдена — <see cref="KnddbApiException"/> со статусом 404.
    /// </remarks>
    public Task<ProductInquiryResult?> ProductInquiryByQrCodeAsync(
        string qrCode,
        CancellationToken cancellationToken = default)
        => PostAsync(
            "ProductInquiryQRCode",
            new ProductInquiryWithQrCodeRequest { QrCode = qrCode },
            KnddbJson.TypeInfo<ProductInquiryWithQrCodeRequest>(),
            KnddbJson.TypeInfo<ProductInquiryResult>(),
            cancellationToken,
            requireAuthentication: false);

    /// <summary>
    /// Проверяет лекарственное средство по паре «GTIN + серийный номер».
    /// </summary>
    /// <param name="gtin">GTIN препарата.</param>
    /// <param name="serialNumber">Серийный номер упаковки.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Полные сведения об упаковке.</returns>
    /// <remarks>
    /// HTTP: <c>POST /api/TrackAndTrace/ProductInquiryGtinSn</c>. <b>Авторизация не требуется.</b>
    /// </remarks>
    public Task<ProductInquiryResult?> ProductInquiryByGtinSnAsync(
        string gtin,
        string serialNumber,
        CancellationToken cancellationToken = default)
        => PostAsync(
            "ProductInquiryGtinSn",
            new ProductInquiryWithGtinSnRequest { Gtin = gtin, SerialNumber = serialNumber },
            KnddbJson.TypeInfo<ProductInquiryWithGtinSnRequest>(),
            KnddbJson.TypeInfo<ProductInquiryResult>(),
            cancellationToken,
            requireAuthentication: false);

    /// <summary>
    /// Регистрирует декларацию импорта партии лекарственного средства.
    /// </summary>
    /// <param name="request">Параметры партии и список QR-кодов.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Идентификатор и реквизиты созданной декларации.</returns>
    /// <remarks>HTTP: <c>POST /api/TrackAndTrace/ImportDeclaration</c>. Роль: <c>ApiImportDeclaration</c>.</remarks>
    public Task<ImportDeclarationResponse?> ImportDeclarationAsync(
        ImportDeclarationRequest request,
        CancellationToken cancellationToken = default)
        => PostAsync(
            "ImportDeclaration",
            request ?? throw new ArgumentNullException(nameof(request)),
            KnddbJson.TypeInfo<ImportDeclarationRequest>(),
            KnddbJson.TypeInfo<ImportDeclarationResponse>(),
            cancellationToken);

    /// <summary>
    /// Регистрирует декларацию производства партии лекарственного средства.
    /// </summary>
    /// <param name="request">Параметры партии и список QR-кодов.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Идентификатор и реквизиты созданной декларации.</returns>
    /// <remarks>HTTP: <c>POST /api/TrackAndTrace/ProductionDeclaration</c>. Роль: <c>ApiProductionDeclaration</c>.</remarks>
    public Task<ProductionDeclarationResponse?> ProductionDeclarationAsync(
        ProductionDeclarationRequest request,
        CancellationToken cancellationToken = default)
        => PostAsync(
            "ProductionDeclaration",
            request ?? throw new ArgumentNullException(nameof(request)),
            KnddbJson.TypeInfo<ProductionDeclarationRequest>(),
            KnddbJson.TypeInfo<ProductionDeclarationResponse>(),
            cancellationToken);

    /// <summary>
    /// Создаёт декларацию перемещения упаковок другому стейкхолдеру.
    /// </summary>
    /// <param name="request">Отправитель, получатель и список упаковок.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Идентификатор созданного перемещения.</returns>
    /// <remarks>
    /// HTTP: <c>POST /api/TrackAndTrace/TransferDeclaration</c>. Роль: <c>ApiTransferDeclaration</c>.
    /// Получатель подтверждает приём методом <see cref="TransferAcceptAsync"/>.
    /// </remarks>
    public Task<TransferDeclarationResponse?> TransferDeclarationAsync(
        TransferDeclarationRequest request,
        CancellationToken cancellationToken = default)
        => PostAsync(
            "TransferDeclaration",
            request ?? throw new ArgumentNullException(nameof(request)),
            KnddbJson.TypeInfo<TransferDeclarationRequest>(),
            KnddbJson.TypeInfo<TransferDeclarationResponse>(),
            cancellationToken);

    /// <summary>
    /// Подтверждает приём перемещения получателем.
    /// </summary>
    /// <param name="declarationId">Идентификатор перемещения.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Идентификатор и дата перемещения.</returns>
    /// <remarks>HTTP: <c>POST /api/TrackAndTrace/TransferAccept</c>. Роль: <c>ApiTransferAccept</c>.</remarks>
    public Task<TransferDeclarationResponse?> TransferAcceptAsync(
        long declarationId,
        CancellationToken cancellationToken = default)
        => PostAsync(
            "TransferAccept",
            new TransferAcceptRequest { DeclarationId = declarationId },
            KnddbJson.TypeInfo<TransferAcceptRequest>(),
            KnddbJson.TypeInfo<TransferDeclarationResponse>(),
            cancellationToken);

    /// <summary>
    /// Отменяет ранее созданное перемещение.
    /// </summary>
    /// <param name="declarationId">Идентификатор перемещения.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Идентификатор и дата отмены.</returns>
    /// <remarks>HTTP: <c>POST /api/TrackAndTrace/TransferCancel</c>. Роль: <c>ApiTransferCancel</c>.</remarks>
    public Task<TransferDeclarationCancelResponse?> TransferCancelAsync(
        long declarationId,
        CancellationToken cancellationToken = default)
        => PostAsync(
            "TransferCancel",
            new TransferCancelRequest { DeclarationId = declarationId },
            KnddbJson.TypeInfo<TransferCancelRequest>(),
            KnddbJson.TypeInfo<TransferDeclarationCancelResponse>(),
            cancellationToken);

    /// <summary>
    /// Создаёт возвратное перемещение упаковок предыдущему держателю.
    /// </summary>
    /// <param name="request">Список возвращаемых упаковок и реквизиты документа.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Идентификатор созданного возврата.</returns>
    /// <remarks>HTTP: <c>POST /api/TrackAndTrace/TransferReturn</c>. Роль: <c>ApiTransferReturn</c>.</remarks>
    public Task<TransferDeclarationResponse?> TransferReturnAsync(
        TransferReturnRequest request,
        CancellationToken cancellationToken = default)
        => PostAsync(
            "TransferReturn",
            request ?? throw new ArgumentNullException(nameof(request)),
            KnddbJson.TypeInfo<TransferReturnRequest>(),
            KnddbJson.TypeInfo<TransferDeclarationResponse>(),
            cancellationToken);

    /// <summary>
    /// Отменяет возвратное перемещение.
    /// </summary>
    /// <param name="declarationId">Идентификатор возврата.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Идентификатор и дата отмены.</returns>
    /// <remarks>HTTP: <c>POST /api/TrackAndTrace/TransferReturnCancel</c>. Роль: <c>ApiTransferReturnCancel</c>.</remarks>
    public Task<TransferDeclarationCancelResponse?> TransferReturnCancelAsync(
        long declarationId,
        CancellationToken cancellationToken = default)
        => PostAsync(
            "TransferReturnCancel",
            new TransferCancelRequest { DeclarationId = declarationId },
            KnddbJson.TypeInfo<TransferCancelRequest>(),
            KnddbJson.TypeInfo<TransferDeclarationCancelResponse>(),
            cancellationToken);

    /// <summary>
    /// Полное содержимое одной декларации перемещения.
    /// </summary>
    /// <param name="declarationId">Идентификатор перемещения.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Перемещение с перечнем упаковок.</returns>
    /// <remarks>HTTP: <c>GET /api/TrackAndTrace/GetTransferDeclaration</c>. Роль: <c>ApiGetTransferDeclaration</c>.</remarks>
    public Task<GetTransferDeclarationResponse?> GetTransferDeclarationAsync(
        long declarationId,
        CancellationToken cancellationToken = default)
    {
        var path = TrackAndTracePath + "GetTransferDeclaration?declarationId=" +
                   declarationId.ToString(System.Globalization.CultureInfo.InvariantCulture);

        return _transport.SendAsync(
            () => new HttpRequestMessage(HttpMethod.Get, path),
            HttpMethod.Get,
            "/" + path,
            KnddbJson.TypeInfo<GetTransferDeclarationResponse>(),
            requireAuthentication: true,
            cancellationToken);
    }

    /// <summary>
    /// Ищет декларации перемещения по необязательным фильтрам.
    /// </summary>
    /// <param name="request">Фильтры поиска.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Список найденных перемещений (без перечня упаковок).</returns>
    /// <remarks>HTTP: <c>POST /api/TrackAndTrace/GetTransferListByFilter</c>. Роль: <c>ApiGetTransferListByFilter</c>.</remarks>
    public Task<GetTransferListByFilterResponse?> GetTransferListByFilterAsync(
        GetTransferListByFilterRequest? request = null,
        CancellationToken cancellationToken = default)
        => PostAsync(
            "GetTransferListByFilter",
            request ?? new GetTransferListByFilterRequest(),
            KnddbJson.TypeInfo<GetTransferListByFilterRequest>(),
            KnddbJson.TypeInfo<GetTransferListByFilterResponse>(),
            cancellationToken);

    /// <summary>
    /// Ставит упаковки на складской учёт.
    /// </summary>
    /// <param name="request">Склад, дата и список упаковок.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Идентификатор созданной складской декларации.</returns>
    /// <remarks>HTTP: <c>POST /api/TrackAndTrace/StockDeclaration</c>. Роль: <c>ApiStockDeclaration</c>.</remarks>
    public Task<StockDeclarationResponse?> StockDeclarationAsync(
        StockDeclarationRequest request,
        CancellationToken cancellationToken = default)
        => PostAsync(
            "StockDeclaration",
            request ?? throw new ArgumentNullException(nameof(request)),
            KnddbJson.TypeInfo<StockDeclarationRequest>(),
            KnddbJson.TypeInfo<StockDeclarationResponse>(),
            cancellationToken);

    /// <summary>
    /// Отменяет складскую декларацию.
    /// </summary>
    /// <param name="declarationId">Идентификатор декларации.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат отмены (обратите внимание на флаг <c>IsSuccess</c>).</returns>
    /// <remarks>HTTP: <c>POST /api/TrackAndTrace/StockDeclarationCancel</c>. Роль: <c>ApiStockDeclarationCancel</c>.</remarks>
    public Task<StockDeclarationCancelResponse?> StockDeclarationCancelAsync(
        long declarationId,
        CancellationToken cancellationToken = default)
        => PostAsync(
            "StockDeclarationCancel",
            new StockDeclarationCancelRequest { DeclarationId = declarationId },
            KnddbJson.TypeInfo<StockDeclarationCancelRequest>(),
            KnddbJson.TypeInfo<StockDeclarationCancelResponse>(),
            cancellationToken);

    /// <summary>
    /// Сводные остатки по всем доступным препаратам.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Сводка остатков в разрезе GTIN.</returns>
    /// <remarks>HTTP: <c>POST /api/TrackAndTrace/GetStockInheldList</c>. Роль: <c>ApiGetStockInheldList</c>.</remarks>
    public Task<GetStockInheldListResponse?> GetStockInheldListAsync(CancellationToken cancellationToken = default)
        => _transport.SendAsync(
            static () => new HttpRequestMessage(HttpMethod.Post, TrackAndTracePath + "GetStockInheldList"),
            HttpMethod.Post,
            "/" + TrackAndTracePath + "GetStockInheldList",
            KnddbJson.TypeInfo<GetStockInheldListResponse>(),
            requireAuthentication: true,
            cancellationToken);

    /// <summary>
    /// Детальные остатки по конкретному GTIN (упаковка за упаковкой).
    /// </summary>
    /// <param name="gtin">GTIN препарата.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Перечень упаковок в наличии.</returns>
    /// <remarks>HTTP: <c>POST /api/TrackAndTrace/GetStockInheldListByGtin</c>. Роль: <c>ApiGetStockInheldListByGtin</c>.</remarks>
    public Task<GetStockInheldListByGtinResponse?> GetStockInheldListByGtinAsync(
        string gtin,
        CancellationToken cancellationToken = default)
        => PostAsync(
            "GetStockInheldListByGtin",
            new GetStockInheldListByGtinRequest { Gtin = gtin },
            KnddbJson.TypeInfo<GetStockInheldListByGtinRequest>(),
            KnddbJson.TypeInfo<GetStockInheldListByGtinResponse>(),
            cancellationToken);

    /// <summary>
    /// Сведения о частично проданной упаковке.
    /// </summary>
    /// <param name="qrCode">QR-код упаковки.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Препарат, серийный номер и уже реализованное количество.</returns>
    /// <remarks>HTTP: <c>POST /api/TrackAndTrace/GetPartialSaleInfo</c>. Роль: <c>ApiGetPartialSaleInfo</c>.</remarks>
    public Task<GetPartialSaleInfoResponse?> GetPartialSaleInfoAsync(
        string qrCode,
        CancellationToken cancellationToken = default)
        => PostAsync(
            "GetPartialSaleInfo",
            new GetPartialSaleInfoRequest { QrCode = qrCode },
            KnddbJson.TypeInfo<GetPartialSaleInfoRequest>(),
            KnddbJson.TypeInfo<GetPartialSaleInfoResponse>(),
            cancellationToken);

    /// <summary>
    /// Создаёт декларацию реализации (продажи или расхода) упаковок.
    /// </summary>
    /// <param name="request">Реквизиты реализации и список упаковок.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Идентификатор созданной декларации реализации.</returns>
    /// <remarks>HTTP: <c>POST /api/TrackAndTrace/SalesDeclaration</c>. Роль: <c>ApiSalesDeclaration</c>.</remarks>
    public Task<SalesDeclarationResponse?> SalesDeclarationAsync(
        SalesDeclarationRequest request,
        CancellationToken cancellationToken = default)
        => PostAsync(
            "SalesDeclaration",
            request ?? throw new ArgumentNullException(nameof(request)),
            KnddbJson.TypeInfo<SalesDeclarationRequest>(),
            KnddbJson.TypeInfo<SalesDeclarationResponse>(),
            cancellationToken);

    /// <summary>
    /// Отменяет декларацию реализации целиком.
    /// </summary>
    /// <param name="declarationId">Идентификатор декларации.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Идентификатор и дата операции.</returns>
    /// <remarks>HTTP: <c>POST /api/TrackAndTrace/SalesDeclarationCancel</c>. Роль: <c>ApiSalesDeclarationCancel</c>.</remarks>
    public Task<SalesDeclarationResponse?> SalesDeclarationCancelAsync(
        long declarationId,
        CancellationToken cancellationToken = default)
        => PostAsync(
            "SalesDeclarationCancel",
            new SalesDeclarationCancelRequest { DeclarationId = declarationId },
            KnddbJson.TypeInfo<SalesDeclarationCancelRequest>(),
            KnddbJson.TypeInfo<SalesDeclarationResponse>(),
            cancellationToken);

    /// <summary>
    /// Отменяет реализацию одной упаковки по её QR-коду.
    /// </summary>
    /// <param name="qrCode">QR-код упаковки.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Идентификатор и дата операции.</returns>
    /// <remarks>HTTP: <c>POST /api/TrackAndTrace/SalesDeclarationBoxCancel</c>. Роль: <c>ApiSalesDeclarationBoxCancel</c>.</remarks>
    public Task<SalesDeclarationResponse?> SalesDeclarationBoxCancelAsync(
        string qrCode,
        CancellationToken cancellationToken = default)
        => PostAsync(
            "SalesDeclarationBoxCancel",
            new SalesDeclarationBoxCancelRequest { QrCode = qrCode },
            KnddbJson.TypeInfo<SalesDeclarationBoxCancelRequest>(),
            KnddbJson.TypeInfo<SalesDeclarationResponse>(),
            cancellationToken);

    /// <summary>
    /// Деактивирует (списывает) упаковки с указанием типа расходной операции.
    /// </summary>
    /// <param name="request">Тип списания, комментарий и список упаковок.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Идентификатор созданной декларации деактивации.</returns>
    /// <remarks>HTTP: <c>POST /api/TrackAndTrace/DeactivateDeclaration</c>. Роль: <c>ApiDeactivateDeclaration</c>.</remarks>
    public Task<DeactivateDeclarationResponse?> DeactivateDeclarationAsync(
        DeactivateDeclarationRequest request,
        CancellationToken cancellationToken = default)
        => PostAsync(
            "DeactivateDeclaration",
            request ?? throw new ArgumentNullException(nameof(request)),
            KnddbJson.TypeInfo<DeactivateDeclarationRequest>(),
            KnddbJson.TypeInfo<DeactivateDeclarationResponse>(),
            cancellationToken);

    /// <summary>
    /// Освобождает ресурсы. Если клиент создан методом <see cref="Create"/>, освобождается и HTTP-клиент.
    /// </summary>
    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private Task<TResponse?> PostAsync<TRequest, TResponse>(
        string action,
        TRequest request,
        JsonTypeInfo<TRequest> requestTypeInfo,
        JsonTypeInfo<TResponse> responseTypeInfo,
        CancellationToken cancellationToken,
        bool requireAuthentication = true)
    {
        var path = TrackAndTracePath + action;

        return _transport.SendAsync(
            () => new HttpRequestMessage(HttpMethod.Post, path)
            {
                Content = CreateJsonContent(request, requestTypeInfo),
            },
            HttpMethod.Post,
            "/" + path,
            responseTypeInfo,
            requireAuthentication,
            cancellationToken);
    }

    private static HttpContent CreateJsonContent<TRequest>(TRequest request, JsonTypeInfo<TRequest> typeInfo)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(request, typeInfo);

        return new StringContent(json, Encoding.UTF8)
        {
            Headers = { ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" } },
        };
    }
}
