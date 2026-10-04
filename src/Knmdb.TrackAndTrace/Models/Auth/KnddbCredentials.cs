using System.Text.Json.Serialization;

namespace Knmdb.TrackAndTrace.Models.Auth;

/// <summary>
/// Пара учётных данных пользователя KNMDB (логин и пароль).
/// </summary>
/// <remarks>
/// Используется исключительно на клиенте, в JSON не сериализуется и по сети как отдельный объект
/// не передаётся: логин и пароль уходят в теле запроса <c>POST /connect/token</c> в виде
/// <c>application/x-www-form-urlencoded</c>.
/// </remarks>
public sealed class KnddbCredentials
{
    /// <summary>
    /// Логин пользователя KNMDB (имя пользователя в системе).
    /// </summary>
    /// <remarks>Обязателен. В запросе уходит как поле <c>username</c>.</remarks>
    public required string UserName { get; init; }

    /// <summary>
    /// Пароль пользователя KNMDB.
    /// </summary>
    /// <remarks>Обязателен. В запросе уходит как поле <c>password</c>.</remarks>
    public required string Password { get; init; }

    /// <summary>Создаёт пару учётных данных.</summary>
    /// <param name="userName">Логин пользователя.</param>
    /// <param name="password">Пароль пользователя.</param>
    public static KnddbCredentials Create(string userName, string password) => new()
    {
        UserName = userName,
        Password = password,
    };

    /// <summary>
    /// Удобное строковое представление без раскрытия пароля.
    /// </summary>
    /// <returns>Строка вида <c>userName=...;password=***</c>.</returns>
    public override string ToString() => $"userName={UserName};password=***";
}

/// <summary>
/// Ответ конечной точки <c>POST /connect/token</c> — выданный набор токенов OAuth 2.0.
/// </summary>
/// <remarks>
/// <para>
/// Сервер KNMDB выдаёт токены по стандарту OAuth 2.0 (OpenIddict). Поля соответствуют
/// разделу ответа Token Endpoint (RFC 6749, §5.1).
/// </para>
/// <para>
/// Все поля приходят в <c>snake_case</c>; в SDK они отображены в свойства C# через атрибуты
/// <see cref="JsonPropertyNameAttribute"/>.
/// </para>
/// </remarks>
public sealed class TokenResponse
{
    /// <summary>
    /// Токен доступа (JWT), который нужно передавать в заголовке
    /// <c>Authorization: Bearer {access_token}</c> при вызове всех защищённых методов API.
    /// </summary>
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    /// <summary>
    /// Тип токена. Для KNMDB всегда <c>Bearer</c> (схема авторизации).
    /// </summary>
    [JsonPropertyName("token_type")]
    public string? TokenType { get; set; }

    /// <summary>
    /// Время жизни токена доступа в секундах (стандарт OAuth 2.0).
    /// </summary>
    /// <remarks>
    /// В описании сервера ошибочно указано «expires in minutes», фактически это секунды,
    /// как того требует RFC 6749. SDK всегда трактует значение как секунды.
    /// </remarks>
    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    /// <summary>
    /// Токен обновления. Позволяет получить новый токен доступа без повторной передачи логина и пароля
    /// (grant_type=<c>refresh_token</c>). Как правило живёт существенно дольше токена доступа.
    /// </summary>
    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    /// <summary>
    /// Выданные области доступа (scopes), разделённые пробелом.
    /// </summary>
    /// <remarks>
    /// Для работы с Track and Trace требуется область <c>api</c> и, как правило,
    /// <c>offline_access</c> — последняя нужна для получения <see cref="RefreshToken"/>.
    /// </remarks>
    [JsonPropertyName("scope")]
    public string? Scope { get; set; }

    /// <summary>
    /// Токен идентификации (OpenID Connect). Для API Track and Trace не используется.
    /// </summary>
    [JsonPropertyName("id_token")]
    public string? IdToken { get; set; }

    /// <summary>
    /// Признак наличия токена доступа.
    /// </summary>
    [JsonIgnore]
    public bool HasAccessToken => !string.IsNullOrWhiteSpace(AccessToken);

    /// <summary>
    /// Момент истечения токена доступа.
    /// </summary>
    /// <param name="utcNow">Текущее время в UTC (для тестируемости). Если не задано — берётся <see cref="DateTimeOffset.UtcNow"/>.</param>
    /// <returns>Абсолютное время истечения токена доступа.</returns>
    public DateTimeOffset GetExpiresAt(DateTimeOffset? utcNow = null)
        => (utcNow ?? DateTimeOffset.UtcNow).AddSeconds(ExpiresIn);

    /// <summary>
    /// Разбирает список областей доступа в массив строк.
    /// </summary>
    /// <returns>Массив областей доступа; пустой массив, если область не задана.</returns>
    public string[] GetScopes()
        => string.IsNullOrWhiteSpace(Scope)
            ? []
            : Scope.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
