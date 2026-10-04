using System.Text.Json.Serialization;
using Knmdb.TrackAndTrace.Serialization;

namespace Knmdb.TrackAndTrace.Models.Requests;

/// <summary>
/// Запрос <c>POST /api/TrackAndTrace/ProductInquiryQRCode</c> — проверка лекарственного средства
/// по QR-коду (Data Matrix).
/// </summary>
/// <remarks>
/// <para>
/// Метод доступен <b>без авторизации</b> (<c>AllowAnonymous</c>) и предназначен для мобильных
/// приложений потребителей: пользователь сканирует код и получает сведения об упаковке.
/// </para>
/// <para>
/// Если упаковка не найдена, сервер отвечает <c>404 Not Found</c> с телом <c>ProblemDetails</c>.
/// </para>
/// </remarks>
public sealed class ProductInquiryWithQrCodeRequest
{
    /// <summary>
    /// QR-код (содержимое Data Matrix), считанный с упаковки.
    /// </summary>
    /// <remarks>
    /// Строка должна полностью совпадать с зарегистрированным кодом, включая идентификаторы
    /// применения (AI) — например, <c>01{GTIN}21{Serial}17{YYMMDD}10{Batch}</c>.
    /// </remarks>
    [JsonPropertyName("qrCode")]
    public string? QrCode { get; set; }
}

/// <summary>
/// Запрос <c>POST /api/TrackAndTrace/ProductInquiryGtinSn</c> — проверка лекарственного средства
/// по паре «GTIN + серийный номер».
/// </summary>
/// <remarks>
/// <para>
/// Альтернатива сканированию QR-кода: используется, когда GTIN и серийный номер известны
/// по отдельности (например, введены вручную или получены из другого источника).
/// </para>
/// <para>Метод доступен <b>без авторизации</b>.</para>
/// </remarks>
public sealed class ProductInquiryWithGtinSnRequest
{
    /// <summary>
    /// GTIN препарата (14 знаков).
    /// </summary>
    /// <remarks>Обязательное поле для успешного поиска.</remarks>
    [JsonPropertyName("gtin")]
    public string? Gtin { get; set; }

    /// <summary>
    /// Серийный номер упаковки (до 18 знаков).
    /// </summary>
    /// <remarks>Обязательное поле для успешного поиска.</remarks>
    [JsonPropertyName("serialNumber")]
    public string? SerialNumber { get; set; }
}

/// <summary>
/// Запрос <c>POST /api/TrackAndTrace/GetStockInheldListByGtin</c> — остатки на складе
/// по конкретному GTIN.
/// </summary>
/// <remarks>Требуется роль <c>ApiGetStockInheldListByGtin</c>.</remarks>
public sealed class GetStockInheldListByGtinRequest
{
    /// <summary>
    /// GTIN препарата, по которому запрашиваются остатки.
    /// </summary>
    [JsonPropertyName("gtin")]
    public string? Gtin { get; set; }
}

/// <summary>
/// Запрос <c>POST /api/TrackAndTrace/GetPartialSaleInfo</c> — сведения о частично проданной упаковке.
/// </summary>
/// <remarks>
/// <para>
/// Позволяет узнать, сколько препарата осталось в упаковке после частичной продажи,
/// чтобы корректно оформить следующую продажу или списание остатка.
/// </para>
/// <para>Требуется роль <c>ApiGetPartialSaleInfo</c>.</para>
/// </remarks>
public sealed class GetPartialSaleInfoRequest
{
    /// <summary>
    /// QR-код (содержимое Data Matrix) упаковки.
    /// </summary>
    [JsonPropertyName("qrCode")]
    public string? QrCode { get; set; }
}

