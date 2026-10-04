using System.Collections.Concurrent;
using Knmdb.TrackAndTrace.Models.Auth;

namespace Knmdb.TrackAndTrace;

/// <summary>
/// Состояние входа одного пользователя KNMDB: учётные данные, токены и срок их действия.
/// </summary>
/// <remarks>
/// <para>
/// Каждая сессия полностью независима, поэтому один экземпляр SDK (и один <see cref="HttpClient"/>)
/// безопасно обслуживает нескольких пользователей сразу.
/// </para>
/// <para>Экземпляры создаются методом <c>SignInAsync</c>; вручную их создавать не требуется.</para>
/// </remarks>
public sealed class KnddbSession
{
    /// <summary>Ключ сессии по умолчанию: <c>default</c>.</summary>
    public const string DefaultKey = "default";

    /// <summary>
    /// Семафор, сериализующий обновление токенов этой сессии.
    /// </summary>
    internal SemaphoreSlim Gate { get; } = new(1, 1);

    /// <summary>
    /// Ключ сессии — идентификатор пользователя внутри SDK.
    /// </summary>
    /// <remarks>Для нескольких пользователей используйте ключи вида <c>user:{login}</c>.</remarks>
    public required string Key { get; init; }

    /// <summary>
    /// Логин пользователя.
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// Учётные данные для автоматического получения нового токена.
    /// </summary>
    /// <remarks>
    /// Хранятся только в памяти процесса и не сохраняются на диск.
    /// Если приложение не должно держать пароль в памяти, вызовите
    /// <c>SignInAsync(..., storeCredentials: false)</c>.
    /// </remarks>
    public KnddbCredentials? Credentials { get; set; }

    /// <summary>
    /// Токен доступа (JWT) для заголовка <c>Authorization: Bearer ...</c>.
    /// </summary>
    public string? AccessToken { get; set; }

    /// <summary>
    /// Токен обновления, позволяющий получить новый токен доступа без ввода пароля.
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// Тип токена. Для KNMDB — <c>Bearer</c>.
    /// </summary>
    public string TokenType { get; set; } = "Bearer";

    /// <summary>
    /// Момент истечения токена доступа (UTC).
    /// </summary>
    public DateTimeOffset AccessTokenExpiresAt { get; set; }

    /// <summary>
    /// Момент последнего успешного получения или обновления токена (UTC).
    /// </summary>
    public DateTimeOffset LastRefreshedAt { get; set; }

    /// <summary>
    /// Области доступа, выданные сервером (значение поля <c>scope</c>).
    /// </summary>
    public string? Scope { get; set; }

    /// <summary>
    /// Признак того, что пользователь вошёл в систему.
    /// </summary>
    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(AccessToken);

    /// <summary>
    /// Проверяет, можно ли использовать токен доступа.
    /// </summary>
    /// <param name="margin">Запас времени до истечения, при котором токен уже считается истекающим.</param>
    /// <param name="utcNow">Текущее время UTC (для тестируемости).</param>
    /// <returns><see langword="true"/>, если токен действителен.</returns>
    public bool IsAccessTokenValid(TimeSpan? margin = null, DateTimeOffset? utcNow = null)
        => IsAuthenticated
           && AccessTokenExpiresAt > (utcNow ?? DateTimeOffset.UtcNow) + (margin ?? TimeSpan.Zero);

    /// <summary>
    /// Применяет к сессии ответ конечной точки <c>/connect/token</c>.
    /// </summary>
    /// <param name="response">Ответ сервера с токенами.</param>
    /// <param name="utcNow">Текущее время UTC (для тестируемости).</param>
    /// <exception cref="ArgumentNullException">Ответ не задан.</exception>
    /// <exception cref="KnddbAuthenticationException">Сервер не вернул токен доступа.</exception>
    public void ApplyTokenResponse(TokenResponse response, DateTimeOffset? utcNow = null)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (!response.HasAccessToken)
        {
            throw new KnddbAuthenticationException(
                "Сервер авторизации KNMDB не вернул поле access_token.")
            {
                SessionKey = Key,
            };
        }

        var now = utcNow ?? DateTimeOffset.UtcNow;

        AccessToken = response.AccessToken;
        TokenType = string.IsNullOrWhiteSpace(response.TokenType) ? "Bearer" : response.TokenType!;

        // expires_in приходит в секундах. Если сервер не прислал значение,
        // считаем токен короткоживущим, чтобы не работать с «вечным» токеном.
        AccessTokenExpiresAt = now.AddSeconds(response.ExpiresIn > 0 ? response.ExpiresIn : 300);

        // Токен обновления приходит не всегда: тогда сохраняем ранее полученный.
        if (!string.IsNullOrWhiteSpace(response.RefreshToken))
        {
            RefreshToken = response.RefreshToken;
        }

        if (!string.IsNullOrWhiteSpace(response.Scope))
        {
            Scope = response.Scope;
        }

