using System.Text.Json.Serialization;
using Knmdb.TrackAndTrace.Models.Enums;
using Knmdb.TrackAndTrace.Serialization;

namespace Knmdb.TrackAndTrace.Models.Requests;

/// <summary>
/// Запрос <c>POST /api/TrackAndTrace/StockDeclaration</c> — постановка упаковок на складской учёт.
/// </summary>
/// <remarks>
/// <para>
/// Применяется для инвентаризации и первичной постановки ранее промаркированных упаковок на баланс склада.
/// После успешного вызова упаковки получают состояние <see cref="ProductState.Stock"/>.
/// </para>
/// <para>Требуется роль <c>ApiStockDeclaration</c>.</para>
/// </remarks>
public sealed class StockDeclarationRequest
{
    /// <summary>
    /// Дата декларации.
    /// </summary>
    /// <remarks>Обязательное поле. Сериализуется в строку формата <c>yyyy-MM-dd</c>.</remarks>
    [JsonPropertyName("declarationDate")]
    [JsonConverter(typeof(DateOnlyJsonConverter))]
    public DateTimeOffset DeclarationDate { get; set; }

    /// <summary>
    /// Код стейкхолдера-склада, на который ставится товар.
    /// </summary>
    /// <remarks>Обязательное поле. Список кодов: <c>GET /api/TrackAndTrace/GetAllStakeholders</c>.</remarks>
    [JsonPropertyName("stakeholderCode")]
    public long StakeholderCode { get; set; }

    /// <summary>
    /// Произвольное текстовое описание декларации.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Перечень упаковок, ставящихся на учёт.
    /// </summary>
    [JsonPropertyName("details")]
    public IList<StockDeclarationDetail>? Details { get; set; }
}

/// <summary>
/// Элемент списка упаковок в <see cref="StockDeclarationRequest"/>.
/// </summary>
public sealed class StockDeclarationDetail
{
    /// <summary>
    /// Номер партии упаковки.
    /// </summary>
    [JsonPropertyName("batchNumber")]
    public string? BatchNumber { get; set; }

    /// <summary>
    /// Дата истечения срока годности упаковки.
    /// </summary>
    /// <remarks>Необязательное поле. Сериализуется в строку формата <c>yyyy-MM-dd</c>.</remarks>
    [JsonPropertyName("expirationDate")]
    [JsonConverter(typeof(NullableDateOnlyJsonConverter))]
    public DateTimeOffset? ExpirationDate { get; set; }

    /// <summary>
    /// QR-код (содержимое Data Matrix) упаковки.
    /// </summary>
    [JsonPropertyName("qrCode")]
    public string? QrCode { get; set; }
}

/// <summary>
/// Запрос <c>POST /api/TrackAndTrace/StockDeclarationCancel</c> — отмена ранее созданной
/// складской декларации.
/// </summary>
/// <remarks>Требуется роль <c>ApiStockDeclarationCancel</c>.</remarks>
public sealed class StockDeclarationCancelRequest
{
    /// <summary>
    /// Идентификатор отменяемой складской декларации.
    /// </summary>
    [JsonPropertyName("declarationId")]
    public long DeclarationId { get; set; }
}

/// <summary>
/// Запрос <c>POST /api/TrackAndTrace/SalesDeclaration</c> — декларация реализации
/// (продажи/расхода) упаковок.
/// </summary>
/// <remarks>
/// <para>
/// Операция выбытия: упаковки помечаются как проданные (<see cref="ProductState.Sales"/>)
/// либо частично проданные (<see cref="ProductState.PartialSales"/>).
/// </para>
/// <para>Требуется роль <c>ApiSalesDeclaration</c>.</para>
/// </remarks>
public sealed class SalesDeclarationRequest
{
    /// <summary>
    /// Идентификатор рецепта.
    /// </summary>
    /// <remarks>Необязательное поле. Заполняется при реализации по рецепту.</remarks>
    [JsonPropertyName("prescriptionId")]
    public string? PrescriptionId { get; set; }

    /// <summary>
    /// ПИН (персональный идентификационный номер) гражданина-покупателя.
    /// </summary>
    /// <remarks>Необязательное поле. Используется при льготном/компенсируемом отпуске.</remarks>
    [JsonPropertyName("patientId")]
    public string? PatientId { get; set; }

    /// <summary>
    /// Номер требования медицинской организации.
    /// </summary>
    /// <remarks>Необязательное поле. Заполняется только для больничного отпуска (hospital).</remarks>
    [JsonPropertyName("requestNumber")]
    public string? RequestNumber { get; set; }

    /// <summary>
    /// Идентификатор подразделения (отделения) медицинской организации.
    /// </summary>
    /// <remarks>Необязательное поле. Заполняется только для больничного отпуска.</remarks>
    [JsonPropertyName("branchDefId")]
    public string? BranchDefId { get; set; }

