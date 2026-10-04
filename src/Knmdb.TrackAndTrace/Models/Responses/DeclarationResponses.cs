using System.Text.Json.Serialization;

namespace Knmdb.TrackAndTrace.Models.Responses;

/// <summary>
/// Ответ <c>POST /api/TrackAndTrace/ImportDeclaration</c> — результат регистрации партии импорта.
/// </summary>
/// <remarks>Требуется роль <c>ApiImportDeclaration</c>.</remarks>
public sealed class ImportDeclarationResponse
{
    /// <summary>
    /// Код стейкхолдера, на который приходована партия.
    /// </summary>
    /// <remarks>Соответствует коду из <c>GET /api/TrackAndTrace/GetAllStakeholders</c>.</remarks>
    [JsonPropertyName("stakeholderCode")]
    public long StakeholderCode { get; set; }

    /// <summary>
    /// Идентификатор созданной декларации импорта.
    /// </summary>
    /// <remarks>Используйте его для отмены декларации (<c>StockDeclarationCancel</c> / <c>DeactivateDeclaration</c>).</remarks>
    [JsonPropertyName("declarationId")]
    public long DeclarationId { get; set; }

    /// <summary>
    /// Дата и время регистрации декларации импорта.
    /// </summary>
    [JsonPropertyName("declarationDate")]
    public DateTimeOffset DeclarationDate { get; set; }

    /// <summary>
    /// GTIN зарегистрированной партии.
    /// </summary>
    [JsonPropertyName("gtin")]
    public string? Gtin { get; set; }

    /// <summary>
    /// Дата производства партии.
    /// </summary>
    [JsonPropertyName("productionDate")]
    public DateTimeOffset ProductionDate { get; set; }

    /// <summary>
    /// Дата истечения срока годности партии.
    /// </summary>
    [JsonPropertyName("expirationDate")]
    public DateTimeOffset ExpirationDate { get; set; }

    /// <summary>
    /// Номер партии.
    /// </summary>
    [JsonPropertyName("batchNo")]
    public string? BatchNo { get; set; }
}

/// <summary>
/// Ответ <c>POST /api/TrackAndTrace/ProductionDeclaration</c> — результат регистрации партии производства.
/// </summary>
/// <remarks>Требуется роль <c>ApiProductionDeclaration</c>.</remarks>
public sealed class ProductionDeclarationResponse
{
    /// <summary>
    /// Код стейкхолдера-производителя.
    /// </summary>
    [JsonPropertyName("stakeholderCode")]
    public long StakeholderCode { get; set; }

    /// <summary>
    /// Идентификатор созданной декларации производства.
    /// </summary>
    [JsonPropertyName("declarationId")]
    public long DeclarationId { get; set; }

    /// <summary>
    /// Дата и время регистрации декларации.
    /// </summary>
    [JsonPropertyName("declarationDate")]
    public DateTimeOffset DeclarationDate { get; set; }

    /// <summary>
    /// GTIN зарегистрированной партии.
    /// </summary>
    [JsonPropertyName("gtin")]
    public string? Gtin { get; set; }

    /// <summary>
    /// Дата производства партии.
    /// </summary>
    [JsonPropertyName("productionDate")]
    public DateTimeOffset ProductionDate { get; set; }

    /// <summary>
    /// Дата истечения срока годности партии.
    /// </summary>
    [JsonPropertyName("expirationDate")]
    public DateTimeOffset ExpirationDate { get; set; }

    /// <summary>
    /// Номер партии.
    /// </summary>
    [JsonPropertyName("batchNo")]
    public string? BatchNo { get; set; }
}

/// <summary>
/// Ответ <c>POST /api/TrackAndTrace/TransferDeclaration</c> — результат создания перемещения.
/// </summary>
/// <remarks>Требуется роль <c>ApiTransferDeclaration</c>.</remarks>
public sealed class TransferDeclarationResponse
{
    /// <summary>
    /// Идентификатор созданной декларации перемещения.
    /// </summary>
    /// <remarks>Передайте его получателю для подтверждения методом <c>TransferAccept</c>.</remarks>
    [JsonPropertyName("declarationId")]
    public long DeclarationId { get; set; }

