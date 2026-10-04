using System.Net.Http.Headers;
using Knmdb.TrackAndTrace.Models.Auth;

namespace Knmdb.TrackAndTrace;

/// <summary>
/// Контур (окружение) API KNMDB.
/// </summary>
/// <remarks>
/// <list type="table">
///   <listheader>
///     <term>Контур</term>
///     <description>Базовый адрес</description>
///   </listheader>
///   <item>
///     <term><see cref="Test"/></term>
///     <description><c>https://testndbapi.med.kg/</c> — тестовый контур.</description>
///   </item>
///   <item>
///     <term><see cref="Production"/></term>
///     <description><c>https://ndbapi.med.kg/</c> — боевой контур.</description>
///   </item>
/// </list>
/// </remarks>
public enum KnddbEnvironment
{
    /// <summary>
    /// Тестовый контур: <c>https://testndbapi.med.kg/</c>.
    /// </summary>
    /// <remarks>Безопасен для разработки и отладки. Используется по умолчанию.</remarks>
    Test = 0,

    /// <summary>
    /// Боевой контур: <c>https://ndbapi.med.kg/</c>.
    /// </summary>
    /// <remarks>Все операции реальны и необратимы.</remarks>
    Production = 1,
}

/// <summary>
/// Настройки SDK KNMDB.
/// </summary>
/// <remarks>
/// <para>
/// Обязательно задаётся только контур: <see cref="KnddbEnvironment.Test"/> (по умолчанию)
/// или <see cref="KnddbEnvironment.Production"/>.
/// </para>
/// <code>
/// var options = new KnddbClientOptions { Environment = KnddbEnvironment.Production };
/// </code>
/// <para>
/// Остальные свойства необязательны: разумные значения подставлены по умолчанию.
/// Настройки привязываются к конфигурации приложения (секция <c>Knddb</c>) при подключении
/// через <c>AddKnddbTrackAndTrace</c>.
/// </para>
/// </remarks>
public sealed class KnddbClientOptions
{
    /// <summary>
    /// Пользовательский агент по умолчанию.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Сервер KNMDB записывает заголовок <c>User-Agent</c> в базу данных при входе.
    /// Если заголовок не передан, запись завершается ошибкой:
    /// <c>resultCode 2</c> («An error occurred while saving the entity changes»).
    /// Поэтому SDK отправляет этот заголовок всегда — даже если приложение его не задало.
    /// </para>
    /// <para>
    /// Своё значение задавайте через <see cref="UserAgent"/>: полезно указывать название
    /// и версию приложения, чтобы администратор KNMDB мог отличить интеграции в журналах.
    /// </para>
    /// </remarks>
    public const string DefaultUserAgent = "Knmdb.TrackAndTrace.SDK/1.0";

    /// <summary>
    /// Область доступа (scope) OAuth 2.0 по умолчанию: <c>api</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Сервер KNMDB регистрирует только область <c>api</c>
    /// (<c>options.RegisterScopes("api")</c> в конфигурации OpenIddict).
    /// </para>
    /// <para>
    /// <b>Не добавляйте <c>offline_access</c>.</b> В OpenIddict эта область
    /// разрешена только при включённом потоке обновления токена
    /// (<c>AllowRefreshTokenFlow()</c>). Сервер KNMDB его не включает, поэтому
    /// запрос с <c>offline_access</c> отклоняется целиком:
    /// <c>invalid_request</c> — «The 'offline_access' scope is not allowed»,
    /// и вход становится невозможным.
    /// </para>
    /// <para>
    /// Своё значение задавайте через <see cref="Scope"/>, если конфигурация
    /// вашего сервера отличается.
    /// </para>
    /// </remarks>
    public const string DefaultScope = "api";

    /// <summary>Имя раздела конфигурации по умолчанию: <c>Knddb</c>.</summary>
    public const string DefaultSectionName = "Knddb";