        LastRefreshedAt = now;
    }

    /// <summary>
    /// Сбрасывает токены, сохраняя ключ, логин и учётные данные.
    /// </summary>
    public void ClearTokens()
    {
        AccessToken = null;
        RefreshToken = null;
        AccessTokenExpiresAt = default;
        Scope = null;
    }

    /// <inheritdoc />
    public override string ToString()
        => $"KnddbSession(Key='{Key}', UserName='{UserName}', " +
           $"Authenticated={IsAuthenticated}, ExpiresAt={AccessTokenExpiresAt:u})";
}

/// <summary>
/// Хранилище сессий: состояния входа по ключу пользователя.
/// </summary>
/// <remarks>
/// Стандартная реализация — <see cref="InMemoryKnddbSessionStore"/>: сессии живут в памяти процесса.
/// Своя реализация нужна, только если сессии должны быть общими для нескольких экземпляров приложения.
/// </remarks>
public interface IKnddbSessionStore
{
    /// <summary>Возвращает сессию по ключу или <see langword="null"/>.</summary>
    /// <param name="key">Ключ сессии.</param>
    /// <returns>Сессия либо <see langword="null"/>.</returns>
    KnddbSession? Get(string key);

    /// <summary>Возвращает существующую сессию по ключу или создаёт новую.</summary>
    /// <param name="key">Ключ сессии.</param>
    /// <param name="userName">Логин пользователя (необязательно).</param>
    /// <returns>Сессия.</returns>
    KnddbSession GetOrCreate(string key, string? userName = null);

    /// <summary>Сохраняет сессию в хранилище.</summary>
    /// <param name="session">Сессия.</param>
    void Set(KnddbSession session);

    /// <summary>Удаляет сессию по ключу.</summary>
    /// <param name="key">Ключ сессии.</param>
    /// <returns><see langword="true"/>, если сессия существовала.</returns>
    bool Remove(string key);

    /// <summary>Возвращает снимок всех сессий.</summary>
    /// <returns>Коллекция сессий.</returns>
    IReadOnlyCollection<KnddbSession> GetAll();
}

/// <summary>
/// Потокобезопасное хранилище сессий в памяти процесса.
/// </summary>
/// <remarks>
/// Регистрируется как singleton. Сессии не сохраняются на диск: после перезапуска приложения
/// потребуется повторный вход.
/// </remarks>
public sealed class InMemoryKnddbSessionStore : IKnddbSessionStore
{
    private readonly ConcurrentDictionary<string, KnddbSession> _sessions = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public KnddbSession? Get(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _sessions.TryGetValue(key, out var session) ? session : null;
    }

    /// <inheritdoc />
    public KnddbSession GetOrCreate(string key, string? userName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return _sessions.GetOrAdd(
            key,
            static (k, name) => new KnddbSession { Key = k, UserName = name },
            userName);
    }

    /// <inheritdoc />
    public void Set(KnddbSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _sessions[session.Key] = session;
    }

    /// <inheritdoc />
    public bool Remove(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _sessions.TryRemove(key, out _);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<KnddbSession> GetAll() => _sessions.Values.ToArray();
}

/// <summary>
/// Хранилище учётных данных, позволяющее переключать пользователя без повторного ввода пароля.
/// </summary>
/// <remarks>
/// Стандартная реализация — <see cref="InMemoryKnddbCredentialStore"/>: пароли хранятся
/// только в оперативной памяти процесса.
/// </remarks>
public interface IKnddbCredentialStore
{
    /// <summary>Сохраняет учётные данные для ключа сессии.</summary>
    /// <param name="sessionKey">Ключ сессии.</param>
    /// <param name="credentials">Учётные данные.</param>
    void Set(string sessionKey, KnddbCredentials credentials);

    /// <summary>Возвращает сохранённые учётные данные.</summary>
    /// <param name="sessionKey">Ключ сессии.</param>
    /// <returns>Учётные данные либо <see langword="null"/>.</returns>
    KnddbCredentials? Get(string sessionKey);

    /// <summary>Удаляет учётные данные.</summary>
    /// <param name="sessionKey">Ключ сессии.</param>
    /// <returns><see langword="true"/>, если данные существовали.</returns>
    bool Remove(string sessionKey);
}

/// <summary>
/// Потокобезопасное хранилище учётных данных в памяти процесса.
/// </summary>
public sealed class InMemoryKnddbCredentialStore : IKnddbCredentialStore
{
    private readonly ConcurrentDictionary<string, KnddbCredentials> _credentials = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public void Set(string sessionKey, KnddbCredentials credentials)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionKey);
        ArgumentNullException.ThrowIfNull(credentials);
        _credentials[sessionKey] = credentials;
    }

    /// <inheritdoc />
    public KnddbCredentials? Get(string sessionKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionKey);
        return _credentials.TryGetValue(sessionKey, out var credentials) ? credentials : null;
    }

    /// <inheritdoc />
    public bool Remove(string sessionKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionKey);
        return _credentials.TryRemove(sessionKey, out _);
    }
}
