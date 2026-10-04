using System.Text.Json.Serialization;
using Knmdb.TrackAndTrace.Models.Enums;

namespace Knmdb.TrackAndTrace.Models.Responses;

/// <summary>
/// Ответ <c>POST /api/TrackAndTrace/GetStockInheldList</c> — сводные остатки по всем препаратам,
/// доступным текущему пользователю.
/// </summary>
/// <remarks>Требуется роль <c>ApiGetStockInheldList</c>.</remarks>
public sealed class GetStockInheldListResponse
{
    /// <summary>
    /// Сводка остатков в разрезе препаратов.
    /// </summary>
    [JsonPropertyName("stockInheldSummaryList")]
    public IList<StockInheldSummaryInfo>? StockInheldSummaryList { get; set; }
}

/// <summary>
/// Сводная строка остатков по одному препарату.
/// </summary>
public sealed class StockInheldSummaryInfo
{
    /// <summary>
    /// GTIN препарата.
    /// </summary>
    [JsonPropertyName("gtin")]
    public string? Gtin { get; set; }

    /// <summary>
    /// Полное торговое наименование препарата.
    /// </summary>
    [JsonPropertyName("fullBrandName")]
    public string? FullBrandName { get; set; }

    /// <summary>
    /// Суммарное количество препарата в наличии.
    /// </summary>
    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }
}

/// <summary>
/// Ответ <c>POST /api/TrackAndTrace/GetStockInheldListByGtin</c> — детальные остатки
/// по конкретному GTIN (упаковка за упаковкой).
/// </summary>
/// <remarks>Требуется роль <c>ApiGetStockInheldListByGtin</c>.</remarks>
public sealed class GetStockInheldListByGtinResponse
{
    /// <summary>
    /// Перечень упаковок в наличии.
    /// </summary>
    [JsonPropertyName("stockInheldList")]
    public IList<StockInheldInfo>? StockInheldList { get; set; }
}

/// <summary>
/// Сведения об одной упаковке в остатках.
/// </summary>
public sealed class StockInheldInfo
{
    /// <summary>
    /// Внутренний идентификатор упаковки (<c>TTProductBox.Id</c>).
    /// </summary>
    [JsonPropertyName("id")]
    public long Id { get; set; }

    /// <summary>
    /// Идентификатор организации, у которой упаковка находится в наличии.
    /// </summary>
    /// <remarks>
    /// Обратите внимание: это внутренний <see cref="Guid"/>, а не код стейкхолдера.
    /// Для получения кода и наименования используйте <c>GetAllStakeholders</c>.
    /// </remarks>
    [JsonPropertyName("currentStakeholderId")]
    public Guid CurrentStakeholderId { get; set; }

    /// <summary>
    /// Идентификатор позиции упаковки препарата (<c>DrugPackageItem</c>).
    /// </summary>
    [JsonPropertyName("drugPackageItemId")]
    public Guid DrugPackageItemId { get; set; }

    /// <summary>
    /// GTIN препарата.
    /// </summary>
    [JsonPropertyName("gtin")]
    public string? Gtin { get; set; }

    /// <summary>
    /// Полное торговое наименование препарата.
    /// </summary>
    [JsonPropertyName("fullBrandName")]
    public string? FullBrandName { get; set; }

    /// <summary>
    /// Номер партии упаковки.
    /// </summary>
    [JsonPropertyName("batchNumber")]
    public string? BatchNumber { get; set; }

    /// <summary>
    /// Дата истечения срока годности упаковки.
    /// </summary>
    [JsonPropertyName("expirationDate")]
    public DateTimeOffset ExpirationDate { get; set; }

    /// <summary>
    /// Серийный номер упаковки.
    /// </summary>
    [JsonPropertyName("serialNumber")]
    public string? SerialNumber { get; set; }

    /// <summary>
    /// Дата декларации, по которой упаковка поступила в наличие.
    /// </summary>
    [JsonPropertyName("declarationDate")]
    public DateTimeOffset? DeclarationDate { get; set; }

    /// <summary>
    /// Цена упаковки.
    /// </summary>
    [JsonPropertyName("price")]
    public decimal? Price { get; set; }

    /// <summary>
    /// Количество, уже реализованное при частичной продаже.
    /// </summary>
    [JsonPropertyName("partialSaleAmount")]
    public decimal? PartialSaleAmount { get; set; }

    /// <summary>
    /// Текущее состояние упаковки.
    /// </summary>
    [JsonPropertyName("currentState")]
    public ProductState CurrentState { get; set; }

