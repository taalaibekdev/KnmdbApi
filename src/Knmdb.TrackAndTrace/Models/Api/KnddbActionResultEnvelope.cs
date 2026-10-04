using System.Text.Json.Serialization;

namespace Knmdb.TrackAndTrace.Models.Api;

/// <summary>
/// Конверт ответа сервера KNMDB: <c>{ resultCode, resultMessage, actionResult }</c>.
/// </summary>
/// <remarks>
/// <para>
/// Сервер оборачивает в этот конверт **все** ответы методов Track and Trace —
/// фильтр <c>ActionResultFilterAttribute</c> заменяет результат действия
/// на <c>Ok(KNDDBActionResult)</c>. Поэтому результат метода лежит не в корне
/// JSON, а в поле <see cref="ActionResult"/>.
/// </para>
/// <para>
/// <b>Ошибки бизнес-логики тоже приходят в конверте и с HTTP-статусом 200</b>:
/// обработчик исключений сервера сериализует <c>KNDDBBusinessException</c>
/// в этот же конверт, оставляя статус <c>200 OK</c>. Отличить успех от ошибки
/// можно только по <see cref="ResultCode"/>:
/// </para>
/// <list type="table">
///   <item>
///     <term><c>0</c></term>
///     <description>успех, данные в <see cref="ActionResult"/>.</description>
///   </item>
///   <item>
///     <term><c>1</c></term>
///     <description>ошибка валидации входных данных.</description>
///   </item>
///   <item>
///     <term><c>2</c></term>
///     <description>неожиданная ошибка сервера.</description>
///   </item>
///   <item>
///     <term><c>6000…6106</c></term>
///     <description>ошибки бизнес-логики Track and Trace — см. <see cref="KnddbResultCodes"/>.</description>
///   </item>
/// </list>
/// <para>
/// SDK разбирает конверт автоматически: при <c>resultCode = 0</c> возвращает
/// содержимое <see cref="ActionResult"/>, иначе выбрасывает
/// <see cref="KnddbApiException"/> с заполненными <c>ResultCode</c>
/// и <c>ResultMessage</c>. Разбирать конверт вручную не нужно.
/// </para>
/// </remarks>
public sealed class KnddbActionResultEnvelope
{
    /// <summary>
    /// Код результата: <c>0</c> — успех, любое другое значение — ошибка.
    /// </summary>
    [JsonPropertyName("resultCode")]
    public int ResultCode { get; set; }

    /// <summary>
    /// Сообщение сервера о результате. При ошибке содержит описание причины
    /// (как правило на английском языке).
    /// </summary>
    [JsonPropertyName("resultMessage")]
    public string? ResultMessage { get; set; }

    /// <summary>
    /// Полезная нагрузка метода. <see langword="null"/> при ошибке.
    /// </summary>
    [JsonPropertyName("actionResult")]
    public System.Text.Json.JsonElement? ActionResult { get; set; }
}
