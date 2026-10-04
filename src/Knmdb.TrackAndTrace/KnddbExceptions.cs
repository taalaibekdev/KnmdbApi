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
    /// Код результата (<c>resultCode</c>) из конверта ответа KNMDB.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Ключевое свойство для ошибок бизнес-логики.</b> Сервер KNMDB отвечает
    /// конвертом <c>{ resultCode, resultMessage, actionResult }</c> и при ошибке
    /// бизнес-логики оставляет HTTP-статус <c>200 OK</c>. Отличить успех от ошибки
    /// по <see cref="StatusCode"/> невозможно — проверяйте это свойство.
    /// </para>
    /// <para>
    /// Значение <c>null</c> означает, что конверта в ответе не было: ошибку вернул
    /// уровень ASP.NET Core (проверка модели, отсутствие прав, необработанное
    /// исключение) — тогда ориентируйтесь на <see cref="StatusCode"/> и
    /// <see cref="ProblemDetails"/>.
    /// </para>
    /// <para>
    /// Расшифровка известных кодов — в <see cref="KnddbResultCodes"/>.
    /// </para>
    /// </remarks>
    public int? ResultCode { get; init; }

    /// <summary>
    /// Сообщение сервера (<c>resultMessage</c>) из конверта ответа.
    /// </summary>
    /// <remarks>
    /// Как правило на английском языке, например
    /// <c>Product with QRCode 0104… not found</c>. Готовое русское пояснение
    /// возвращает <see cref="GetResultDescription"/>.
    /// </remarks>
    public string? ResultMessage { get; init; }

    /// <summary>
    /// Признак ошибки бизнес-логики: сервер вернул конверт с ненулевым
    /// <see cref="ResultCode"/>.
    /// </summary>
    /// <remarks>
    /// Такие ошибки приходят с HTTP-статусом 200, поэтому проверка
    /// <see cref="StatusCode"/> их не выявляет.
    /// </remarks>
    public bool IsBusinessError => ResultCode is not null and not KnddbResultCodes.Success;

    /// <summary>
    /// Возвращает пояснение причины на русском языке.
    /// </summary>
    /// <returns>
    /// Расшифровку <see cref="ResultCode"/> из <see cref="KnddbResultCodes.Describe"/>,
    /// либо <see cref="ResultMessage"/> от сервера, либо <see langword="null"/>.
    /// </returns>
    public string? GetResultDescription()
        => KnddbResultCodes.Describe(ResultCode) ?? ResultMessage;

    /// <summary>
    /// Признак того, что упаковка/сущность не найдена (HTTP 404).
    /// </summary>
    public bool IsNotFound => StatusCode == HttpStatusCode.NotFound;

    /// <summary>
    /// Признак того, что упаковка не найдена — по HTTP-статусу (404) либо
    /// по коду результата <see cref="KnddbResultCodes.ProductWithQrCodeNotFound"/>
    /// или <see cref="KnddbResultCodes.ProductWithSerialNumberNotFound"/>.
    /// </summary>
    /// <remarks>
    /// Удобно для проверки лекарственного средства и для операций с упаковками:
    /// сервер сообщает «не найдено» и через HTTP 404, и через код 6022/6039
    /// в конверте с HTTP 200.
    /// </remarks>
    public bool IsProductNotFound
        => IsNotFound
           || ResultCode == KnddbResultCodes.ProductWithQrCodeNotFound
           || ResultCode == KnddbResultCodes.ProductWithSerialNumberNotFound;

    /// <summary>
    /// Признак того, что упаковка не подходит для продажи
    /// (<see cref="KnddbResultCodes.ProductWithQrCodeNotSuitableForSale"/>).
    /// </summary>
    /// <remarks>
    /// Самая частая причина — повторная продажа уже проданной упаковки.
    /// </remarks>
    public bool IsProductNotSuitableForSale
        => ResultCode == KnddbResultCodes.ProductWithQrCodeNotSuitableForSale;

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
        var builder = new System.Text.StringBuilder(base.ToString());

        if (ResultCode is not null)
        {
            builder.Append(Environment.NewLine).Append("resultCode: ").Append(ResultCode.Value);

            var description = GetResultDescription();
            if (!string.IsNullOrWhiteSpace(description))
            {
                builder.Append(" — ").Append(description);
            }
        }

        if (TraceId is not null)
        {
            builder.Append(Environment.NewLine).Append("TraceId: ").Append(TraceId);
        }

        if (Errors is { Count: > 0 })
        {
            builder.Append(Environment.NewLine).Append("Ошибки валидации:");
            foreach (var pair in Errors)
            {
                builder.Append(Environment.NewLine)
                    .Append("  - ").Append(pair.Key).Append(": ").Append(pair.Value);
            }
        }

        return builder.ToString();
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
