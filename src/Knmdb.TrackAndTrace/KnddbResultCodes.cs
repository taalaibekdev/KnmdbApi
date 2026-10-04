namespace Knmdb.TrackAndTrace;

/// <summary>
/// Известные коды результата (<c>resultCode</c>) сервера KNMDB.
/// </summary>
/// <remarks>
/// <para>
/// <b>Важно понимать.</b> Сервер KNMDB возвращает конверт
/// <c>{ "resultCode": …, "resultMessage": …, "actionResult": … }</c> и при успехе,
/// и при ошибке бизнес-логики. При ошибке бизнес-логики HTTP-статус остаётся
/// <c>200 OK</c> — отличить успех от ошибки по статусу невозможно, нужно смотреть
/// на <see cref="KnddbApiException.ResultCode"/>.
/// </para>
/// <para>
/// SDK разбирает конверт автоматически: при <c>resultCode = 0</c> возвращает
/// содержимое <c>actionResult</c> как результат метода, а при любом другом
/// значении выбрасывает <see cref="KnddbApiException"/> с заполненными
/// <see cref="KnddbApiException.ResultCode"/> и
/// <see cref="KnddbApiException.ResultMessage"/>.
/// </para>
/// </remarks>
public static class KnddbResultCodes
{
    /// <summary>Успешное выполнение: <c>0</c>.</summary>
    public const int Success = 0;

    /// <summary>Ошибка валидации входных данных: <c>1</c>.</summary>
    public const int ValidationError = 1;

    /// <summary>Неожиданная ошибка на сервере: <c>2</c>.</summary>
    public const int UnexpectedError = 2;

    // --- Track and Trace: ввод в оборот -----------------------------------

    /// <summary>В списке больше одного одинакового QR-кода: <c>6003</c>.</summary>
    public const int SimilarQrCode = 6003;

    /// <summary>Такой QR-код уже есть в системе: <c>6004</c>.</summary>
    public const int SimilarQrCodeInDatabase = 6004;

    /// <summary>В списке разные номера партий: <c>6005</c>.</summary>
    public const int DifferentBatchNo = 6005;

    /// <summary>В списке разные даты истечения срока годности: <c>6006</c>.</summary>
    public const int DifferentExpirationDate = 6006;

    /// <summary>Дата производства позже даты декларации: <c>6010</c>.</summary>
    public const int ProductionDateAfterDeclaration = 6010;

    /// <summary>Подходящих препаратов в списке нет: <c>6012</c>.</summary>
    public const int NoSuitableProducts = 6012;

    /// <summary>Серийные номера уже объявлены: <c>6021</c>.</summary>
    public const int SerialNumbersAlreadyDeclared = 6021;

    /// <summary>
    /// Упаковка с указанным QR-кодом не найдена: <c>6022</c>.
    /// </summary>
    /// <remarks>
    /// Типичная причина — QR-код не зарегистрирован, опечатка при вводе или
    /// упаковка относится к другой организации.
    /// </remarks>
    public const int ProductWithQrCodeNotFound = 6022;

    /// <summary>
    /// Упаковка с указанным QR-кодом не подходит для продажи: <c>6023</c>.
    /// </summary>
    /// <remarks>
    /// Типичная причина — повторная продажа уже проданной упаковки,
    /// либо упаковка деактивирована или просрочена.
    /// </remarks>
    public const int ProductWithQrCodeNotSuitableForSale = 6023;

    /// <summary>QR-код не соответствует модели: <c>6024</c>.</summary>
    public const int QrCodeNotSuitableToModel = 6024;

    /// <summary>GTIN в QR-кодах отличается от GTIN в запросе: <c>6025</c>.</summary>
    public const int QrCodeGtinMismatch = 6025;

    /// <summary>Дата годности в QR-кодах отличается от даты в запросе: <c>6026</c>.</summary>
    public const int QrCodeExpirationDateMismatch = 6026;

    /// <summary>Номер партии в QR-кодах отличается от номера в запросе: <c>6027</c>.</summary>
    public const int QrCodeBatchNumberMismatch = 6027;

    /// <summary>Препарат не подходит для частичной продажи: <c>6028</c>.</summary>
    public const int MedicineNotSuitableForPartialSale = 6028;

    /// <summary>Количество при частичной продаже должно быть больше нуля: <c>6029</c>.</summary>
    public const int PartialSaleAmountMustBePositive = 6029;

    /// <summary>Недостаточно количества для частичной продажи: <c>6030</c>.</summary>
    public const int NotEnoughAmountForPartialSale = 6030;

    /// <summary>Препарат должен продаваться частично: <c>6031</c>.</summary>
    public const int MustBePartiallySold = 6031;