    /// <summary>Имя именованного <see cref="HttpClient"/> по умолчанию.</summary>
    public const string DefaultHttpClientName = "Knddb.TrackAndTrace";

    /// <summary>
    /// Контур API KNMDB.
    /// </summary>
    /// <remarks>
    /// <para>По умолчанию — <see cref="KnddbEnvironment.Test"/>.</para>
    /// <para>
    /// Значение определяет <see cref="BaseAddress"/>, если адрес не задан явно.
    /// Для боевой эксплуатации укажите <see cref="KnddbEnvironment.Production"/> осознанно.
    /// </para>
    /// </remarks>
    public KnddbEnvironment Environment { get; set; } = KnddbEnvironment.Test;

    /// <summary>
    /// Базовый адрес API. Если не задан, берётся из <see cref="Environment"/>.
    /// </summary>
    /// <remarks>
    /// Задавайте явно только для нестандартного адреса (локальный стенд, прокси).
    /// </remarks>
    public Uri? BaseAddress { get; set; }

    /// <summary>
    /// Таймаут одного HTTP-запроса. По умолчанию — 100 секунд.
    /// </summary>
    /// <remarks>Увеличьте для массовых операций с большим количеством QR-кодов.</remarks>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(100);

    /// <summary>
    /// Логин и пароль по умолчанию.
    /// </summary>
    /// <remarks>
    /// Заполняйте, если приложение работает под одной учётной записью и не хочет вызывать
    /// <c>SignInAsync</c>. Для многопользовательских приложений оставьте <see langword="null"/>
    /// и выполняйте вход явно.
    /// </remarks>
    public KnddbCredentials? DefaultCredentials { get; set; }

    /// <summary>
    /// Пользовательский агент в заголовке <c>User-Agent</c> (необязательно).
    /// </summary>
    /// <remarks>
    /// Если не задан, отправляется <see cref="DefaultUserAgent"/>. Заголовок
    /// отправляется всегда: без него сервер KNMDB не может сохранить запись
    /// о входе (см. <see cref="DefaultUserAgent"/>).
    /// </remarks>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Область доступа (scope) OAuth 2.0, запрашиваемая при входе.
    /// </summary>
    /// <remarks>
    /// <para>
    /// По умолчанию — <see cref="DefaultScope"/> (<c>api</c>).
    /// </para>
    /// <para>
    /// Меняйте только если конфигурация вашего сервера KNMDB отличается.
    /// Значение <c>offline_access</c> приведёт к отказу входа, если на сервере
    /// не включён поток обновления токена.
    /// </para>
    /// <para>
    /// Пустая строка означает «не передавать параметр <c>scope</c>» — тогда
    /// сервер использует области по умолчанию.
    /// </para>
    /// </remarks>
    public string? Scope { get; set; }