    /// <summary>
    /// Наименование отделения медицинской организации.
    /// </summary>
    /// <remarks>Необязательное поле. Заполняется только для больничного отпуска.</remarks>
    [JsonPropertyName("departmentName")]
    public string? DepartmentName { get; set; }

    /// <summary>
    /// Признак аптечной продажи.
    /// </summary>
    /// <remarks>
    /// <see langword="true"/> — реализация через аптеку (розница);
    /// <see langword="false"/> — расход медицинской организации.
    /// </remarks>
    [JsonPropertyName("isPharmacyConsuption")]
    public bool IsPharmacyConsumption { get; set; }

    /// <summary>
    /// Перечень реализуемых упаковок.
    /// </summary>
    [JsonPropertyName("details")]
    public IList<SalesDeclarationDetail>? Details { get; set; }
}

/// <summary>
/// Элемент списка реализуемых упаковок в <see cref="SalesDeclarationRequest"/>.
/// </summary>
public sealed class SalesDeclarationDetail
{
    /// <summary>
    /// QR-код (содержимое Data Matrix) упаковки.
    /// </summary>
    [JsonPropertyName("qrCode")]
    public string? QrCode { get; set; }

    /// <summary>
    /// Цена реализации упаковки (или её проданной части).
    /// </summary>
    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    /// <summary>
    /// Признак частичной продажи упаковки.
    /// </summary>
    /// <remarks>
    /// <see langword="true"/> — продаётся только часть содержимого упаковки,
    /// в этом случае обязателен <see cref="PartialSaleAmount"/>.
    /// </remarks>
    [JsonPropertyName("isPartialSale")]
    public bool IsPartialSale { get; set; }

    /// <summary>
    /// Количество, реализуемое при частичной продаже.
    /// </summary>
    /// <remarks>Имеет смысл только при <see cref="IsPartialSale"/> = <see langword="true"/>.</remarks>
    [JsonPropertyName("partialSaleAmount")]
    public decimal PartialSaleAmount { get; set; }
}

/// <summary>
/// Запрос <c>POST /api/TrackAndTrace/SalesDeclarationCancel</c> — отмена декларации реализации
/// целиком (по идентификатору).
/// </summary>
/// <remarks>Требуется роль <c>ApiSalesDeclarationCancel</c>.</remarks>
public sealed class SalesDeclarationCancelRequest
{
    /// <summary>
    /// Идентификатор отменяемой декларации реализации.
    /// </summary>
    [JsonPropertyName("declarationId")]
    public long DeclarationId { get; set; }
}

/// <summary>
/// Запрос <c>POST /api/TrackAndTrace/SalesDeclarationBoxCancel</c> — отмена реализации
/// отдельной упаковки (по QR-коду).
/// </summary>
/// <remarks>
/// Применяется, когда требуется отменить продажу одной конкретной упаковки,
/// не затрагивая остальные позиции декларации.
/// Требуется роль <c>ApiSalesDeclarationBoxCancel</c>.
/// </remarks>
public sealed class SalesDeclarationBoxCancelRequest
{
    /// <summary>
    /// QR-код (содержимое Data Matrix) упаковки, продажу которой нужно отменить.
    /// </summary>
    [JsonPropertyName("qrCode")]
    public string? QrCode { get; set; }
}

/// <summary>
/// Запрос <c>POST /api/TrackAndTrace/DeactivateDeclaration</c> — деактивация (списание)
/// упаковок с указанием типа расходной операции.
/// </summary>
/// <remarks>Требуется роль <c>ApiDeactivateDeclaration</c>.</remarks>
public sealed class DeactivateDeclarationRequest
{
    /// <summary>
    /// Тип расходной операции, по которой упаковки выводятся из оборота.
    /// </summary>
    /// <remarks>
    /// Обязательное поле. Значение по умолчанию — <see cref="ConsumptionType.SystemOut"/>.
    /// Полный список значений и их смысл см. в <see cref="ConsumptionType"/>.
    /// </remarks>
    [JsonPropertyName("consumptionType")]
    public ConsumptionType ConsumptionType { get; set; }

    /// <summary>
    /// Произвольное текстовое описание (комментарий) деактивации.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Перечень деактивируемых упаковок.
    /// </summary>
    [JsonPropertyName("details")]
    public IList<DeactivateDeclarationDetail>? Details { get; set; }
}

/// <summary>
/// Элемент списка деактивируемых упаковок в <see cref="DeactivateDeclarationRequest"/>.
/// </summary>
public sealed class DeactivateDeclarationDetail
{
    /// <summary>
    /// QR-код (содержимое Data Matrix) деактивируемой упаковки.
    /// </summary>
    [JsonPropertyName("qrCode")]
    public string? QrCode { get; set; }
}