    /// <summary>QR-код не удалось разобрать: <c>6032</c>.</summary>
    public const int QrCodeCouldNotBeParsed = 6032;

    /// <summary>Упаковка уже объявлена ранее: <c>6033</c>.</summary>
    public const int ProductAlreadyDeclared = 6033;

    /// <summary>Декларация принадлежит другой организации: <c>6034</c>.</summary>
    public const int DeclarationDoesntBelongToYou = 6034;

    /// <summary>Декларацию нельзя отменить: детали уже обработаны: <c>6035</c>.</summary>
    public const int DeclarationCannotBeCancelled = 6035;

    /// <summary>Записей для этой модели QR-кода не найдено: <c>6036</c>.</summary>
    public const int QrCodeModelNotFound = 6036;

    /// <summary>Организация не является участником Track and Trace: <c>6037</c>.</summary>
    public const int OrganizationIsNotTrackAndTraceStakeholder = 6037;

    /// <summary>Импортёр с указанным ИНН не найден: <c>6038</c>.</summary>
    public const int ImporterWithTaxNumberNotFound = 6038;

    /// <summary>Упаковка с такой парой GTIN и серийного номера не найдена: <c>6039</c>.</summary>
    public const int ProductWithSerialNumberNotFound = 6039;

    /// <summary>Склад не принадлежит вашей организации: <c>6040</c>.</summary>
    public const int WarehouseDoesntBelongToYourOrganization = 6040;

    /// <summary>Список деталей перемещения пуст: <c>6042</c>.</summary>
    public const int TransferDeclarationDetailsIsEmpty = 6042;

    /// <summary>В деактивации должны быть упаковки одного препарата: <c>6043</c>.</summary>
    public const int DeactivateDeclarationProductsMustBeSame = 6043;

    /// <summary>Список деталей деактивации пуст: <c>6044</c>.</summary>
    public const int DeactivateDeclarationDetailsIsEmpty = 6044;

    /// <summary>Упаковку нельзя деактивировать: она продана: <c>6045</c>.</summary>
    public const int ProductCannotBeDeactivated = 6045;

    /// <summary>Продажа этой упаковки уже отменена: <c>6046</c>.</summary>
    public const int ProductAlreadySalesCancelled = 6046;

    /// <summary>Перемещение не в состоянии «инициировано»: <c>6047</c>.</summary>
    public const int TransferDeclarationIsNotInitiated = 6047;

    /// <summary>QR-код содержит символы, отличные от ASCII: <c>6048</c>.</summary>
    public const int QrCodeNonAsciiCharFound = 6048;

    /// <summary>Ошибка разбора QR-кода: <c>6049</c>.</summary>
    public const int QrCodeParseError = 6049;

    /// <summary>Упаковка деактивирована: <c>6050</c>.</summary>
    public const int ProductIsDeactivated = 6050;

    /// <summary>Упаковка не деактивирована: <c>6051</c>.</summary>
    public const int ProductIsNotDeactivated = 6051;

    /// <summary>Длина ПИН гражданина превышает 24 символа: <c>6052</c>.</summary>
    public const int CitizenNumberTooLong = 6052;

    // --- Track and Trace: рецепты и аптеки -------------------------------

    /// <summary>Рецепт не найден: <c>6100</c>.</summary>
    public const int PrescriptionNotFound = 6100;

    /// <summary>У пользователя не заполнен ПИН: <c>6101</c>.</summary>
    public const int UserNationalIdentityNotFound = 6101;

    /// <summary>У пользователя нет аптеки или номера лицензии: <c>6102</c>.</summary>
    public const int PharmacyLicenseNotFound = 6102;

    /// <summary>Реализация по рецепту не удалась: <c>6103</c>.</summary>
    public const int SalesDeclarationFailed = 6103;

    /// <summary>Отмена реализации по рецепту не удалась: <c>6104</c>.</summary>
    public const int SalesCancelDeclarationFailed = 6104;

    /// <summary>Пользователь должен быть привязан к аптеке: <c>6105</c>.</summary>
    public const int UserMustBelongPharmacy = 6105;

    /// <summary>Пользователь должен быть привязан к аптеке или больнице: <c>6106</c>.</summary>
    public const int UserMustBelongPharmacyOrHospital = 6106;

