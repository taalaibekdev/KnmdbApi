using System.Text.Json.Serialization;
using Knmdb.TrackAndTrace.Models.Enums;
using Knmdb.TrackAndTrace.Serialization;

namespace Knmdb.TrackAndTrace.Models.Requests;

/// <summary>
/// Запрос <c>POST /api/TrackAndTrace/ImportDeclaration</c> — декларация импорта партии
/// лекарственного средства с регистрацией серийных номеров (QR-кодов).
/// </summary>
/// <remarks>
/// <para>
/// Операция вводит в оборот партию импортированного препарата и «привязывает» к ней список
/// ранее выпущенных QR-кодов (Data Matrix). После успешного вызова каждая упаковка получает
/// состояние <see cref="ProductState.Import"/>.
/// </para>
/// <para>
/// Требуется роль <c>ApiImportDeclaration</c>.
/// </para>
/// </remarks>
public sealed class ImportDeclarationRequest
{
    /// <summary>
    /// GTIN (Global Trade Item Number) препарата — 14-значный идентификатор торговой единицы.
    /// </summary>
    /// <remarks>
    /// <para>Обязательное поле, минимальная длина 1 символ.</para>
    /// <para>Должен совпадать с GTIN, указанным при заказе QR-кодов, иначе сервер вернёт ошибку проверки.</para>
    /// </remarks>
    [JsonPropertyName("gtin")]
    public required string Gtin { get; set; }

    /// <summary>
    /// Номер партии (серии) препарата, присвоенный производителем.
    /// </summary>
    /// <remarks>Обязательное поле, минимальная длина 1 символ. Произвольная строка до 11 символов в QR-коде.</remarks>
    [JsonPropertyName("batchNo")]
    public required string BatchNo { get; set; }

    /// <summary>
    /// Дата истечения срока годности препарата.
    /// </summary>
    /// <remarks>
    /// <para>Обязательное поле. Сериализуется в строку формата <c>yyyy-MM-dd</c>.</para>
    /// <para>Должна совпадать с датой, зашитой в QR-коды партии.</para>
    /// </remarks>
    [JsonPropertyName("expirationDate")]
    [JsonConverter(typeof(DateOnlyJsonConverter))]
    public required DateTimeOffset ExpirationDate { get; set; }

    /// <summary>
    /// Цена за единицу препарата (цена упаковки в валюте системы).
    /// </summary>
    /// <remarks>Обязательное поле. Отрицательные значения недопустимы.</remarks>
    [JsonPropertyName("price")]
    public required decimal Price { get; set; }

    /// <summary>
    /// Дата производства препарата.
    /// </summary>
    /// <remarks>Обязательное поле. Сериализуется в строку формата <c>yyyy-MM-dd</c>.</remarks>
    [JsonPropertyName("productionDate")]
    [JsonConverter(typeof(DateOnlyJsonConverter))]
    public required DateTimeOffset ProductionDate { get; set; }

    /// <summary>
    /// Дата документа-основания (накладной, ГТД и т. п.).
    /// </summary>
    /// <remarks>Обязательное поле. Сериализуется в строку формата <c>yyyy-MM-dd</c>.</remarks>
    [JsonPropertyName("documentDate")]
    [JsonConverter(typeof(DateOnlyJsonConverter))]
    public required DateTimeOffset DocumentDate { get; set; }

    /// <summary>
    /// Номер документа-основания.
    /// </summary>
    /// <remarks>Обязательное поле, минимальная длина 1 символ.</remarks>
    [JsonPropertyName("documentNo")]
    public required string DocumentNo { get; set; }

    /// <summary>
    /// Идентификатор типа QR-кода.
    /// </summary>
    /// <remarks>
    /// <para>Обязательное поле.</para>
    /// <para>
    /// Список допустимых значений получайте методом <c>GET /api/TrackAndTrace/GetSupportedQRTypes</c>.
    /// Тип определяет порядок полей внутри Data Matrix кода (GTIN, серийный номер, срок годности, номер партии).
    /// </para>
    /// </remarks>
    [JsonPropertyName("qrTypeId")]
    public required int QrTypeId { get; set; }

