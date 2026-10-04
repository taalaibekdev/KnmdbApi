using System.Net;
using System.Text.Json;
using Knmdb.TrackAndTrace.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Knmdb.TrackAndTrace;

/// <summary>
/// Получает и обновляет токены OAuth 2.0 для сессий KNMDB.
/// </summary>
/// <remarks>
/// <para>
/// Работает с конечной точкой <c>POST /connect/token</c>. Схема:
/// </para>
/// <list type="number">
///   <item><description>вход по логину и паролю (<c>grant_type=password</c>);</description></item>
///   <item><description>обновление по токену обновления (<c>grant_type=refresh_token</c>);</description></item>
///   <item><description>повторный вход по сохранённым учётным данным, если токен обновления отклонён.</description></item>
/// </list>
/// <para>Настройки читаются из <see cref="KnddbClientOptions"/> на каждом вызове, поэтому их изменение
/// (например, смена контура) подхватывается без пересоздания SDK.</para>
/// </remarks>
internal sealed class KnddbTokenStore
{
    private const string TokenPath = "connect/token";

    private readonly Func<HttpClient> _httpClientProvider;
    private readonly Func<KnddbClientOptions> _optionsProvider;
    private readonly IKnddbSessionStore _sessions;
    private readonly IKnddbCredentialStore _credentials;
    private readonly ILogger<KnddbTokenStore> _logger;

    public KnddbTokenStore(
        Func<HttpClient> httpClientProvider,
        Func<KnddbClientOptions> optionsProvider,
        IKnddbSessionStore sessions,
        IKnddbCredentialStore credentials,
        ILogger<KnddbTokenStore>? logger = null)
    {
        _httpClientProvider = httpClientProvider;
        _optionsProvider = optionsProvider;
        _sessions = sessions;
        _credentials = credentials;
        _logger = logger ?? NullLogger<KnddbTokenStore>.Instance;
    }

    /// <summary>
    /// Создаёт сессию и выполняет вход по логину и паролю.
    /// </summary>
    /// <param name="sessionKey">Ключ сессии.</param>
    /// <param name="credentials">Учётные данные (необязательно — иначе берутся сохранённые или данные по умолчанию).</param>
    /// <param name="storeCredentials">Сохранять ли пароль в памяти для автоматического продления.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Сессия с действующими токенами.</returns>
    public async Task<KnddbSession> SignInAsync(
        string sessionKey,
        Models.Auth.KnddbCredentials? credentials = null,
        bool storeCredentials = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionKey);

        var session = _sessions.GetOrCreate(sessionKey);
        var effective = ResolveCredentials(sessionKey, session, credentials);

        await session.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var response = await RequestTokenAsync(
                BuildPasswordGrant(effective),
                sessionKey,
                cancellationToken).ConfigureAwait(false);

            session.UserName = effective.UserName;
            session.Credentials = storeCredentials ? effective : null;
            session.ApplyTokenResponse(response);

            if (storeCredentials)
            {
                _credentials.Set(sessionKey, effective);
            }
            else
            {
                _credentials.Remove(sessionKey);
            }

            _logger.LogInformation(
                "Вход в KNMDB выполнен: сессия '{SessionKey}', пользователь '{UserName}', токен действует до {ExpiresAt:u}.",
                sessionKey,
                session.UserName,
                session.AccessTokenExpiresAt);