    /// <summary>
    /// Запас времени до истечения токена, при котором он обновляется заранее.
    /// </summary>
    /// <remarks>По умолчанию — 30 секунд. Компенсирует расхождение часов клиента и сервера.</remarks>
    public TimeSpan TokenExpirationMargin { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Имя сессии по умолчанию. По умолчанию — <c>default</c>.
    /// </summary>
    public string DefaultSessionKey { get; set; } = KnddbSession.DefaultKey;

    /// <summary>
    /// Имя именованного <see cref="HttpClient"/> в <c>IHttpClientFactory</c>.
    /// </summary>
    /// <remarks>Меняйте, только если приложению нужны собственные обработчики HTTP-запросов.</remarks>
    public string HttpClientName { get; set; } = DefaultHttpClientName;

    /// <summary>
    /// Контур, которому соответствует текущий адрес подключения.
    /// </summary>
    /// <remarks>
    /// <see langword="null"/>, если адрес не совпадает ни с тестовым, ни с боевым контуром.
    /// </remarks>
    public KnddbEnvironment? ResolvedEnvironment
        => BaseAddress is null
            ? Environment
            : BaseAddress.Host.Contains("testndbapi", StringComparison.OrdinalIgnoreCase)
                ? KnddbEnvironment.Test
                : BaseAddress.Host.Contains("ndbapi", StringComparison.OrdinalIgnoreCase)
                    ? KnddbEnvironment.Production
                    : null;

    /// <summary>
    /// Базовый адрес, по которому будет работать SDK.
    /// </summary>
    /// <remarks>Явно заданный <see cref="BaseAddress"/> либо адрес выбранного контура.</remarks>
    public Uri EffectiveBaseAddress => BaseAddress ?? GetEnvironmentAddress(Environment);

    /// <summary>
    /// Признак работы на боевом контуре.
    /// </summary>
    /// <remarks>
    /// Удобно для предупреждений в приложении перед необратимыми операциями.
    /// </remarks>
    public bool IsProduction => ResolvedEnvironment == KnddbEnvironment.Production;

    /// <summary>
    /// Задаёт контур и соответствующий ему адрес.
    /// </summary>
    /// <param name="environment">Контур: тестовый или боевой.</param>
    /// <returns>Те же настройки для продолжения цепочки вызовов.</returns>
    /// <remarks>
    /// <code>
    /// var options = new KnddbClientOptions().ForEnvironment(KnddbEnvironment.Production);
    /// // options.BaseAddress == https://ndbapi.med.kg/
    /// </code>
    /// </remarks>
    public KnddbClientOptions ForEnvironment(KnddbEnvironment environment)
    {
        Environment = environment;
        BaseAddress = GetEnvironmentAddress(environment);
        return this;
    }

    /// <summary>
    /// Задаёт тестовый контур: <c>https://testndbapi.med.kg/</c>.
    /// </summary>
    /// <returns>Те же настройки для продолжения цепочки вызовов.</returns>
    /// <remarks>Рекомендуется для разработки, отладки и автоматических тестов.</remarks>
    public KnddbClientOptions ForTest() => ForEnvironment(KnddbEnvironment.Test);

    /// <summary>
    /// Задаёт боевой контур: <c>https://ndbapi.med.kg/</c>.
    /// </summary>
    /// <returns>Те же настройки для продолжения цепочки вызовов.</returns>
    /// <remarks>Все операции на боевом контуре реальны и необратимы.</remarks>
    public KnddbClientOptions ForProduction() => ForEnvironment(KnddbEnvironment.Production);

    /// <summary>
    /// Возвращает базовый адрес указанного контура.
    /// </summary>
    /// <param name="environment">Контур.</param>
    /// <returns>Базовый адрес контура.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Контур неизвестен.</exception>
    public static Uri GetEnvironmentAddress(KnddbEnvironment environment) => environment switch
    {
        KnddbEnvironment.Test => new Uri("https://testndbapi.med.kg/", UriKind.Absolute),
        KnddbEnvironment.Production => new Uri("https://ndbapi.med.kg/", UriKind.Absolute),
        _ => throw new ArgumentOutOfRangeException(
            nameof(environment),
            environment,
            "Неизвестный контур KNMDB."),
    };

    /// <summary>
    /// Проверяет настройки и приводит базовый адрес к виду, оканчивающемуся на «/».
    /// </summary>
    /// <returns>Нормализованный базовый адрес.</returns>
    /// <exception cref="KnddbConfigurationException">Настройки некорректны.</exception>
    public Uri Validate()
    {
        var address = EffectiveBaseAddress;

        if (!address.IsAbsoluteUri)
        {
            throw new KnddbConfigurationException(
                $"Базовый адрес API KNMDB должен быть абсолютным URI, получено: '{address}'.");
        }

        if (address.Scheme is not ("http" or "https"))
        {
            throw new KnddbConfigurationException(
                $"Схема базового адреса должна быть http или https, получено: '{address.Scheme}'.");
        }

        if (Timeout <= TimeSpan.Zero)
        {
            throw new KnddbConfigurationException("KnddbClientOptions.Timeout должен быть больше нуля.");
        }

        if (string.IsNullOrWhiteSpace(DefaultSessionKey))
        {
            throw new KnddbConfigurationException("KnddbClientOptions.DefaultSessionKey не может быть пустым.");
        }

        if (string.IsNullOrWhiteSpace(HttpClientName))
        {
            throw new KnddbConfigurationException("KnddbClientOptions.HttpClientName не может быть пустым.");
        }

        var normalized = address.AbsoluteUri.EndsWith('/')
            ? address
            : new Uri(address.AbsoluteUri + "/", UriKind.Absolute);

        BaseAddress = normalized;
        return normalized;
    }

    /// <summary>
    /// Создаёт «сырой» <see cref="HttpClient"/> по текущим настройкам.
    /// </summary>
    /// <param name="handler">Пользовательский обработчик HTTP (необязательно).</param>
    /// <returns>Настроенный клиент; владельцем становится вызывающая сторона.</returns>
    /// <remarks>
    /// Используйте, когда SDK работает без контейнера внедрения зависимостей.
    /// Заголовок <c>Authorization</c> здесь не задаётся: токен подставляется из текущей сессии
    /// на каждый запрос, поэтому один клиент безопасно обслуживает нескольких пользователей.
    /// </remarks>
    public HttpClient CreateHttpClient(HttpMessageHandler? handler = null)
    {
        var client = handler is null
            ? new HttpClient(CreateHandler(), disposeHandler: true)
            : new HttpClient(handler, disposeHandler: true);

        client.BaseAddress = Validate();
        client.Timeout = Timeout;

        // Заголовок User-Agent отправляется всегда: без него сервер KNMDB
        // не может сохранить запись о входе (resultCode 2).
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            "User-Agent",
            string.IsNullOrWhiteSpace(UserAgent) ? DefaultUserAgent : UserAgent);

        return client;
    }