    /// <summary>
    /// Возвращает понятное описание известного кода результата на русском языке.
    /// </summary>
    /// <param name="resultCode">Код результата из конверта ответа.</param>
    /// <returns>
    /// Описание причины; для неизвестного кода — <see langword="null"/>
    /// (используйте <see cref="KnddbApiException.ResultMessage"/> от сервера).
    /// </returns>
    /// <remarks>
    /// Соответствие кодов и сообщений — из справочника <c>KNDDBMessages</c>
    /// сервера KNMDB. Сообщения сервера англоязычные, описания ниже — русские,
    /// их удобно показывать оператору.
    /// </remarks>
    public static string? Describe(int? resultCode) => resultCode switch
    {
        Success => "Операция выполнена успешно.",
        ValidationError => "Ошибка проверки входных данных.",
        UnexpectedError => "Внутренняя ошибка сервера KNMDB. Обратитесь в поддержку.",

        SimilarQrCode => "В списке есть повторяющиеся QR-коды.",
        SimilarQrCodeInDatabase => "Один из QR-кодов уже зарегистрирован в системе.",
        DifferentBatchNo => "В списке QR-кодов разные номера партий.",
        DifferentExpirationDate => "В списке QR-кодов разные даты истечения срока годности.",
        ProductionDateAfterDeclaration => "Дата производства позже даты декларации.",
        NoSuitableProducts => "В списке нет препаратов, подходящих для этой операции.",
        SerialNumbersAlreadyDeclared => "Эти серийные номера уже объявлены ранее.",

        ProductWithQrCodeNotFound => "Упаковка с таким QR-кодом не найдена в системе.",
        ProductWithQrCodeNotSuitableForSale =>
            "Упаковка не подходит для продажи: возможно, она уже продана, деактивирована или просрочена.",
        QrCodeNotSuitableToModel => "QR-код не соответствует выбранной модели (типу) кода.",
        QrCodeGtinMismatch => "GTIN внутри QR-кода не совпадает с GTIN в запросе.",
        QrCodeExpirationDateMismatch => "Дата годности внутри QR-кода не совпадает с датой в запросе.",
        QrCodeBatchNumberMismatch => "Номер партии внутри QR-кода не совпадает с номером в запросе.",

        MedicineNotSuitableForPartialSale => "Этот препарат не подлежит частичной продаже.",
        PartialSaleAmountMustBePositive => "Количество при частичной продаже должно быть больше нуля.",
        NotEnoughAmountForPartialSale => "В упаковке недостаточно препарата для частичной продажи.",
        MustBePartiallySold => "Этот препарат должен продаваться частично.",

        QrCodeCouldNotBeParsed => "QR-код не удалось разобрать. Проверьте формат кода.",
        QrCodeNonAsciiCharFound => "QR-код содержит недопустимые символы.",
        QrCodeParseError => "Ошибка разбора QR-кода.",
        ProductAlreadyDeclared => "Эта упаковка уже объявлена ранее.",
        QrCodeModelNotFound => "Модель QR-кода не найдена. Уточните значение qrTypeId.",
        ProductWithSerialNumberNotFound =>
            "Упаковка с такой парой «GTIN + серийный номер» не найдена.",
        OrganizationIsNotTrackAndTraceStakeholder =>
            "Организация не является участником системы прослеживаемости.",
        ImporterWithTaxNumberNotFound => "Импортёр с указанным ИНН не найден.",

        DeclarationDoesntBelongToYou => "Декларация принадлежит другой организации.",
        DeclarationCannotBeCancelled =>
            "Декларацию нельзя отменить: её позиции уже обработаны.",
        WarehouseDoesntBelongToYourOrganization => "Склад не принадлежит вашей организации.",
        TransferDeclarationDetailsIsEmpty => "Список упаковок для перемещения пуст.",
        TransferDeclarationIsNotInitiated =>
            "Перемещение уже не в состоянии «инициировано» — изменить его нельзя.",

        DeactivateDeclarationProductsMustBeSame =>
            "В одной деактивации должны быть упаковки одного препарата.",
        DeactivateDeclarationDetailsIsEmpty => "Список упаковок для деактивации пуст.",
        ProductCannotBeDeactivated => "Упаковку нельзя деактивировать: она продана.",
        ProductAlreadySalesCancelled => "Продажа этой упаковки уже отменена.",
        ProductIsDeactivated => "Упаковка уже деактивирована.",
        ProductIsNotDeactivated => "Упаковка не деактивирована.",

        CitizenNumberTooLong => "Длина ПИН гражданина превышает 24 символа.",
        PrescriptionNotFound => "Рецепт не найден.",
        UserNationalIdentityNotFound => "У пользователя не заполнен ПИН.",
        PharmacyLicenseNotFound => "У пользователя нет привязки к аптеке или номера лицензии.",
        SalesDeclarationFailed => "Реализация по рецепту не удалась.",
        SalesCancelDeclarationFailed => "Отмена реализации по рецепту не удалась.",
        UserMustBelongPharmacy => "Пользователь должен быть привязан к аптеке.",
        UserMustBelongPharmacyOrHospital =>
            "Пользователь должен быть привязан к аптеке или больнице.",

        _ => null,
    };
}
