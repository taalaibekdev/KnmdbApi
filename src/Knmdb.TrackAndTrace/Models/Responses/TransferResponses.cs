using System.Text.Json.Serialization;
using Knmdb.TrackAndTrace.Models.Enums;

namespace Knmdb.TrackAndTrace.Models.Responses;

/// <summary>
/// Ответ <c>GET /api/TrackAndTrace/GetTransferDeclaration?declarationId=...</c> — полное содержимое
/// одной декларации перемещения, включая перечень упаковок.
/// </summary>
/// <remarks>Требуется роль <c>ApiGetTransferDeclaration</c>.</remarks>
public sealed class GetTransferDeclarationResponse
{
    /// <summary>
    /// Идентификатор декларации перемещения.
    /// </summary>
    [JsonPropertyName("declarationId")]
    public long DeclarationId { get; set; }

    /// <summary>
    /// Номер сопроводительного документа.
    /// </summary>
    [JsonPropertyName("documentNo")]
    public string? DocumentNo { get; set; }

    /// <summary>
    /// Дата сопроводительного документа.
    /// </summary>
    /// <remarks>Может отсутствовать, если документ не указан при создании перемещения.</remarks>
    [JsonPropertyName("documentDate")]
    public DateTimeOffset? DocumentDate { get; set; }

    /// <summary>
    /// Код стейкхолдера-отправителя.
    /// </summary>
    [JsonPropertyName("fromStakeholder")]
    public long FromStakeholder { get; set; }

    /// <summary>
    /// Код стейкхолдера-получателя.
    /// </summary>
    [JsonPropertyName("toStakeholder")]
    public long ToStakeholder { get; set; }

    /// <summary>
    /// Дата декларации перемещения.
    /// </summary>
    [JsonPropertyName("declarationDate")]
    public DateTimeOffset DeclarationDate { get; set; }

    /// <summary>
    /// Текстовое описание перемещения.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Текущее состояние перемещения.
    /// </summary>
    /// <remarks>См. <see cref="TransferState"/>.</remarks>
    [JsonPropertyName("currentState")]
    public TransferState CurrentState { get; set; }

    /// <summary>
    /// Признак возвратного перемещения.
    /// </summary>
    /// <remarks><see langword="true"/> — перемещение является возвратом.</remarks>
    [JsonPropertyName("isReturn")]
    public bool IsReturn { get; set; }

    /// <summary>
    /// Перечень упаковок в составе перемещения.
    /// </summary>
    [JsonPropertyName("details")]
    public IList<TransferDeclarationDetailItem>? Details { get; set; }
}

/// <summary>
/// Сведения об одной упаковке в составе декларации перемещения.
/// </summary>
public sealed class TransferDeclarationDetailItem
{
    /// <summary>
    /// Внутренний идентификатор упаковки в системе (<c>TTProductBox.Id</c>).
    /// </summary>
    [JsonPropertyName("productBoxId")]
    public long ProductBoxId { get; set; }

    /// <summary>
    /// Идентификатор позиции упаковки препарата (<c>DrugPackageItem</c>).
    /// </summary>
    /// <remarks>Связывает упаковку с карточкой препарата в справочнике.</remarks>
    [JsonPropertyName("drugPackageItemId")]
    public Guid DrugPackageItemId { get; set; }

    /// <summary>
    /// Полное торговое наименование препарата с дозировкой и формой выпуска.
    /// </summary>
    [JsonPropertyName("fullBrandName")]
    public string? FullBrandName { get; set; }

    /// <summary>
    /// QR-код (содержимое Data Matrix) упаковки.
    /// </summary>
    [JsonPropertyName("qrCode")]
    public string? QrCode { get; set; }

    /// <summary>
    /// GTIN препарата.
    /// </summary>
    [JsonPropertyName("gtin")]
    public string? Gtin { get; set; }