    /// <summary>
    /// Создаёт HTTP-обработчик с разумными настройками для мобильных и серверных приложений.
    /// </summary>
    /// <returns>Настроенный обработчик.</returns>
    public static SocketsHttpHandler CreateHandler() => new()
    {
        // Ограничиваем время жизни соединения, чтобы изменения DNS подхватывались
        // без пересоздания HttpClient.
        PooledConnectionLifetime = TimeSpan.FromMinutes(10),
        AutomaticDecompression = System.Net.DecompressionMethods.All,
        ConnectTimeout = TimeSpan.FromSeconds(30),
    };

    /// <summary>
    /// Добавляет к запросу заголовки, заданные в настройках.
    /// </summary>
    /// <param name="request">Формируемый запрос.</param>
    /// <remarks>
    /// Заголовок <c>User-Agent</c> подставляется всегда. Без него сервер KNMDB
    /// не может сохранить запись о входе и отвечает кодом результата 2 —
    /// «An error occurred while saving the entity changes».
    /// </remarks>
    internal void ApplyHeaders(HttpRequestMessage request)
    {
        if (request.Headers.Contains("User-Agent"))
        {
            return;
        }

        request.Headers.TryAddWithoutValidation(
            "User-Agent",
            string.IsNullOrWhiteSpace(UserAgent) ? DefaultUserAgent : UserAgent);
    }

    /// <summary>
    /// Готовит заголовки по умолчанию для именованного <see cref="HttpClient"/>.
    /// </summary>
    /// <param name="client">Настраиваемый клиент.</param>
    internal void ApplyDefaultHeaders(HttpClient client)
    {
        client.BaseAddress = Validate();
        client.Timeout = Timeout;

        // Заголовок User-Agent отправляется всегда: без него сервер KNMDB
        // не может сохранить запись о входе (resultCode 2).
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            "User-Agent",
            string.IsNullOrWhiteSpace(UserAgent) ? DefaultUserAgent : UserAgent);

        if (client.DefaultRequestHeaders.AcceptEncoding.Count == 0)
        {
            client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
            client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("deflate"));
            client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("br"));
        }
    }
}
