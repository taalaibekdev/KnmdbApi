using System.Text.Json.Serialization;
using Knmdb.TrackAndTrace.Models.Enums;

namespace Knmdb.TrackAndTrace.Models.Responses;

/// <summary>
/// Результат проверки лекарственного средства — ответ методов
/// <c>POST /api/TrackAndTrace/ProductInquiryQRCode</c> и <c>POST /api/TrackAndTrace/ProductInquiryGtinSn</c>.
/// </summary>
/// <remarks>
/// <para>
/// Оба метода доступны без авторизации и предназначены для приложений потребителей
/// («проверить лекарство»). Ответ содержит полную картину по упаковке: срок годности,
/// текущего держателя, историю движения, признаки приостановки/отзыва и сведения о препарате.
/// </para>
/// <para>
/// Если упаковка не найдена, сервер возвращает <c>404 Not Found</c> с телом <c>ProblemDetails</c>,
/// и SDK выбрасывает <c>KnddbApiException</c> со статусом 404.
/// </para>
/// </remarks>
public sealed class ProductInquiryResult
{
    /// <summary>
    /// Внутренний идентификатор упаковки в системе (<c>TTProductBox.Id</c>).
    /// </summary>
    [JsonPropertyName("productBoxId")]
    public long ProductBoxId { get; set; }

    /// <summary>
    /// Идентификатор позиции упаковки препарата (<c>DrugPackageItem</c>).
    /// </summary>
    [JsonPropertyName("productPackageItemId")]
    public Guid ProductPackageItemId { get; set; }

    /// <summary>
    /// Наименование препарата.
    /// </summary>
    [JsonPropertyName("productName")]
    public string? ProductName { get; set; }

    /// <summary>
    /// GTIN препарата (14 знаков).
    /// </summary>
    [JsonPropertyName("gtin")]
    public string? Gtin { get; set; }

    /// <summary>
    /// Серийный номер упаковки.
    /// </summary>
    [JsonPropertyName("serialNumber")]
    public string? SerialNumber { get; set; }

    /// <summary>
    /// Номер партии упаковки.
    /// </summary>
    [JsonPropertyName("batchNumber")]
    public string? BatchNumber { get; set; }

    /// <summary>
    /// Дата производства упаковки.
    /// </summary>
    [JsonPropertyName("productionDate")]
    public DateTimeOffset ProductionDate { get; set; }

    /// <summary>
    /// Дата истечения срока годности упаковки.
    /// </summary>
    [JsonPropertyName("expirationDate")]
    public DateTimeOffset ExpirationDate { get; set; }

    /// <summary>
    /// QR-код (содержимое Data Matrix) упаковки.
    /// </summary>
    [JsonPropertyName("qrCode")]
    public string? QrCode { get; set; }

    /// <summary>
    /// Наименование организации, у которой упаковка находится в настоящий момент.
    /// </summary>
    [JsonPropertyName("stakeHolderName")]
    public string? StakeHolderName { get; set; }

    /// <summary>
    /// ИНН организации, у которой упаковка находится в настоящий момент.
    /// </summary>
    /// <remarks>Присутствует в фактическом ответе API, хотя отсутствует в опубликованной OpenAPI-схеме.</remarks>
    [JsonPropertyName("stakeholderTaxNumber")]
    public string? StakeholderTaxNumber { get; set; }

    /// <summary>
    /// Признак того, что срок годности упаковки истёк на момент запроса.
    /// </summary>
    [JsonPropertyName("isExpired")]
    public bool IsExpired { get; set; }

    /// <summary>
    /// Признак того, что упаковка доступна для продажи (не выбыла, не приостановлена, не просрочена).
    /// </summary>
    [JsonPropertyName("isAvailableForSale")]
    public bool IsAvailableForSale { get; set; }

    /// <summary>
    /// Признак того, что упаковка приостановлена или отозвана из обращения.
    /// </summary>
    [JsonPropertyName("isSuspendedOrRecalled")]
    public bool IsSuspendedOrRecalled { get; set; }

    /// <summary>
    /// Складской статус упаковки: в наличии, выбыла или в перемещении.
    /// </summary>
    /// <remarks>См. <see cref="ProductStatus"/>.</remarks>
    [JsonPropertyName("productStatus")]
    public ProductStatus ProductStatus { get; set; }

    /// <summary>
    /// Последнее событие, произошедшее с упаковкой.
    /// </summary>
    /// <remarks>См. <see cref="ProductState"/>.</remarks>
    [JsonPropertyName("productState")]
    public ProductState ProductState { get; set; }

    /// <summary>
    /// Сведения о приостановке или отзыве упаковки (если применимо).
    /// </summary>
    [JsonPropertyName("suspendRecallInfo")]
    public SuspendRecallInfo? SuspendRecallInfo { get; set; }

