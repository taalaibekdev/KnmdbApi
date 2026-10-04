using System.Net;
using System.Text;
using System.Text.Json;
using Knmdb.TrackAndTrace.Serialization;

namespace Knmdb.TrackAndTrace.Tests.Infrastructure;

/// <summary>
/// Обработчик HTTP, который отдаёт заранее подготовленные ответы и записывает все запросы.
/// </summary>
/// <remarks>
/// Позволяет тестировать SDK целиком — от формирования запроса до разбора ответа —
/// без реального сервера KNMDB.
/// </remarks>
internal sealed class RecordingHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, int, HttpResponseMessage> _responder;

    public RecordingHttpMessageHandler(Func<HttpRequestMessage, int, HttpResponseMessage> responder)
        => _responder = responder;

    /// <summary>Все запросы в порядке отправки (с уже прочитанным телом).</summary>
    public List<RecordedRequest> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var index = Requests.Count;

        string? body = null;
        if (request.Content is not null)
        {
            body = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }

        Requests.Add(new RecordedRequest
        {
            Method = request.Method,
            RequestUri = request.RequestUri,
            Authorization = request.Headers.Authorization?.ToString(),
            Body = body,
            ContentType = request.Content?.Headers.ContentType?.ToString(),
            UserAgent = request.Headers.UserAgent.ToString(),
        });

        return _responder(request, index);
    }

    public static HttpResponseMessage Json(HttpStatusCode statusCode, string json, string contentType = "application/json")
        => new(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, contentType),
        };

    /// <summary>
    /// Отдаёт ответ, тело которого получается сериализацией объекта.
    /// </summary>
    public static HttpResponseMessage FromObject<T>(HttpStatusCode statusCode, T value)
        => Json(statusCode, JsonSerializer.Serialize(value, KnddbJson.DefaultOptions));

    /// <summary>
    /// Отдаёт успешный ответ в конверте KNMDB:
    /// <c>{ "resultCode": 0, "resultMessage": "...", "actionResult": {...} }</c>.
    /// </summary>
    /// <param name="payload">
    /// Полезная нагрузка метода в виде готового JSON-фрагмента, например
    /// <c>"""{ "numberOfStakeholders": 0 }"""</c>.
    /// </param>
    /// <remarks>
    /// <para>
    /// Именно так отвечает настоящий сервер: фильтр <c>ActionResultFilterAttribute</c>
    /// оборачивает результат каждого метода Track and Trace в этот конверт.
    /// </para>
    /// <para>
    /// Принимается строка, а не объект: сериализация идёт через источник-генератор
    /// <c>KnddbJsonContext</c>, которому анонимные типы недоступны.
    /// </para>
    /// </remarks>
    public static HttpResponseMessage Envelope(string payload)
        => Json(
            HttpStatusCode.OK,
            $$"""
            {
              "resultCode": 0,
              "resultMessage": "Action completed successfully.",
              "actionResult": {{payload}}
            }
            """);

    /// <summary>
    /// Отдаёт ошибку бизнес-логики в конверте с HTTP-статусом 200.
    /// </summary>
    /// <remarks>
    /// Настоящий сервер так отвечает на <c>KNDDBBusinessException</c>: статус
    /// остаётся 200, а причина передаётся кодом <paramref name="resultCode"/>.
    /// </remarks>
    public static HttpResponseMessage EnvelopeError(int resultCode, string resultMessage)
        => Json(
            HttpStatusCode.OK,
            $$"""
            {
              "resultCode": {{resultCode}},
              "resultMessage": {{JsonSerializer.Serialize(resultMessage)}},
              "actionResult": null
            }
            """);

    public static HttpResponseMessage Empty(HttpStatusCode statusCode) => new(statusCode);
}

/// <summary>Снимок отправленного запроса.</summary>
internal sealed class RecordedRequest
{
    public required HttpMethod Method { get; init; }

    public Uri? RequestUri { get; init; }

    public string? Authorization { get; init; }

    public string? Body { get; init; }

    public string? ContentType { get; init; }

    /// <summary>Заголовок <c>User-Agent</c>.</summary>
    public string? UserAgent { get; init; }

    public string Path => RequestUri?.AbsolutePath ?? string.Empty;

    public string? Query => RequestUri?.Query;

    public JsonDocument ParseBody() => JsonDocument.Parse(Body ?? "{}");
}
