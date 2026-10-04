using System.Text.Json.Serialization;
using Knmdb.TrackAndTrace.Models.Enums;

namespace Knmdb.TrackAndTrace.Models.Responses;

/// <summary>
/// Ответ <c>GET /api/TrackAndTrace/GetAllStakeholders</c> — список всех организаций
/// (стейкхолдеров), зарегистрированных в системе.
/// </summary>
/// <remarks>Требуется роль <c>ApiGetAllStakeholders</c>.</remarks>
public sealed class GetAllStakeholdersResponse
{
    /// <summary>
    /// Общее количество организаций в ответе.
    /// </summary>
    [JsonPropertyName("numberOfStakeholders")]
    public int NumberOfStakeholders { get; set; }

    /// <summary>
    /// Список организаций.
    /// </summary>
    /// <remarks>Может быть <see langword="null"/>, если список пуст.</remarks>
    [JsonPropertyName("stakeholders")]
    public IList<StakeholderInfo>? Stakeholders { get; set; }
}

/// <summary>
/// Сведения об организации-участнике оборота лекарственных средств.
/// </summary>
public sealed class StakeholderInfo
{
    /// <summary>
    /// Уникальный код организации в системе KNMDB.
    /// </summary>
    /// <remarks>
    /// Именно это значение передаётся в поля <c>stakeholderCode</c>, <c>fromStakeholder</c>,
    /// <c>toStakeholder</c> других запросов.
    /// </remarks>
    [JsonPropertyName("code")]
    public long Code { get; set; }

    /// <summary>
    /// Полное наименование организации.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Тип организации.
    /// </summary>
    /// <remarks>См. <see cref="StakeholderType"/>.</remarks>
    [JsonPropertyName("type")]
    public StakeholderType Type { get; set; }

    /// <summary>
    /// ИНН (налоговый номер) организации.
    /// </summary>
    [JsonPropertyName("taxNumber")]
    public string? TaxNumber { get; set; }

    /// <summary>
    /// Адрес организации (улица, дом).
    /// </summary>
    [JsonPropertyName("address")]
    public string? Address { get; set; }

    /// <summary>
    /// Город организации.
    /// </summary>
    [JsonPropertyName("city")]
    public string? City { get; set; }

    /// <summary>
    /// Район (область) организации.
    /// </summary>
    [JsonPropertyName("district")]
    public string? District { get; set; }

    /// <summary>
    /// Код головной (родительской) организации, если текущая является филиалом.
    /// </summary>
    [JsonPropertyName("parentCode")]
    public long? ParentCode { get; set; }

    /// <summary>
    /// Наименование головной (родительской) организации, если применимо.
    /// </summary>
    [JsonPropertyName("parentName")]
    public string? ParentName { get; set; }
}

/// <summary>
/// Ответ <c>GET /api/TrackAndTrace/GetSupportedQRTypes</c> — перечень поддерживаемых типов QR-кодов.
/// </summary>
/// <remarks>
/// <para>
/// <b>Устаревший тип.</b> Метод <c>GetSupportedQRTypes</c> исключён из API департамента
/// лекарственных средств и будет удалён из SDK, когда окончательно исчезнет из API.
/// Новую интеграцию на него не закладывайте.
/// </para>
/// </remarks>
[Obsolete(
    "Метод GetSupportedQRTypes исключён из API департамента лекарственных средств. " +
    "Тип будет удалён из SDK вместе с методом GetSupportedQrTypesAsync.",
    error: false,
    DiagnosticId = "KNMDB0001")]
public sealed class GetSupportedQRTypesResponse
{
    /// <summary>
    /// Список поддерживаемых типов QR-кодов.
    /// </summary>
    [JsonPropertyName("qrTypes")]
    public IList<QRTypeInfo>? QrTypes { get; set; }
}

/// <summary>
/// Описание одного типа QR-кода (формата Data Matrix).
/// </summary>
/// <remarks>
/// <para>
/// <b>Устаревший тип.</b> Используется только методом <c>GetSupportedQrTypesAsync</c>,
/// который исключён из API департамента лекарственных средств.
/// </para>
/// </remarks>
[Obsolete(
    "Тип QR-кода используется только устаревшим методом GetSupportedQrTypesAsync. " +
    "Тип будет удалён из SDK вместе с ним.",
    error: false,
    DiagnosticId = "KNMDB0001")]
public sealed class QRTypeInfo
{
    /// <summary>
    /// Числовой идентификатор типа QR-кода.
    /// </summary>
    /// <remarks>Передаётся в поле <c>qrTypeId</c> запросов деклараций.</remarks>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    /// Краткое наименование типа QR-кода.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Подробное описание типа QR-кода: порядок и длина полей внутри Data Matrix.
    /// </summary>
    /// <remarks>
    /// <para>
    /// В зависимости от типа порядок данных в коде отличается. Пример описания для европейского типа:
    /// <c>01GTIN(14) 21SerialNumber(18) 17ExpiryDate(yymmdd)(6) 10BatchNumber(11)</c>, где
    /// GTIN — 14 знаков, серийный номер — 18 знаков, срок годности — 6 знаков в формате
    /// <c>yymmdd</c>, номер партии — 11 знаков.
    /// </para>
    /// </remarks>
    [JsonPropertyName("description")]
    public string? Description { get; set; }
}
