/// Известные коды результата (`resultCode`) сервера KNMDB.
///
/// ## Почему это важно
///
/// Сервер KNMDB возвращает конверт
/// `{ "resultCode": …, "resultMessage": …, "actionResult": … }` и при успехе,
/// и при ошибке бизнес-логики. При ошибке бизнес-логики HTTP-статус остаётся
/// `200 OK` — отличить успех от ошибки по статусу невозможно, нужно смотреть
/// на [KnddbApiException.resultCode].
///
/// SDK разбирает конверт автоматически: при `resultCode = 0` возвращает
/// содержимое `actionResult` как результат метода, а при любом другом значении
/// выбрасывает [KnddbApiException] с заполненными `resultCode`
/// и `resultMessage`.
///
/// ```dart
/// try {
///   await client.salesDeclaration(request);
/// } on KnddbApiException catch (error) {
///   if (error.statusCode == 200 && error.isBusinessError) {
///     print('Бизнес-ошибка ${error.resultCode}: ${error.resultDescription}');
///   }
/// }
/// ```
abstract final class KnddbResultCodes {
  /// Успешное выполнение: `0`.
  static const int success = 0;

  /// Ошибка валидации входных данных: `1`.
  static const int validationError = 1;

  /// Неожиданная ошибка на сервере: `2`.
  static const int unexpectedError = 2;

  // --- Track and Trace: ввод в оборот -------------------------------------

  /// В списке больше одного одинакового QR-кода: `6003`.
  static const int similarQrCode = 6003;

  /// Такой QR-код уже есть в системе: `6004`.
  static const int similarQrCodeInDatabase = 6004;

  /// В списке разные номера партий: `6005`.
  static const int differentBatchNo = 6005;

  /// В списке разные даты истечения срока годности: `6006`.
  static const int differentExpirationDate = 6006;

  /// Дата производства позже даты декларации: `6010`.
  static const int productionDateAfterDeclaration = 6010;

  /// Подходящих препаратов в списке нет: `6012`.
  static const int noSuitableProducts = 6012;

  /// Серийные номера уже объявлены: `6021`.
  static const int serialNumbersAlreadyDeclared = 6021;

  /// Упаковка с указанным QR-кодом не найдена: `6022`.
  ///
  /// Типичная причина — QR-код не зарегистрирован, опечатка при вводе или
  /// упаковка относится к другой организации.
  static const int productWithQrCodeNotFound = 6022;

  /// Упаковка не подходит для продажи: `6023`.
  ///
  /// Типичная причина — повторная продажа уже проданной упаковки, либо
  /// упаковка деактивирована или просрочена.
  static const int productWithQrCodeNotSuitableForSale = 6023;

  /// QR-код не соответствует выбранной модели: `6024`.
  static const int qrCodeNotSuitableToModel = 6024;

  /// GTIN в QR-кодах отличается от GTIN в запросе: `6025`.
  static const int qrCodeGtinMismatch = 6025;

  /// Дата годности в QR-кодах отличается от даты в запросе: `6026`.
  static const int qrCodeExpirationDateMismatch = 6026;

  /// Номер партии в QR-кодах отличается от номера в запросе: `6027`.
  static const int qrCodeBatchNumberMismatch = 6027;

  /// Препарат не подходит для частичной продажи: `6028`.
  static const int medicineNotSuitableForPartialSale = 6028;

  /// Количество при частичной продаже должно быть больше нуля: `6029`.
  static const int partialSaleAmountMustBePositive = 6029;

  /// Недостаточно количества для частичной продажи: `6030`.
  static const int notEnoughAmountForPartialSale = 6030;

  /// Препарат должен продаваться частично: `6031`.
  static const int mustBePartiallySold = 6031;

  /// QR-код не удалось разобрать: `6032`.
  static const int qrCodeCouldNotBeParsed = 6032;

  /// Упаковка уже объявлена ранее: `6033`.
  static const int productAlreadyDeclared = 6033;

  /// Декларация принадлежит другой организации: `6034`.
  static const int declarationDoesntBelongToYou = 6034;

