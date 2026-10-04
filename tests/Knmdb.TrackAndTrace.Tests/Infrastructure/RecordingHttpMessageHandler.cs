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

    public string Path => RequestUri?.AbsolutePath ?? string.Empty;

    public string? Query => RequestUri?.Query;

    public JsonDocument ParseBody() => JsonDocument.Parse(Body ?? "{}");
}
