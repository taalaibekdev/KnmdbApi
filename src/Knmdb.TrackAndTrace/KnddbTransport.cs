using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Knmdb.TrackAndTrace.Serialization;

namespace Knmdb.TrackAndTrace;

/// <summary>
/// Выполняет HTTP-вызовы к API KNMDB: подставляет токен текущего пользователя,
/// обновляет его при ответе 401 и преобразует ошибки сервера в <see cref="KnddbApiException"/>.
/// </summary>
internal sealed class KnddbTransport
{
    private readonly Func<HttpClient> _httpClientProvider;
    private readonly Func<KnddbClientOptions> _optionsProvider;
    private readonly KnddbTokenStore _tokens;
    private readonly KnddbCurrentUser _currentUser;
    private readonly Func<KnddbSession?> _sessionFallback;

    public KnddbTransport(
        Func<HttpClient> httpClientProvider,
        Func<KnddbClientOptions> optionsProvider,
        KnddbTokenStore tokens,
        KnddbCurrentUser currentUser,
        Func<KnddbSession?> sessionFallback)
    {
        _httpClientProvider = httpClientProvider;
        _optionsProvider = optionsProvider;
        _tokens = tokens;
        _currentUser = currentUser;
        _sessionFallback = sessionFallback;
    }

    /// <summary>Ключ сессии текущего пользователя.</summary>
    public string ResolveSessionKey()
    {
        var options = _optionsProvider();
        return _currentUser.Current?.Key
               ?? _sessionFallback()?.Key
               ?? options.DefaultSessionKey;
    }

    /// <summary>Текущая сессия без обращения к сети.</summary>
    public KnddbSession? GetCurrentSession() => _currentUser.Current ?? _sessionFallback();

    /// <summary>
    /// Выполняет запрос и десериализует ответ.
    /// </summary>
    /// <typeparam name="TResponse">Тип ответа.</typeparam>
    /// <param name="requestFactory">Фабрика запроса (создаёт новый запрос на каждую попытку).</param>
    /// <param name="method">HTTP-метод.</param>
    /// <param name="path">Относительный путь.</param>
    /// <param name="responseTypeInfo">Метаданные типа ответа.</param>
    /// <param name="requireAuthentication">Нужен ли токен (анонимные методы — нет).</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Ответ; <see langword="default"/>, если тело пустое.</returns>
    public async Task<TResponse?> SendAsync<TResponse>(
        Func<HttpRequestMessage> requestFactory,
        HttpMethod method,
        string path,
        JsonTypeInfo<TResponse> responseTypeInfo,
        bool requireAuthentication = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requestFactory);
        ArgumentNullException.ThrowIfNull(responseTypeInfo);