  /// Декларацию нельзя отменить: детали уже обработаны: `6035`.
  static const int declarationCannotBeCancelled = 6035;

  /// Записей для этой модели QR-кода не найдено: `6036`.
  static const int qrCodeModelNotFound = 6036;

  /// Организация не является участником Track and Trace: `6037`.
  static const int organizationIsNotTrackAndTraceStakeholder = 6037;

  /// Импортёр с указанным ИНН не найден: `6038`.
  static const int importerWithTaxNumberNotFound = 6038;

  /// Упаковка с такой парой GTIN и серийного номера не найдена: `6039`.
  static const int productWithSerialNumberNotFound = 6039;

  /// Склад не принадлежит вашей организации: `6040`.
  static const int warehouseDoesntBelongToYourOrganization = 6040;

  /// Список деталей перемещения пуст: `6042`.
  static const int transferDeclarationDetailsIsEmpty = 6042;

  /// В деактивации должны быть упаковки одного препарата: `6043`.
  static const int deactivateDeclarationProductsMustBeSame = 6043;

  /// Список деталей деактивации пуст: `6044`.
  static const int deactivateDeclarationDetailsIsEmpty = 6044;

  /// Упаковку нельзя деактивировать: она продана: `6045`.
  static const int productCannotBeDeactivated = 6045;

  /// Продажа этой упаковки уже отменена: `6046`.
  static const int productAlreadySalesCancelled = 6046;

  /// Перемещение не в состоянии «инициировано»: `6047`.
  static const int transferDeclarationIsNotInitiated = 6047;

  /// QR-код содержит символы, отличные от ASCII: `6048`.
  static const int qrCodeNonAsciiCharFound = 6048;

  /// Ошибка разбора QR-кода: `6049`.
  static const int qrCodeParseError = 6049;

  /// Упаковка деактивирована: `6050`.
  static const int productIsDeactivated = 6050;

  /// Упаковка не деактивирована: `6051`.
  static const int productIsNotDeactivated = 6051;

  /// Длина ПИН гражданина превышает 24 символа: `6052`.
  static const int citizenNumberTooLong = 6052;

  // --- Track and Trace: рецепты и аптеки ---------------------------------

  /// Рецепт не найден: `6100`.
  static const int prescriptionNotFound = 6100;

  /// У пользователя не заполнен ПИН: `6101`.
  static const int userNationalIdentityNotFound = 6101;

  /// У пользователя нет аптеки или номера лицензии: `6102`.
  static const int pharmacyLicenseNotFound = 6102;

  /// Реализация по рецепту не удалась: `6103`.
  static const int salesDeclarationFailed = 6103;

  /// Отмена реализации по рецепту не удалась: `6104`.
  static const int salesCancelDeclarationFailed = 6104;

  /// Пользователь должен быть привязан к аптеке: `6105`.
  static const int userMustBelongPharmacy = 6105;

  /// Пользователь должен быть привязан к аптеке или больнице: `6106`.
  static const int userMustBelongPharmacyOrHospital = 6106;