    /// <summary>
    /// Список QR-кодов (содержимое Data Matrix) регистрируемой партии.
    /// </summary>
    /// <remarks>
    /// <para>Обязательное поле.</para>
    /// <para>Каждый элемент — строка, считанная сканером или сгенерированная по правилам выбранного <see cref="QrTypeId"/>.</para>
    /// </remarks>
    [JsonPropertyName("qrCodes")]
    public required IList<string> QrCodes { get; set; }

    /// <summary>
    /// Произвольное текстовое описание декларации импорта.
    /// </summary>
    /// <remarks>Необязательное поле. Может содержать любую пояснительную информацию.</remarks>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Регистрационный номер заявки на импорт.
    /// </summary>
    /// <remarks>
    /// Необязательное поле. Используется в конфигурации для Азербайджана (<c>AZERBAIJAN</c>),
    /// в базовой конфигурации (Кыргызстан) не применяется.
    /// </remarks>
    [JsonPropertyName("importApplicationRegistrationNumber")]
    public long? ImportApplicationRegistrationNumber { get; set; }

    /// <summary>
    /// ИНН (tax number) компании-импортёра.
    /// </summary>
    /// <remarks>Необязательное поле. Позволяет серверу однозначно определить импортёра в спорных случаях.</remarks>
    [JsonPropertyName("importerCompanyTaxNumber")]
    public string? ImporterCompanyTaxNumber { get; set; }

    /// <summary>
    /// Код стейкхолдера-склада, на который приходуется партия.
    /// </summary>
    /// <remarks>
    /// Необязательное поле. Список кодов получайте методом <c>GET /api/TrackAndTrace/GetAllStakeholders</c>.
    /// Если не указано, сервер использует стейкхолдера, привязанного к учётной записи.
    /// </remarks>
    [JsonPropertyName("stakeholderCode")]
    public long? StakeholderCode { get; set; }
}

/// <summary>
/// Запрос <c>POST /api/TrackAndTrace/ProductionDeclaration</c> — декларация производства партии
/// лекарственного средства с регистрацией серийных номеров.
/// </summary>
/// <remarks>
/// <para>
/// Аналогична декларации импорта, но выполняется производителем для собственной выпущенной продукции.
/// После успешного вызова упаковки получают состояние <see cref="ProductState.Production"/>.
/// </para>
/// <para>
/// Требуется роль <c>ApiProductionDeclaration</c>.
/// </para>
/// </remarks>
public sealed class ProductionDeclarationRequest
{
    /// <summary>
    /// GTIN препарата (14 знаков).
    /// </summary>
    /// <remarks>Необязательное на уровне контракта поле, но фактически обязательное для успешной операции.</remarks>
    [JsonPropertyName("gtin")]
    public string? Gtin { get; set; }

    /// <summary>
    /// Номер партии (серии) препарата.
    /// </summary>
    /// <remarks>Необязательное на уровне контракта поле, но фактически обязательное.</remarks>
    [JsonPropertyName("batchNo")]
    public string? BatchNo { get; set; }

    /// <summary>
    /// Дата истечения срока годности.
    /// </summary>
    /// <remarks>Сериализуется в строку формата <c>yyyy-MM-dd</c>.</remarks>
    [JsonPropertyName("expirationDate")]
    [JsonConverter(typeof(DateOnlyJsonConverter))]
    public DateTimeOffset ExpirationDate { get; set; }

    /// <summary>
    /// Цена за единицу препарата.
    /// </summary>
    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    /// <summary>
    /// Дата производства.
    /// </summary>
    /// <remarks>Сериализуется в строку формата <c>yyyy-MM-dd</c>.</remarks>
    [JsonPropertyName("productionDate")]
    [JsonConverter(typeof(DateOnlyJsonConverter))]
    public DateTimeOffset ProductionDate { get; set; }