        return await SendCoreAsync(
            requestFactory,
            method,
            path,
            requireAuthentication,
            cancellationToken,
            async (response, token) =>
            {
                if (response.StatusCode == HttpStatusCode.NoContent
                    || response.Content.Headers.ContentLength == 0)
                {
                    return default;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
                return await JsonSerializer.DeserializeAsync(stream, responseTypeInfo, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
    }

    /// <summary>
    /// Выполняет запрос без полезного тела ответа (например, <c>POST /connect/logout</c>).
    /// </summary>
    /// <param name="requestFactory">Фабрика запроса.</param>
    /// <param name="method">HTTP-метод.</param>
    /// <param name="path">Относительный путь.</param>
    /// <param name="requireAuthentication">Нужен ли токен.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    public Task SendWithoutResponseAsync(
        Func<HttpRequestMessage> requestFactory,
        HttpMethod method,
        string path,
        bool requireAuthentication = true,
        CancellationToken cancellationToken = default)
        => SendCoreAsync(
            requestFactory,
            method,
            path,
            requireAuthentication,
            cancellationToken,
            static (_, _) => Task.FromResult(true));

    private async Task<TResult?> SendCoreAsync<TResult>(
        Func<HttpRequestMessage> requestFactory,
        HttpMethod method,
        string path,
        bool requireAuthentication,
        CancellationToken cancellationToken,
        Func<HttpResponseMessage, CancellationToken, Task<TResult?>> readResponse)
    {
        var options = _optionsProvider();
        options.Validate();

        var sessionKey = ResolveSessionKey();
        var session = await _tokens
            .GetSessionAsync(sessionKey, requireAuthentication, forceRefresh: false, cancellationToken)
            .ConfigureAwait(false);

        var authenticationRetried = false;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var request = requestFactory();
            ApplyAuthorization(request, session);
            options.ApplyHeaders(request);

            using var response = await _httpClientProvider()
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.Unauthorized
                && requireAuthentication
                && !authenticationRetried)
            {
                // Токен мог истечь раньше заявленного срока: обновляем и повторяем один раз.
                authenticationRetried = true;
                session = await _tokens
                    .GetSessionAsync(sessionKey, requireAuthentication, forceRefresh: true, cancellationToken)
                    .ConfigureAwait(false);

                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                throw CreateApiException(response, method, path, body, sessionKey);
            }

            return await readResponse(response, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Добавляет заголовок <c>Authorization</c> с токеном сессии.</summary>
    /// <param name="request">Запрос.</param>
    /// <param name="session">Сессия.</param>
    public static void ApplyAuthorization(HttpRequestMessage request, KnddbSession session)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(session.AccessToken))
        {
            return;
        }

        request.Headers.Authorization = new AuthenticationHeaderValue(
            string.IsNullOrWhiteSpace(session.TokenType) ? "Bearer" : session.TokenType,
            session.AccessToken);
    }

    private static KnddbApiException CreateApiException(
        HttpResponseMessage response,
        HttpMethod method,
        string path,
        string body,
        string sessionKey)
    {
        var problem = ParseProblem(body);
        var traceId = GetTraceId(response);

        var message = response.StatusCode switch
        {
            HttpStatusCode.BadRequest => "Сервер KNMDB отклонил запрос: некорректные данные.",
            HttpStatusCode.Unauthorized => "Сервер KNMDB не принял токен доступа (401). Требуется повторный вход.",
            HttpStatusCode.Forbidden =>
                "Недостаточно прав для вызова метода (403). Проверьте роли API у учётной записи KNMDB.",
            HttpStatusCode.NotFound => "Запрашиваемые данные не найдены (404).",
            HttpStatusCode.Conflict =>
                "Конфликт состояния на сервере KNMDB (409): операция несовместима с текущим состоянием объекта.",
            _ when (int)response.StatusCode >= 500 =>
                $"Внутренняя ошибка сервера KNMDB ({(int)response.StatusCode}).",
            _ => $"Сервер KNMDB вернул ошибку {(int)response.StatusCode}.",
        };

        var details = problem?.GetMessage();
        if (!string.IsNullOrWhiteSpace(details) && !message.Contains(details, StringComparison.Ordinal))
        {
            message = $"{message} {details}";
        }

        if (!string.IsNullOrWhiteSpace(traceId))
        {
            message = $"{message} (traceId: {traceId})";
        }

        var errors = ExtractErrors(problem, body);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return new KnddbAuthenticationException(message)
            {
                Method = method.Method,
                RequestUri = path,
                StatusCode = response.StatusCode,
                ResponseBody = body,
                ProblemDetails = problem,
                TraceId = traceId,
                Errors = errors,
                SessionKey = sessionKey,
            };
        }

        return new KnddbApiException(message)
        {
            Method = method.Method,
            RequestUri = path,
            StatusCode = response.StatusCode,
            ResponseBody = body,
            ProblemDetails = problem,
            TraceId = traceId,
            Errors = errors,
        };
    }

    private static string? GetTraceId(HttpResponseMessage response)
        => response.Headers.TryGetValues("X-Request-Id", out var values) ? values.FirstOrDefault() : null;

    private static Models.Api.ApiProblemDetails? ParseProblem(string body)
    {
        if (string.IsNullOrWhiteSpace(body) || !body.TrimStart().StartsWith('{'))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(body, KnddbJson.TypeInfo<Models.Api.ApiProblemDetails>());
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IReadOnlyDictionary<string, string>? ExtractErrors(Models.Api.ApiProblemDetails? problem, string body)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (problem?.Extensions is { Count: > 0 } extensions
            && extensions.TryGetValue("errors", out var errors)
            && errors is not null)
        {
            CollectErrors(errors.ToString(), result);
        }

        if (result.Count == 0 && !string.IsNullOrWhiteSpace(problem?.Detail))
        {
            result["detail"] = problem.Detail!;
        }

        if (result.Count == 0
            && problem is null
            && !string.IsNullOrWhiteSpace(body)
            && !body.TrimStart().StartsWith('{'))
        {
            result["response"] = body.Length > 512 ? body[..512] : body;
        }

        return result.Count == 0 ? null : result;
    }

    private static void CollectErrors(string? json, Dictionary<string, string> target)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                target[property.Name] = property.Value.ValueKind == JsonValueKind.Array
                    ? string.Join("; ", property.Value.EnumerateArray().Select(e => e.ToString()))
                    : property.Value.ToString();
            }
        }
        catch (JsonException)
        {
            // Ошибки валидации не критичны: приложение всё равно получит статус и тело ответа.
        }
    }
}