    /// <summary>
    /// Дата и время создания декларации перемещения.
    /// </summary>
    [JsonPropertyName("declarationDate")]
    public DateTimeOffset DeclarationDate { get; set; }
}

/// <summary>
/// Ответ методов отмены перемещения — <c>TransferCancel</c> и <c>TransferReturnCancel</c>.
/// </summary>
/// <remarks>
/// Роли: <c>ApiTransferCancel</c> и <c>ApiTransferReturnCancel</c> соответственно.
/// </remarks>
public sealed class TransferDeclarationCancelResponse
{
    /// <summary>
    /// Идентификатор отменённой декларации перемещения.
    /// </summary>
    [JsonPropertyName("declarationId")]
    public long DeclarationId { get; set; }

    /// <summary>
    /// Дата и время отмены декларации.
    /// </summary>
    [JsonPropertyName("declarationDate")]
    public DateTimeOffset DeclarationDate { get; set; }
}

/// <summary>
/// Ответ <c>POST /api/TrackAndTrace/StockDeclaration</c> — результат постановки упаковок на склад.
/// </summary>
/// <remarks>Требуется роль <c>ApiStockDeclaration</c>.</remarks>
public sealed class StockDeclarationResponse
{
    /// <summary>
    /// Идентификатор созданной складской декларации.
    /// </summary>
    [JsonPropertyName("declarationId")]
    public long DeclarationId { get; set; }

    /// <summary>
    /// Дата и время создания складской декларации.
    /// </summary>
    [JsonPropertyName("declarationDate")]
    public DateTimeOffset DeclarationDate { get; set; }
}

/// <summary>
/// Ответ <c>POST /api/TrackAndTrace/StockDeclarationCancel</c> — результат отмены складской декларации.
/// </summary>
/// <remarks>Требуется роль <c>ApiStockDeclarationCancel</c>.</remarks>
public sealed class StockDeclarationCancelResponse
{
    /// <summary>
    /// Идентификатор отменённой складской декларации.
    /// </summary>
    [JsonPropertyName("declarationId")]
    public long DeclarationId { get; set; }

    /// <summary>
    /// Признак успешности отмены.
    /// </summary>
    /// <remarks>
    /// Если сервер вернул <see langword="false"/>, причина описана в <see cref="Message"/>.
    /// Обратите внимание: при этом HTTP-ответ всё равно имеет статус 200.
    /// </remarks>
    [JsonPropertyName("isSuccess")]
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Текстовое пояснение результата отмены.
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

/// <summary>
/// Ответ методов реализации — <c>SalesDeclaration</c>, <c>SalesDeclarationCancel</c>
/// и <c>SalesDeclarationBoxCancel</c>.
/// </summary>
/// <remarks>
/// Роли: <c>ApiSalesDeclaration</c>, <c>ApiSalesDeclarationCancel</c>,
/// <c>ApiSalesDeclarationBoxCancel</c> соответственно.
/// </remarks>
public sealed class SalesDeclarationResponse
{
    /// <summary>
    /// Код стейкхолдера, оформившего реализацию.
    /// </summary>
    /// <remarks>Соответствует коду из <c>GET /api/TrackAndTrace/GetAllStakeholders</c>.</remarks>
    [JsonPropertyName("stakeholderCode")]
    public long StakeholderCode { get; set; }

    /// <summary>
    /// Идентификатор декларации реализации.
    /// </summary>
    [JsonPropertyName("declarationId")]
    public long DeclarationId { get; set; }

    /// <summary>
    /// Дата и время операции.
    /// </summary>
    [JsonPropertyName("declarationDate")]
    public DateTimeOffset DeclarationDate { get; set; }
}

/// <summary>
/// Ответ <c>POST /api/TrackAndTrace/DeactivateDeclaration</c> — результат деактивации упаковок.
/// </summary>
/// <remarks>Требуется роль <c>ApiDeactivateDeclaration</c>.</remarks>
public sealed class DeactivateDeclarationResponse
{
    /// <summary>
    /// Идентификатор созданной декларации деактивации.
    /// </summary>
    [JsonPropertyName("declarationId")]
    public long DeclarationId { get; set; }

    /// <summary>
    /// Дата и время деактивации.
    /// </summary>
    [JsonPropertyName("declarationDate")]
    public DateTimeOffset DeclarationDate { get; set; }
}
