using System.Net;
using Knmdb.TrackAndTrace.Models.Api;

namespace Knmdb.TrackAndTrace;

/// <summary>
/// Исключение, которое SDK выбрасывает, когда API KNMDB вернул неуспешный HTTP-статус.
/// </summary>
/// <remarks>
/// <para>
/// Содержит всё необходимое для диагностики: HTTP-статус, «сырое» тело ответа, разобранный
/// <c>ProblemDetails</c> и идентификатор запроса <c>traceId</c> (если сервер его вернул).
/// </para>
/// <para>
/// Коллекция <see cref="Errors"/> заполняется, если сервер вернул ошибки валидации модели
/// (400 Bad Request со списком полей).
/// </para>
/// </remarks>
public class KnddbApiException : Exception
{
    /// <summary>
    /// Создаёт исключение.
    /// </summary>
    public KnddbApiException()
    {
    }

    /// <summary>
    /// Создаёт исключение с сообщением.
    /// </summary>
    /// <param name="message">Текст ошибки.</param>
    public KnddbApiException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Создаёт исключение с сообщением и внутренним исключением.
    /// </summary>
    /// <param name="message">Текст ошибки.</param>
    /// <param name="innerException">Внутреннее исключение.</param>
    public KnddbApiException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// HTTP-метод запроса (<c>GET</c>, <c>POST</c> и т. д.).
    /// </summary>
    public string? Method { get; init; }

    /// <summary>
    /// Относительный путь запроса, например <c>/api/TrackAndTrace/GetAllStakeholders</c>.
    /// </summary>
    public string? RequestUri { get; init; }

    /// <summary>
    /// HTTP-статус ответа.
    /// </summary>
    public HttpStatusCode? StatusCode { get; init; }

    /// <summary>
    /// «Сырое» тело ответа сервера (может быть усечено при логировании, но здесь хранится целиком).
    /// </summary>
    public string? ResponseBody { get; init; }

    /// <summary>
    /// Разобранное описание проблемы в формате RFC 7807, если сервер его вернул.
    /// </summary>
    public ApiProblemDetails? ProblemDetails { get; init; }

    /// <summary>
    /// Идентификатор запроса для обращения в поддержку (<c>traceId</c> / <c>requestId</c>).
    /// </summary>
    public string? TraceId { get; init; }

    /// <summary>
    /// Ошибки валидации модели: имя поля → текст ошибки.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Errors { get; init; }

    /// <summary>
    /// Признак того, что упаковка/сущность не найдена (HTTP 404).
    /// </summary>
    public bool IsNotFound => StatusCode == HttpStatusCode.NotFound;

    /// <summary>
    /// Признак проблемы с авторизацией: HTTP 401 или 403.
    /// </summary>
    public bool IsAuthenticationFailure
        => StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;

    /// <summary>
    /// Признак отсутствия прав на операцию (HTTP 403) — у пользователя нет требуемой роли API.
    /// </summary>
    public bool IsForbidden => StatusCode == HttpStatusCode.Forbidden;

    /// <summary>
    /// Признак ошибки входных данных (HTTP 400).
    /// </summary>
    public bool IsBadRequest => StatusCode == HttpStatusCode.BadRequest;

    /// <summary>
    /// Признак временной ошибки сервера (HTTP 5xx), при которой имеет смысл повторить запрос.
    /// </summary>
    public bool IsTransientFailure
        => StatusCode is null || (int)StatusCode >= 500 || StatusCode == HttpStatusCode.RequestTimeout;

    /// <inheritdoc />
    public override string ToString()
    {
        var baseText = base.ToString();
        if (Errors is null || Errors.Count == 0)
        {
            return TraceId is null ? baseText : $"{baseText}{Environment.NewLine}TraceId: {TraceId}";
        }

        var details = string.Join(Environment.NewLine, Errors.Select(e => $"  - {e.Key}: {e.Value}"));
        return $"{baseText}{Environment.NewLine}Ошибки валидации:{Environment.NewLine}{details}";
    }
}

/// <summary>
/// Исключение, возникающее при неудачной аутентификации: неверный логин/пароль,
/// истёкший и не подлежащий обновлению токен, отсутствие активной сессии.
/// </summary>
/// <remarks>
/// Наследуется от <see cref="KnddbApiException"/>, поэтому существующие обработчики
/// <see cref="KnddbApiException"/> продолжат работать.
/// </remarks>
public sealed class KnddbAuthenticationException : KnddbApiException
{
    /// <summary>
    /// Создаёт исключение с сообщением.
    /// </summary>
    /// <param name="message">Текст ошибки.</param>
    public KnddbAuthenticationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Создаёт исключение с сообщением и внутренним исключением.
    /// </summary>
    /// <param name="message">Текст ошибки.</param>
    /// <param name="innerException">Внутреннее исключение.</param>
    public KnddbAuthenticationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Описание ошибки OAuth 2.0, полученное от сервера
    /// (например, <c>invalid_grant</c>, <c>invalid_client</c>).
    /// </summary>
    public string? OAuthError { get; init; }

    /// <summary>
    /// Ключ сессии, для которой не удалось выполнить аутентификацию.
    /// </summary>
    public string? SessionKey { get; init; }
}

/// <summary>
/// Исключение конфигурации SDK: не задан адрес сервера, отсутствуют учётные данные,
/// для защищённого метода нет активной сессии.
/// </summary>
/// <remarks>
/// Возникает до обращения к сети, поэтому его не нужно повторять — требуется исправить настройки
/// или сначала выполнить вход (<c>SignInAsync</c>).
/// </remarks>
public sealed class KnddbConfigurationException : KnddbApiException
{
    /// <summary>
    /// Создаёт исключение с сообщением.
    /// </summary>
    /// <param name="message">Текст ошибки.</param>
    public KnddbConfigurationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Создаёт исключение с сообщением и внутренним исключением.
    /// </summary>
    /// <param name="message">Текст ошибки.</param>
    /// <param name="innerException">Внутреннее исключение.</param>
    public KnddbConfigurationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