    /// <summary>
    /// QR-код (содержимое Data Matrix) упаковки.
    /// </summary>
    [JsonPropertyName("qrCode")]
    public string? QrCode { get; set; }
}

/// <summary>
/// Ответ <c>POST /api/TrackAndTrace/GetPartialSaleInfo</c> — сведения о частично проданной упаковке.
/// </summary>
/// <remarks>
/// Требуется роль <c>ApiGetPartialSaleInfo</c>.
/// Обратите внимание: в опубликованной OpenAPI-схеме для этого метода ошибочно указан тип
/// <c>GetStockInheldListByGtinResponse</c>. Фактический ответ имеет описываемую ниже форму.
/// </remarks>
public sealed class GetPartialSaleInfoResponse
{
    /// <summary>
    /// GTIN препарата.
    /// </summary>
    [JsonPropertyName("gtin")]
    public string? Gtin { get; set; }

    /// <summary>
    /// Полное торговое наименование препарата.
    /// </summary>
    [JsonPropertyName("fullBrandName")]
    public string? FullBrandName { get; set; }

    /// <summary>
    /// Серийный номер упаковки.
    /// </summary>
    [JsonPropertyName("serialNumber")]
    public string? SerialNumber { get; set; }

    /// <summary>
    /// Количество препарата, уже реализованное при частичной продаже.
    /// </summary>
    [JsonPropertyName("partialSaleAmount")]
    public decimal PartialSaleAmount { get; set; }
}

/// <summary>
/// Ответ <c>POST /api/TrackAndTrace/GetMedicineList</c> — справочник лекарственных средств.
/// </summary>
/// <remarks>Требуется роль <c>ApiGetMedicineList</c>.</remarks>
public sealed class GetMedicineListResponse
{
    /// <summary>
    /// Общее количество препаратов в ответе.
    /// </summary>
    [JsonPropertyName("numberOfMedicines")]
    public int NumberOfMedicines { get; set; }

    /// <summary>
    /// Список препаратов.
    /// </summary>
    [JsonPropertyName("medicineList")]
    public IList<MedicineInfo>? MedicineList { get; set; }
}

/// <summary>
/// Сведения о лекарственном средстве в справочнике.
/// </summary>
public sealed class MedicineInfo
{
    /// <summary>
    /// GTIN препарата.
    /// </summary>
    [JsonPropertyName("gtin")]
    public string? Gtin { get; set; }

    /// <summary>
    /// Торговое наименование препарата (краткое).
    /// </summary>
    [JsonPropertyName("brandName")]
    public string? BrandName { get; set; }

    /// <summary>
    /// Полное торговое наименование препарата с дозировкой и формой выпуска.
    /// </summary>
    [JsonPropertyName("fullBrandName")]
    public string? FullBrandName { get; set; }

    /// <summary>
    /// Наименование компании-производителя.
    /// </summary>
    [JsonPropertyName("manufacturerCompany")]
    public string? ManufacturerCompany { get; set; }

    /// <summary>
    /// Страна происхождения препарата.
    /// </summary>
    [JsonPropertyName("country")]
    public string? Country { get; set; }

    /// <summary>
    /// Код АТХ (анатомо-терапевтическо-химическая классификация).
    /// </summary>
    [JsonPropertyName("atcCode")]
    public string? AtcCode { get; set; }

    /// <summary>
    /// Лекарственная форма / состав препарата.
    /// </summary>
    [JsonPropertyName("formula")]
    public string? Formula { get; set; }

    /// <summary>
    /// Перечень действующих веществ (МНН).
    /// </summary>
    [JsonPropertyName("innList")]
    public string? InnList { get; set; }

    /// <summary>
    /// Статус обязательности маркировки и прослеживаемости препарата.
    /// </summary>
    /// <remarks>См. <see cref="TrackAndTraceStatus"/>.</remarks>
    [JsonPropertyName("trackAndTraceStatus")]
    public TrackAndTraceStatus TrackAndTraceStatus { get; set; }

    /// <summary>
    /// Дата и время последнего изменения карточки препарата.
    /// </summary>
    /// <remarks>
    /// Используйте максимальное значение этого поля как <c>lastUpdate</c>
    /// в следующем запросе для инкрементальной синхронизации.
    /// </remarks>
    [JsonPropertyName("lastUpdate")]
    public DateTimeOffset LastUpdate { get; set; }
}