    /// <summary>
    /// История движения упаковки: кто, когда и по какой декларации её держал.
    /// </summary>
    [JsonPropertyName("productInquiryHistory")]
    public IList<ProductInquiryHistory>? ProductInquiryHistory { get; set; }

    /// <summary>
    /// Предельная розничная цена препарата (в виде строки, как хранится в справочнике).
    /// </summary>
    [JsonPropertyName("overallRetailPrice")]
    public string? OverallRetailPrice { get; set; }

    /// <summary>
    /// Номер регистрационного удостоверения (сертификата) препарата.
    /// </summary>
    [JsonPropertyName("certificateNumber")]
    public string? CertificateNumber { get; set; }

    /// <summary>
    /// Инструкция по применению препарата (текст или ссылка).
    /// </summary>
    [JsonPropertyName("instructionForUse")]
    public string? InstructionForUse { get; set; }

    /// <summary>
    /// Идентификатор бинарного объекта с инструкцией по применению.
    /// </summary>
    /// <remarks>Используйте его для скачивания файла инструкции из хранилища документов KNMDB.</remarks>
    [JsonPropertyName("instructionForUseDocId")]
    public Guid? InstructionForUseDocId { get; set; }

    /// <summary>
    /// Идентификатор бинарного объекта с изображением упаковки.
    /// </summary>
    [JsonPropertyName("packagingImageDocId")]
    public Guid? PackagingImageDocId { get; set; }

    /// <summary>
    /// Признак того, что препарат входит в перечень льготного/компенсируемого обеспечения.
    /// </summary>
    /// <remarks>Может отсутствовать (<see langword="null"/>), если признак не задан.</remarks>
    [JsonPropertyName("isFomsDrug")]
    public bool? IsFomsDrug { get; set; }

    /// <summary>
    /// Размер компенсации (в виде строки, как хранится в справочнике).
    /// </summary>
    [JsonPropertyName("compensation")]
    public string? Compensation { get; set; }

    /// <summary>
    /// Тип расходной операции, по которой упаковка выбыла из оборота.
    /// </summary>
    /// <remarks>См. <see cref="ConsumptionType"/>. Может быть <see langword="null"/>, если упаковка в обороте.</remarks>
    [JsonPropertyName("consumptionType")]
    public ConsumptionType? ConsumptionType { get; set; }

    /// <summary>
    /// Наименование производителя препарата.
    /// </summary>
    [JsonPropertyName("manufacturerName")]
    public string? ManufacturerName { get; set; }

    /// <summary>
    /// Остаток препарата в упаковке после частичной продажи.
    /// </summary>
    /// <remarks>
    /// Заполняется, если по упаковке была операция частичной продажи
    /// (<see cref="ProductState.PartialSales"/>).
    /// </remarks>
    [JsonPropertyName("partialSaleRemainingAmount")]
    public decimal? PartialSaleRemainingAmount { get; set; }
}

/// <summary>
/// Сведения о приостановке или отзыве упаковки из обращения.
/// </summary>
public sealed class SuspendRecallInfo
{
    /// <summary>
    /// Дата начала периода приостановки/отзыва.
    /// </summary>
    [JsonPropertyName("startDate")]
    public DateTimeOffset? StartDate { get; set; }

    /// <summary>
    /// Дата окончания периода приостановки/отзыва.
    /// </summary>
    /// <remarks><see langword="null"/> — ограничение бессрочное (полный отзыв).</remarks>
    [JsonPropertyName("endDate")]
    public DateTimeOffset? EndDate { get; set; }

    /// <summary>
    /// Причина приостановки или отзыва.
    /// </summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}

/// <summary>
/// Одно событие в истории движения упаковки.
/// </summary>
public sealed class ProductInquiryHistory
{
    /// <summary>
    /// Номер декларации, по которой произошло событие.
    /// </summary>
    [JsonPropertyName("declarationNumber")]
    public long DeclarationNumber { get; set; }

    /// <summary>
    /// Наименование организации, у которой находилась упаковка на момент события.
    /// </summary>
    [JsonPropertyName("stakeHolder")]
    public string? StakeHolder { get; set; }

    /// <summary>
    /// Состояние упаковки, установленное данным событием.
    /// </summary>
    [JsonPropertyName("state")]
    public ProductState State { get; set; }

    /// <summary>
    /// Дата и время события.
    /// </summary>
    [JsonPropertyName("stateDate")]
    public DateTimeOffset StateDate { get; set; }

    /// <summary>
    /// Цена упаковки на момент события.
    /// </summary>
    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    /// <summary>
    /// Количество, реализованное при частичной продаже в рамках данного события.
    /// </summary>
    [JsonPropertyName("partialSaleAmount")]
    public decimal? PartialSaleAmount { get; set; }
}