  /// Возвращает понятное описание известного кода результата на русском языке.
  ///
  /// Для неизвестного кода возвращает `null` — тогда используйте
  /// [KnddbApiException.resultMessage] от сервера.
  ///
  /// Соответствие кодов и сообщений — из справочника `KNDDBMessages` сервера
  /// KNMDB. Сообщения сервера англоязычные, описания ниже — русские, их удобно
  /// показывать оператору.
  static String? describe(int? resultCode) => switch (resultCode) {
        success => 'Операция выполнена успешно.',
        validationError => 'Ошибка проверки входных данных.',
        unexpectedError =>
          'Внутренняя ошибка сервера KNMDB. Обратитесь в поддержку.',
        similarQrCode => 'В списке есть повторяющиеся QR-коды.',
        similarQrCodeInDatabase =>
          'Один из QR-кодов уже зарегистрирован в системе.',
        differentBatchNo => 'В списке QR-кодов разные номера партий.',
        differentExpirationDate =>
          'В списке QR-кодов разные даты истечения срока годности.',
        productionDateAfterDeclaration =>
          'Дата производства позже даты декларации.',
        noSuitableProducts =>
          'В списке нет препаратов, подходящих для этой операции.',
        serialNumbersAlreadyDeclared =>
          'Эти серийные номера уже объявлены ранее.',
        productWithQrCodeNotFound =>
          'Упаковка с таким QR-кодом не найдена в системе.',
        productWithQrCodeNotSuitableForSale =>
          'Упаковка не подходит для продажи: возможно, она уже продана, '
              'деактивирована или просрочена.',
        qrCodeNotSuitableToModel =>
          'QR-код не соответствует выбранной модели (типу) кода.',
        qrCodeGtinMismatch =>
          'GTIN внутри QR-кода не совпадает с GTIN в запросе.',
        qrCodeExpirationDateMismatch =>
          'Дата годности внутри QR-кода не совпадает с датой в запросе.',
        qrCodeBatchNumberMismatch =>
          'Номер партии внутри QR-кода не совпадает с номером в запросе.',
        medicineNotSuitableForPartialSale =>
          'Этот препарат не подлежит частичной продаже.',
        partialSaleAmountMustBePositive =>
          'Количество при частичной продаже должно быть больше нуля.',
        notEnoughAmountForPartialSale =>
          'В упаковке недостаточно препарата для частичной продажи.',
        mustBePartiallySold => 'Этот препарат должен продаваться частично.',
        qrCodeCouldNotBeParsed =>
          'QR-код не удалось разобрать. Проверьте формат кода.',
        qrCodeNonAsciiCharFound => 'QR-код содержит недопустимые символы.',
        qrCodeParseError => 'Ошибка разбора QR-кода.',
        productAlreadyDeclared => 'Эта упаковка уже объявлена ранее.',
        qrCodeModelNotFound =>
          'Модель QR-кода не найдена. Уточните значение qrTypeId.',
        productWithSerialNumberNotFound =>
          'Упаковка с такой парой «GTIN + серийный номер» не найдена.',
        organizationIsNotTrackAndTraceStakeholder =>
          'Организация не является участником системы прослеживаемости.',
        importerWithTaxNumberNotFound => 'Импортёр с указанным ИНН не найден.',
        declarationDoesntBelongToYou =>
          'Декларация принадлежит другой организации.',
        declarationCannotBeCancelled =>
          'Декларацию нельзя отменить: её позиции уже обработаны.',
        warehouseDoesntBelongToYourOrganization =>
          'Склад не принадлежит вашей организации.',
        transferDeclarationDetailsIsEmpty =>
          'Список упаковок для перемещения пуст.',
        transferDeclarationIsNotInitiated =>
          'Перемещение уже не в состоянии «инициировано» — изменить его нельзя.',
        deactivateDeclarationProductsMustBeSame =>
          'В одной деактивации должны быть упаковки одного препарата.',
        deactivateDeclarationDetailsIsEmpty =>
          'Список упаковок для деактивации пуст.',
        productCannotBeDeactivated =>
          'Упаковку нельзя деактивировать: она продана.',
        productAlreadySalesCancelled => 'Продажа этой упаковки уже отменена.',
        productIsDeactivated => 'Упаковка уже деактивирована.',
        productIsNotDeactivated => 'Упаковка не деактивирована.',
        citizenNumberTooLong => 'Длина ПИН гражданина превышает 24 символа.',
        prescriptionNotFound => 'Рецепт не найден.',
        userNationalIdentityNotFound => 'У пользователя не заполнен ПИН.',
        pharmacyLicenseNotFound =>
          'У пользователя нет привязки к аптеке или номера лицензии.',
        salesDeclarationFailed => 'Реализация по рецепту не удалась.',
        salesCancelDeclarationFailed =>
          'Отмена реализации по рецепту не удалась.',
        userMustBelongPharmacy => 'Пользователь должен быть привязан к аптеке.',
        userMustBelongPharmacyOrHospital =>
          'Пользователь должен быть привязан к аптеке или больнице.',
        _ => null,
      };
}