    /// <summary>
    /// Дата документа-основания.
    /// </summary>
    /// <remarks>Сериализуется в строку формата <c>yyyy-MM-dd</c>.</remarks>
    [JsonPropertyName("documentDate")]
    [JsonConverter(typeof(DateOnlyJsonConverter))]
    public DateTimeOffset DocumentDate { get; set; }

    /// <summary>
    /// Номер документа-основания.
    /// </summary>
    [JsonPropertyName("documentNo")]
    public string? DocumentNo { get; set; }

    /// <summary>
    /// Дата самой декларации.
    /// </summary>
    /// <remarks>Сериализуется в строку формата <c>yyyy-MM-dd</c>.</remarks>
    [JsonPropertyName("declarationDate")]
    [JsonConverter(typeof(DateOnlyJsonConverter))]
    public DateTimeOffset DeclarationDate { get; set; }

    /// <summary>
    /// Идентификатор типа QR-кода (см. <c>GET /api/TrackAndTrace/GetSupportedQRTypes</c>).
    /// </summary>
    [JsonPropertyName("qrTypeId")]
    public int QrTypeId { get; set; }

    /// <summary>
    /// Список QR-кодов выпускаемой партии.
    /// </summary>
    [JsonPropertyName("qrCodes")]
    public IList<string>? QrCodes { get; set; }

    /// <summary>
    /// Произвольное текстовое описание декларации.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Код стейкхолдера-производителя.
    /// </summary>
    /// <remarks>Необязательное поле. Список кодов: <c>GET /api/TrackAndTrace/GetAllStakeholders</c>.</remarks>
    [JsonPropertyName("stakeholderCode")]
    public long? StakeholderCode { get; set; }
}

/// <summary>
/// Запрос <c>POST /api/TrackAndTrace/TransferDeclaration</c> — декларация перемещения упаковок
/// от одного стейкхолдера другому.
/// </summary>
/// <remarks>
/// <para>
/// Создаёт перемещение в состоянии <see cref="TransferState.Initiated"/>.
/// Получатель должен подтвердить приём методом <c>TransferAccept</c>, иначе упаковки
/// останутся в состоянии <see cref="ProductState.TransferInitiated"/>.
/// </para>
/// <para>Требуется роль <c>ApiTransferDeclaration</c>.</para>
/// </remarks>
public sealed class TransferDeclarationRequest
{
    /// <summary>
    /// Код стейкхолдера-отправителя (кто передаёт упаковки).
    /// </summary>
    /// <remarks>Обязательное поле. Список кодов: <c>GET /api/TrackAndTrace/GetAllStakeholders</c>.</remarks>
    [JsonPropertyName("fromStakeholder")]
    public long FromStakeholder { get; set; }

    /// <summary>
    /// Код стейкхолдера-получателя (кто принимает упаковки).
    /// </summary>
    /// <remarks>Обязательное поле. Список кодов: <c>GET /api/TrackAndTrace/GetAllStakeholders</c>.</remarks>
    [JsonPropertyName("toStakeholder")]
    public long ToStakeholder { get; set; }

    /// <summary>
    /// Дата декларации перемещения.
    /// </summary>
    /// <remarks>Обязательное поле. Сериализуется в строку формата <c>yyyy-MM-dd</c>.</remarks>
    [JsonPropertyName("declarationDate")]
    [JsonConverter(typeof(DateOnlyJsonConverter))]
    public DateTimeOffset DeclarationDate { get; set; }

    /// <summary>
    /// Номер сопроводительного документа.
    /// </summary>
    /// <remarks>Необязательное поле.</remarks>
    [JsonPropertyName("documentNo")]
    public string? DocumentNo { get; set; }

    /// <summary>
    /// Дата сопроводительного документа.
    /// </summary>
    /// <remarks>Необязательное поле. Сериализуется в строку формата <c>yyyy-MM-dd</c>.</remarks>
    [JsonPropertyName("documentDate")]
    [JsonConverter(typeof(NullableDateOnlyJsonConverter))]
    public DateTimeOffset? DocumentDate { get; set; }