            return session;
        }
        finally
        {
            session.Gate.Release();
        }
    }

    /// <summary>
    /// Возвращает сессию с действующим токеном доступа, получая или обновляя его при необходимости.
    /// </summary>
    /// <param name="sessionKey">Ключ сессии.</param>
    /// <param name="requireAuthentication">
    /// <see langword="true"/> — для защищённых методов: если войти не удалось, выбрасывается исключение.
    /// <see langword="false"/> — для анонимных методов: запрос может уйти без токена.
    /// </param>
    /// <param name="forceRefresh">Принудительно обновить токен (после ответа 401).</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Сессия.</returns>
    public async Task<KnddbSession> GetSessionAsync(
        string sessionKey,
        bool requireAuthentication = true,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionKey);

        var options = _optionsProvider();
        var session = _sessions.GetOrCreate(sessionKey);

        if (!forceRefresh && session.IsAccessTokenValid(options.TokenExpirationMargin))
        {
            return session;
        }

        var credentials = session.Credentials
                          ?? _credentials.Get(sessionKey)
                          ?? options.DefaultCredentials;

        if (credentials is null)
        {
            if (requireAuthentication)
            {
                throw new KnddbAuthenticationException(
                    $"Для сессии '{sessionKey}' нет действующего токена и не заданы учётные данные. " +
                    "Выполните вход: SignInAsync(login, password) или заполните KnddbClientOptions.DefaultCredentials.")
                {
                    SessionKey = sessionKey,
                };
            }

            return session;
        }

        session.Credentials ??= credentials;

        await session.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Пока ждали семафор, токен мог обновиться в параллельном запросе.
            if (!forceRefresh && session.IsAccessTokenValid(options.TokenExpirationMargin))
            {
                return session;
            }

            return await RenewAsync(session, credentials, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            session.Gate.Release();
        }
    }

    /// <summary>
    /// Завершает сессию: удаляет токены и сохранённые учётные данные.
    /// </summary>
    /// <param name="sessionKey">Ключ сессии.</param>
    public void SignOut(string sessionKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionKey);

        _credentials.Remove(sessionKey);

        var session = _sessions.Get(sessionKey);
        if (session is not null)
        {
            session.ClearTokens();
            session.Credentials = null;
            session.UserName = null;
        }

        _sessions.Remove(sessionKey);
        _logger.LogInformation("Сессия '{SessionKey}' завершена.", sessionKey);
    }

    private async Task<KnddbSession> RenewAsync(
        KnddbSession session,
        Models.Auth.KnddbCredentials credentials,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(session.RefreshToken))
        {
            try
            {
                var refreshed = await RequestTokenAsync(
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["grant_type"] = "refresh_token",
                        ["refresh_token"] = session.RefreshToken!,
                    },
                    session.Key,
                    cancellationToken).ConfigureAwait(false);

                session.ApplyTokenResponse(refreshed);
                return session;
            }
            catch (KnddbAuthenticationException ex)
            {
                // Токен обновления отклонён — переходим к повторному входу по логину и паролю.
                _logger.LogInformation(
                    ex,
                    "Сессия '{SessionKey}': токен обновления отклонён, выполняется повторный вход.",
                    session.Key);

                session.RefreshToken = null;
            }
        }

        var response = await RequestTokenAsync(
            BuildPasswordGrant(credentials),
            session.Key,
            cancellationToken).ConfigureAwait(false);

        session.Credentials ??= credentials;
        session.UserName = credentials.UserName;
        session.ApplyTokenResponse(response);

        return session;
    }

    /// <summary>
    /// Формирует тело запроса токена для потока «логин и пароль».
    /// </summary>
    /// <param name="credentials">Учётные данные.</param>
    /// <returns>Поля формы запроса.</returns>
    /// <remarks>
    /// Параметр <c>scope</c> добавляется, только если он задан явно.
    /// Пустое значение <see cref="KnddbClientOptions.Scope"/> означает
    /// «использовать области сервера по умолчанию».
    /// </remarks>
    private Dictionary<string, string> BuildPasswordGrant(Models.Auth.KnddbCredentials credentials)
    {
        var form = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["grant_type"] = "password",
            ["username"] = credentials.UserName,
            ["password"] = credentials.Password,
        };

        var scope = ResolveScope();
        if (!string.IsNullOrWhiteSpace(scope))
        {
            form["scope"] = scope;
        }

        return form;
    }

    /// <summary>
    /// Возвращает область доступа (scope) для запроса токена.
    /// </summary>
    /// <returns>
    /// Значение <see cref="KnddbClientOptions.Scope"/> либо
    /// <see cref="KnddbClientOptions.DefaultScope"/>, если оно не задано.
    /// </returns>
    /// <remarks>
    /// Область <c>offline_access</c> не добавляется: сервер KNMDB её не
    /// регистрирует, и запрос с ней отклоняется целиком — вход становится
    /// невозможным.
    /// </remarks>
    private string ResolveScope()
    {
        var scope = _optionsProvider().Scope;

        return scope ?? KnddbClientOptions.DefaultScope;
    }

    private Models.Auth.KnddbCredentials ResolveCredentials(
        string sessionKey,
        KnddbSession session,
        Models.Auth.KnddbCredentials? credentials)
    {
        var resolved = credentials
                       ?? session.Credentials
                       ?? _credentials.Get(sessionKey)
                       ?? _optionsProvider().DefaultCredentials;

        if (resolved is null)
        {
            throw new KnddbConfigurationException(
                $"Не заданы учётные данные для сессии '{sessionKey}'. " +
                "Передайте логин и пароль в SignInAsync(...) или заполните KnddbClientOptions.DefaultCredentials.");
        }

        if (string.IsNullOrWhiteSpace(resolved.UserName))
        {
            throw new KnddbConfigurationException("Логин пользователя KNMDB не может быть пустым.");
        }

        if (string.IsNullOrEmpty(resolved.Password))
        {
            throw new KnddbConfigurationException("Пароль пользователя KNMDB не может быть пустым.");
        }

        return resolved;
    }

    private async Task<Models.Auth.TokenResponse> RequestTokenAsync(
        Dictionary<string, string> form,
        string sessionKey,
        CancellationToken cancellationToken)
    {
        var options = _optionsProvider();
        var baseAddress = options.Validate();
        var client = _httpClientProvider();

        using var content = new FormUrlEncodedContent(form);
        using var request = new HttpRequestMessage(HttpMethod.Post, TokenPath) { Content = content };
        options.ApplyHeaders(request);

        HttpResponseMessage response;
        try
        {
            response = await client
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new KnddbAuthenticationException(
                $"Не удалось обратиться к серверу авторизации KNMDB по адресу " +
                $"'{new Uri(baseAddress, TokenPath)}'. Проверьте сеть и адрес сервера.",
                ex)
            {
                Method = "POST",
                RequestUri = "/" + TokenPath,
                SessionKey = sessionKey,
            };
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new KnddbAuthenticationException(
                $"Превышен таймаут ({options.Timeout}) при обращении к серверу авторизации KNMDB.", ex)
            {
                Method = "POST",
                RequestUri = "/" + TokenPath,
                SessionKey = sessionKey,
            };
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw CreateAuthenticationException(response.StatusCode, body, sessionKey);
            }

            // Сервер может ответить кодом 200 и конвертом с ошибкой: обработчик
            // исключений KNMDB возвращает статус 200 даже для внутренних сбоев.
            // Без этой проверки пользователь увидел бы невнятное «сервер не вернул
            // поле access_token» вместо настоящей причины.
            var envelope = TryReadErrorEnvelope(body);
            if (envelope is not null)
            {
                throw CreateEnvelopeException(envelope.Value, sessionKey, body);
            }

            try
            {
                return JsonSerializer.Deserialize(body, KnddbJson.TypeInfo<Models.Auth.TokenResponse>())
                       ?? throw new KnddbAuthenticationException(
                           "Сервер авторизации KNMDB вернул пустой ответ на запрос токена.")
                       {
                           SessionKey = sessionKey,
                       };
            }
            catch (JsonException ex)
            {
                throw new KnddbAuthenticationException(
                    "Не удалось разобрать ответ сервера авторизации KNMDB. " +
                    $"Тело ответа: {Truncate(body, 512)}", ex)
                {
                    SessionKey = sessionKey,
                    ResponseBody = body,
                };
            }
        }
    }

    private static KnddbAuthenticationException CreateAuthenticationException(
        HttpStatusCode statusCode,
        string body,
        string sessionKey)
    {
        var (error, description) = TryParseOAuthError(body);

        var message = statusCode switch
        {
            HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized =>
                "Сервер KNMDB отклонил запрос токена: неверный логин или пароль, " +
                "либо токен обновления больше не действителен.",
            HttpStatusCode.Forbidden =>
                "Учётной записи KNMDB запрещён вход (учётная запись заблокирована или отключена).",
            HttpStatusCode.ServiceUnavailable =>
                "Сервер авторизации KNMDB временно недоступен.",
            _ => $"Сервер авторизации KNMDB вернул ошибку {(int)statusCode}.",
        };

        if (!string.IsNullOrWhiteSpace(error))
        {
            message = $"{message} Код ошибки OAuth: {error}" +
                      (string.IsNullOrWhiteSpace(description) ? "." : $". {description}");
        }

        return new KnddbAuthenticationException(message)
        {
            Method = "POST",
            RequestUri = "/" + TokenPath,
            StatusCode = statusCode,
            ResponseBody = body,
            SessionKey = sessionKey,
            OAuthError = error,
        };
    }

    /// <summary>
    /// Пытается прочитать конверт KNMDB с ошибкой из тела успешного ответа.
    /// </summary>
    /// <param name="body">Тело ответа.</param>
    /// <returns>
    /// Код и сообщение ошибки либо <see langword="null"/>, если конверта нет
    /// или он сообщает об успехе.
    /// </returns>
    /// <remarks>
    /// Обработчик исключений KNMDB отвечает статусом 200 и телом
    /// <c>{ "resultCode": …, "resultMessage": … }</c> даже при внутреннем сбое,
    /// в том числе на <c>/connect/token</c>.
    /// </remarks>
    private static (int Code, string? Message)? TryReadErrorEnvelope(string body)
    {
        if (string.IsNullOrWhiteSpace(body) || !body.TrimStart().StartsWith('{'))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("resultCode", out var codeElement)
                || !codeElement.TryGetInt32(out var code)
                || code == KnddbResultCodes.Success)
            {
                return null;
            }

            var message = root.TryGetProperty("resultMessage", out var messageElement)
                && messageElement.ValueKind == JsonValueKind.String
                    ? messageElement.GetString()
                    : null;

            return (code, message);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static KnddbAuthenticationException CreateEnvelopeException(
        (int Code, string? Message) envelope,
        string sessionKey,
        string body)
    {
        var description = KnddbResultCodes.Describe(envelope.Code);

        var message = $"Сервер KNMDB не выполнил вход (resultCode {envelope.Code}).";

        if (description is not null)
        {
            message = $"{message} {description}";
        }

        if (!string.IsNullOrWhiteSpace(envelope.Message))
        {
            message = $"{message} Ответ сервера: {envelope.Message}";
        }

        if (envelope.Code == KnddbResultCodes.UnexpectedError)
        {
            message += " Вероятная причина — несовместимость запроса с сервером: "
                     + "проверьте заголовок User-Agent и формат дат.";
        }

        return new KnddbAuthenticationException(message)
        {
            Method = "POST",
            RequestUri = "/" + TokenPath,
            StatusCode = HttpStatusCode.OK,
            ResponseBody = body,
            ResultCode = envelope.Code,
            ResultMessage = envelope.Message,
            SessionKey = sessionKey,
        };
    }

    private static (string? Error, string? Description) TryParseOAuthError(string body)
    {
        if (string.IsNullOrWhiteSpace(body) || !body.TrimStart().StartsWith('{'))
        {
            return (null, null);
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            var error = root.TryGetProperty("error", out var e) ? e.GetString() : null;
            var description = root.TryGetProperty("error_description", out var d) ? d.GetString() : null;

            if (error is null && description is null)
            {
                // Иногда сервер отвечает в формате ProblemDetails.
                error = root.TryGetProperty("title", out var t) ? t.GetString() : null;
                description = root.TryGetProperty("detail", out var det) ? det.GetString() : null;
            }

            return (error, description);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : string.Concat(value.AsSpan(0, maxLength), "…");
}