/// <summary>
/// Запрос <c>POST /api/TrackAndTrace/GetTransferListByFilter</c> — поиск деклараций перемещения
/// по набору необязательных фильтров.
/// </summary>
/// <remarks>
/// <para>
/// Все поля необязательны. Незаполненные фильтры не участвуют в отборе.
/// Если не задан ни один фильтр, сервер вернёт список доступных пользователю перемещений.
/// </para>
/// <para>Требуется роль <c>ApiGetTransferListByFilter</c>.</para>
/// </remarks>
public sealed class GetTransferListByFilterRequest
{
    /// <summary>
    /// Идентификатор декларации перемещения — точный поиск одной записи.
    /// </summary>
    [JsonPropertyName("declarationId")]
    public long? DeclarationId { get; set; }

    /// <summary>
    /// Номер сопроводительного документа.
    /// </summary>
    /// <remarks>Необязательный фильтр.</remarks>
    [JsonPropertyName("documentNo")]
    public string? DocumentNo { get; set; }

    /// <summary>
    /// Дата сопроводительного документа.
    /// </summary>
    /// <remarks>Необязательный фильтр. Сериализуется в строку формата <c>yyyy-MM-dd</c>.</remarks>
    [JsonPropertyName("documentDate")]
    [JsonConverter(typeof(NullableDateOnlyJsonConverter))]
    public DateTimeOffset? DocumentDate { get; set; }

    /// <summary>
    /// Начало диапазона дат декларации (включительно).
    /// </summary>
    /// <remarks>Необязательный фильтр. Сериализуется в строку формата <c>yyyy-MM-dd</c>.</remarks>
    [JsonPropertyName("declarationDateFrom")]
    [JsonConverter(typeof(NullableDateOnlyJsonConverter))]
    public DateTimeOffset? DeclarationDateFrom { get; set; }

    /// <summary>
    /// Конец диапазона дат декларации (включительно).
    /// </summary>
    /// <remarks>Необязательный фильтр. Сериализуется в строку формата <c>yyyy-MM-dd</c>.</remarks>
    [JsonPropertyName("declarationDateTo")]
    [JsonConverter(typeof(NullableDateOnlyJsonConverter))]
    public DateTimeOffset? DeclarationDateTo { get; set; }

    /// <summary>
    /// Состояние перемещения.
    /// </summary>
    /// <remarks>Необязательный фильтр. См. <c>TransferState</c>.</remarks>
    [JsonPropertyName("currentState")]
    public Enums.TransferState? CurrentState { get; set; }

    /// <summary>
    /// Роль стейкхолдера в перемещении (отправитель или получатель).
    /// </summary>
    /// <remarks>Необязательный фильтр. См. <c>TransferType</c>.</remarks>
    [JsonPropertyName("transferType")]
    public Enums.TransferType? TransferType { get; set; }

    /// <summary>
    /// Признак возвратного перемещения.
    /// </summary>
    /// <remarks>
    /// Необязательный фильтр: <see langword="true"/> — только возвраты,
    /// <see langword="false"/> — только обычные перемещения, <see langword="null"/> — без фильтра.
    /// </remarks>
    [JsonPropertyName("isReturn")]
    public bool? IsReturn { get; set; }
}

/// <summary>
/// Запрос <c>POST /api/TrackAndTrace/GetMedicineList</c> — список лекарственных средств,
/// изменённых после указанной даты.
/// </summary>
/// <remarks>
/// <para>
/// Используется для инкрементальной синхронизации локального справочника препаратов:
/// в первый раз передайте <see langword="null"/> и получите полный список, далее передавайте
/// максимальную дату <c>lastUpdate</c> из предыдущего ответа.
/// </para>
/// <para>Требуется роль <c>ApiGetMedicineList</c>.</para>
/// </remarks>
public sealed class GetMedicineListRequest
{
    /// <summary>
    /// Нижняя граница даты изменения препарата.
    /// </summary>
    /// <remarks>
    /// Необязательное поле. Сериализуется в формате ISO-8601 (UTC), например <c>2026-02-21T10:30:00Z</c>.
    /// Если не указано — возвращается весь справочник.
    /// </remarks>
    [JsonPropertyName("lastUpdate")]
    public DateTimeOffset? LastUpdate { get; set; }
}