    /// <summary>
    /// Произвольное текстовое описание перемещения.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Перечень передаваемых упаковок.
    /// </summary>
    /// <remarks>Каждый элемент содержит QR-код упаковки и её цену.</remarks>
    [JsonPropertyName("details")]
    public IList<TransferDeclarationDetail>? Details { get; set; }
}

/// <summary>
/// Элемент списка перемещаемых упаковок в <see cref="TransferDeclarationRequest"/>.
/// </summary>
public sealed class TransferDeclarationDetail
{
    /// <summary>
    /// QR-код (содержимое Data Matrix) перемещаемой упаковки.
    /// </summary>
    [JsonPropertyName("qrCode")]
    public string? QrCode { get; set; }

    /// <summary>
    /// Цена упаковки, по которой она передаётся получателю.
    /// </summary>
    [JsonPropertyName("price")]
    public decimal Price { get; set; }
}

/// <summary>
/// Запрос <c>POST /api/TrackAndTrace/TransferAccept</c> — подтверждение приёма перемещения получателем.
/// </summary>
/// <remarks>Требуется роль <c>ApiTransferAccept</c>.</remarks>
public sealed class TransferAcceptRequest
{
    /// <summary>
    /// Идентификатор подтверждаемой декларации перемещения.
    /// </summary>
    /// <remarks>Обязательное поле. Значение берётся из ответа <c>TransferDeclaration</c>.</remarks>
    [JsonPropertyName("declarationId")]
    public long DeclarationId { get; set; }
}

/// <summary>
/// Запрос отмены перемещения.
/// </summary>
/// <remarks>
/// Используется методами:
/// <list type="bullet">
///   <item><description><c>POST /api/TrackAndTrace/TransferCancel</c> — отмена исходящего перемещения;</description></item>
///   <item><description><c>POST /api/TrackAndTrace/TransferReturnCancel</c> — отмена возвратного перемещения.</description></item>
/// </list>
/// Роли: <c>ApiTransferCancel</c> и <c>ApiTransferReturnCancel</c> соответственно.
/// </remarks>
public sealed class TransferCancelRequest
{
    /// <summary>
    /// Идентификатор отменяемой декларации перемещения.
    /// </summary>
    /// <remarks>Обязательное поле.</remarks>
    [JsonPropertyName("declarationId")]
    public long DeclarationId { get; set; }
}

/// <summary>
/// Запрос <c>POST /api/TrackAndTrace/TransferReturn</c> — возврат упаковок предыдущему держателю
/// (возвратное перемещение).
/// </summary>
/// <remarks>
/// <para>
/// Создаёт перемещение с признаком <c>isReturn = true</c>. Отправителем и получателем
/// сервер определяет участников автоматически по истории движения упаковок.
/// </para>
/// <para>Требуется роль <c>ApiTransferReturn</c>.</para>
/// </remarks>
public sealed class TransferReturnRequest
{
    /// <summary>
    /// Номер сопроводительного документа возврата.
    /// </summary>
    [JsonPropertyName("documentNo")]
    public string? DocumentNo { get; set; }

    /// <summary>
    /// Дата сопроводительного документа возврата.
    /// </summary>
    /// <remarks>Сериализуется в строку формата <c>yyyy-MM-dd</c>.</remarks>
    [JsonPropertyName("documentDate")]
    [JsonConverter(typeof(NullableDateOnlyJsonConverter))]
    public DateTimeOffset? DocumentDate { get; set; }

    /// <summary>
    /// Произвольное текстовое описание возврата.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Перечень возвращаемых упаковок.
    /// </summary>
    [JsonPropertyName("details")]
    public IList<TransferReturnRequestDetail>? Details { get; set; }
}

/// <summary>
/// Элемент списка возвращаемых упаковок в <see cref="TransferReturnRequest"/>.
/// </summary>
public sealed class TransferReturnRequestDetail
{
    /// <summary>
    /// QR-код (содержимое Data Matrix) возвращаемой упаковки.
    /// </summary>
    [JsonPropertyName("qrCode")]
    public string? QrCode { get; set; }
}
