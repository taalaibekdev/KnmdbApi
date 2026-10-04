using System.Text.Json.Serialization;

namespace Knmdb.TrackAndTrace.Models.Api;

/// <summary>
/// Описание проблемы в формате RFC 7807 (Problem Details for HTTP APIs).
/// </summary>
/// <remarks>
/// <para>
/// KNMDB API возвращает объект этой формы для ошибок уровня ASP.NET Core: <c>404 Not Found</c>,
/// <c>400 Bad Request</c>, <c>500 Internal Server Error</c> и т. п.
/// </para>
/// <para>
/// Точная копия <c>Microsoft.AspNetCore.Mvc.ProblemDetails</c> — сделана собственной,
/// чтобы базовый SDK не тянул зависимость на ASP.NET Core и работал в мобильных и desktop-приложениях.
/// </para>
/// <para>
/// Дополнительные нестандартные поля сервера попадают в <see cref="Extensions"/>.
/// </para>
/// </remarks>
public sealed class ApiProblemDetails
{
    /// <summary>
    /// URI-идентификатор типа проблемы (ссылка на описание класса ошибки).
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>
    /// Краткое человекочитаемое название проблемы (например, <c>Not Found</c>).
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>
    /// HTTP-код ответа, продублированный в теле (например, <c>404</c>).
    /// </summary>
    [JsonPropertyName("status")]
    public int? Status { get; set; }

    /// <summary>
    /// Подробное описание конкретной ошибки (например, «Упаковка с указанным QR-кодом не найдена»).
    /// </summary>
    [JsonPropertyName("detail")]
    public string? Detail { get; set; }

    /// <summary>
    /// URI конкретного экземпляра проблемы (обычно путь запроса).
    /// </summary>
    [JsonPropertyName("instance")]
    public string? Instance { get; set; }

    /// <summary>
    /// Дополнительные (нестандартные) поля, добавленные сервером.
    /// </summary>
    public IDictionary<string, object?>? Extensions { get; set; }

    /// <summary>
    /// Наиболее информативное текстовое описание проблемы из доступных полей.
    /// </summary>
    /// <returns><see cref="Detail"/>, при его отсутствии — <see cref="Title"/>, иначе <see langword="null"/>.</returns>
    public string? GetMessage() => Detail ?? Title;

    /// <inheritdoc />
    public override string ToString()
        => string.IsNullOrWhiteSpace(GetMessage())
            ? $"HTTP {Status?.ToString() ?? "?"}"
            : $"HTTP {Status?.ToString() ?? "?"}: {GetMessage()}";
}