    /// <summary>
    /// Номер партии упаковки.
    /// </summary>
    [JsonPropertyName("batchNumber")]
    public string? BatchNumber { get; set; }

    /// <summary>
    /// Дата истечения срока годности упаковки.
    /// </summary>
    /// <remarks>Может отсутствовать, если данные не были заполнены.</remarks>
    [JsonPropertyName("expirationDate")]
    public DateTimeOffset? ExpirationDate { get; set; }

    /// <summary>
    /// Серийный номер упаковки.
    /// </summary>
    [JsonPropertyName("serialNumber")]
    public string? SerialNumber { get; set; }

    /// <summary>
    /// Цена упаковки в составе перемещения.
    /// </summary>
    [JsonPropertyName("price")]
    public decimal Price { get; set; }
}

/// <summary>
/// Ответ <c>POST /api/TrackAndTrace/GetTransferListByFilter</c> — список деклараций перемещения,
/// удовлетворяющих фильтру.
/// </summary>
/// <remarks>Требуется роль <c>ApiGetTransferListByFilter</c>.</remarks>
public sealed class GetTransferListByFilterResponse
{
    /// <summary>
    /// Найденные декларации перемещения.
    /// </summary>
    /// <remarks>
    /// Элементы содержат только «шапку» перемещения (без перечня упаковок). Чтобы получить состав,
    /// вызовите <c>GET /api/TrackAndTrace/GetTransferDeclaration</c> с нужным <c>declarationId</c>.
    /// </remarks>
    [JsonPropertyName("transferDeclarationList")]
    public IList<TransferDeclarationInfo>? TransferDeclarationList { get; set; }
}

/// <summary>
/// Краткая информация о декларации перемещения (элемент списка).
/// </summary>
public sealed class TransferDeclarationInfo
{
    /// <summary>
    /// Идентификатор декларации перемещения.
    /// </summary>
    [JsonPropertyName("declarationId")]
    public long DeclarationId { get; set; }

    /// <summary>
    /// Номер сопроводительного документа.
    /// </summary>
    [JsonPropertyName("documentNo")]
    public string? DocumentNo { get; set; }

    /// <summary>
    /// Дата сопроводительного документа.
    /// </summary>
    [JsonPropertyName("documentDate")]
    public DateTimeOffset? DocumentDate { get; set; }

    /// <summary>
    /// Дата декларации перемещения.
    /// </summary>
    [JsonPropertyName("declarationDate")]
    public DateTimeOffset DeclarationDate { get; set; }

    /// <summary>
    /// Код стейкхолдера-отправителя.
    /// </summary>
    [JsonPropertyName("fromStakeholderCode")]
    public long FromStakeholderCode { get; set; }

    /// <summary>
    /// Наименование стейкхолдера-отправителя.
    /// </summary>
    [JsonPropertyName("fromStakeholder")]
    public string? FromStakeholder { get; set; }

    /// <summary>
    /// Код стейкхолдера-получателя.
    /// </summary>
    [JsonPropertyName("toStakeholderCode")]
    public long ToStakeholderCode { get; set; }

    /// <summary>
    /// Наименование стейкхолдера-получателя.
    /// </summary>
    [JsonPropertyName("toStakeholder")]
    public string? ToStakeholder { get; set; }

    /// <summary>
    /// Общее количество упаковок в перемещении.
    /// </summary>
    [JsonPropertyName("totalCount")]
    public long TotalCount { get; set; }

    /// <summary>
    /// Текстовое описание перемещения.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Текущее состояние перемещения.
    /// </summary>
    [JsonPropertyName("currentState")]
    public TransferState CurrentState { get; set; }

    /// <summary>
    /// Роль текущего стейкхолдера в перемещении.
    /// </summary>
    [JsonPropertyName("transferType")]
    public TransferType TransferType { get; set; }

    /// <summary>
    /// Признак возвратного перемещения.
    /// </summary>
    [JsonPropertyName("isReturn")]
    public bool IsReturn { get; set; }
}
