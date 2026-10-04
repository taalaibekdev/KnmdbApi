import '../../json.dart';
import '../enums/consumption_type.dart';
import '../enums/product_state.dart';

/// Результат проверки лекарственного средства — ответ методов
/// `productInquiryByQrCode` и `productInquiryByGtinSn`.
///
/// Оба метода доступны без авторизации и предназначены для приложений
/// потребителей («проверить лекарство»). Ответ содержит полную картину
/// по упаковке: срок годности, текущего держателя, историю движения, признаки
/// приостановки или отзыва и сведения о препарате.
class ProductInquiryResult {
  /// Создаёт результат.
  const ProductInquiryResult({
    required this.productBoxId,
    required this.productPackageItemId,
    required this.productionDate,
    required this.expirationDate,
    required this.isExpired,
    required this.isAvailableForSale,
    required this.isSuspendedOrRecalled,
    required this.productStatus,
    required this.productState,
    this.productName,
    this.gtin,
    this.serialNumber,
    this.batchNumber,
    this.qrCode,
    this.stakeHolderName,
    this.stakeholderTaxNumber,
    this.suspendRecallInfo,
    this.productInquiryHistory,
    this.overallRetailPrice,
    this.certificateNumber,
    this.instructionForUse,
    this.instructionForUseDocId,
    this.packagingImageDocId,
    this.isFomsDrug,
    this.compensation,
    this.consumptionType,
    this.manufacturerName,
    this.partialSaleRemainingAmount,
  });

  /// Разбирает результат из JSON.
  factory ProductInquiryResult.fromJson(Map<String, dynamic> json) {
    return ProductInquiryResult(
      productBoxId: asInt(json['productBoxId']) ?? 0,
      productPackageItemId: json['productPackageItemId'] as String? ?? '',
      productionDate:
          asDateTime(json['productionDate']) ?? DateTime.now().toUtc(),
      expirationDate:
          asDateTime(json['expirationDate']) ?? DateTime.now().toUtc(),
      isExpired: asBool(json['isExpired']) ?? false,
      isAvailableForSale: asBool(json['isAvailableForSale']) ?? false,
      isSuspendedOrRecalled: asBool(json['isSuspendedOrRecalled']) ?? false,
      productStatus: ProductStatus.fromWire(json['productStatus']) ??
          ProductStatus.stockOut,
      productState: ProductState.fromWire(json['productState']) ??
          ProductState.deactivation,
      productName: json['productName'] as String?,
      gtin: json['gtin'] as String?,
      serialNumber: json['serialNumber'] as String?,
      batchNumber: json['batchNumber'] as String?,
      qrCode: json['qrCode'] as String?,
      stakeHolderName: json['stakeHolderName'] as String?,
      stakeholderTaxNumber: json['stakeholderTaxNumber'] as String?,
      suspendRecallInfo: json['suspendRecallInfo'] == null
          ? null
          : SuspendRecallInfo.fromJson(asMap(json['suspendRecallInfo'])!),
      productInquiryHistory: asMapList(json['productInquiryHistory'])
          ?.map(ProductInquiryHistory.fromJson)
          .toList(growable: false),
      overallRetailPrice: json['overallRetailPrice'] as String?,
      certificateNumber: json['certificateNumber'] as String?,
      instructionForUse: json['instructionForUse'] as String?,
      instructionForUseDocId: json['instructionForUseDocId'] as String?,
      packagingImageDocId: json['packagingImageDocId'] as String?,
      isFomsDrug: asBool(json['isFomsDrug']),
      compensation: json['compensation'] as String?,
      consumptionType: ConsumptionType.fromWire(json['consumptionType']),
      manufacturerName: json['manufacturerName'] as String?,
      partialSaleRemainingAmount: asDouble(json['partialSaleRemainingAmount']),
    );
  }

  /// Внутренний идентификатор упаковки в системе.
  final int productBoxId;

  /// Идентификатор позиции упаковки препарата.
  final String productPackageItemId;

  /// Дата производства упаковки.
  final DateTime productionDate;

  /// Дата истечения срока годности упаковки.
  final DateTime expirationDate;

  /// Признак того, что срок годности упаковки истёк на момент запроса.
  final bool isExpired;

  /// Признак того, что упаковка доступна для продажи: не выбыла, не
  /// приостановлена, не просрочена.
  final bool isAvailableForSale;

  /// Признак того, что упаковка приостановлена или отозвана из обращения.
  final bool isSuspendedOrRecalled;

  /// Складской статус упаковки.
  final ProductStatus productStatus;

  /// Последнее событие, произошедшее с упаковкой.
  final ProductState productState;

  /// Наименование препарата.
  final String? productName;

  /// GTIN препарата (14 знаков).
  final String? gtin;

  /// Серийный номер упаковки.
  final String? serialNumber;

  /// Номер партии упаковки.
  final String? batchNumber;

  /// QR-код (содержимое Data Matrix) упаковки.
  final String? qrCode;

  /// Наименование организации, у которой упаковка находится сейчас.
  final String? stakeHolderName;

  /// ИНН организации, у которой упаковка находится сейчас.
  ///
  /// Присутствует в фактическом ответе API, хотя отсутствует
  /// в опубликованной OpenAPI-схеме.
  final String? stakeholderTaxNumber;

  /// Сведения о приостановке или отзыве упаковки, если применимо.
  final SuspendRecallInfo? suspendRecallInfo;

  /// История движения упаковки: кто, когда и по какой декларации её держал.
  final List<ProductInquiryHistory>? productInquiryHistory;

  /// Предельная розничная цена препарата (в виде строки).
  final String? overallRetailPrice;

  /// Номер регистрационного удостоверения (сертификата) препарата.
  final String? certificateNumber;

  /// Инструкция по применению препарата.
  final String? instructionForUse;

  /// Идентификатор бинарного объекта с инструкцией по применению.
  final String? instructionForUseDocId;

  /// Идентификатор бинарного объекта с изображением упаковки.
  final String? packagingImageDocId;

  /// Признак того, что препарат входит в перечень льготного обеспечения.
  final bool? isFomsDrug;

  /// Размер компенсации (в виде строки).
  final String? compensation;

  /// Тип расходной операции, по которой упаковка выбыла из оборота.
  ///
  /// `null`, если упаковка ещё в обороте.
  final ConsumptionType? consumptionType;

  /// Наименование производителя препарата.
  final String? manufacturerName;

  /// Остаток препарата в упаковке после частичной продажи.
  ///
  /// Заполняется, если по упаковке была операция частичной продажи.
  final double? partialSaleRemainingAmount;

  /// Краткий вердикт для интерфейса приложения потребителя.
  ///
  /// Возвращает одно из значений:
  /// - `recalled` — упаковка отозвана или приостановлена (критично);
  /// - `expired` — истёк срок годности;
  /// - `notForSale` — упаковка уже выбыла из оборота;
  /// - `ok` — упаковка легальна и доступна к продаже.
  String get verificationVerdict {
    if (isSuspendedOrRecalled) {
      return 'recalled';
    }
    if (isExpired) {
      return 'expired';
    }
    if (!isAvailableForSale) {
      return 'notForSale';
    }
    return 'ok';
  }

  /// Причина вердикта [verificationVerdict] на русском языке.
  String get verificationMessage {
    switch (verificationVerdict) {
      case 'recalled':
        final reason = suspendRecallInfo?.reason;
        return reason == null || reason.isEmpty
            ? 'Упаковка отозвана или приостановлена'
            : 'Упаковка отозвана: $reason';
      case 'expired':
        return 'Срок годности истёк';
      case 'notForSale':
        return 'Упаковка выбыла из оборота';
      case 'ok':
        return 'Упаковка легальна и доступна к продаже';
      default:
        return '';
    }
  }
}

/// Сведения о приостановке или отзыве упаковки из обращения.
class SuspendRecallInfo {
  /// Создаёт сведения.
  const SuspendRecallInfo({this.startDate, this.endDate, this.reason});

  /// Разбирает сведения из JSON.
  factory SuspendRecallInfo.fromJson(Map<String, dynamic> json) {
    return SuspendRecallInfo(
      startDate: asDateTime(json['startDate']),
      endDate: asDateTime(json['endDate']),
      reason: json['reason'] as String?,
    );
  }

  /// Дата начала периода приостановки или отзыва.
  final DateTime? startDate;

  /// Дата окончания периода; `null` — ограничение бессрочное.
  final DateTime? endDate;

  /// Причина приостановки или отзыва.
  final String? reason;
}

/// Одно событие в истории движения упаковки.
class ProductInquiryHistory {
  /// Создаёт событие.
  const ProductInquiryHistory({
    required this.declarationNumber,
    required this.state,
    required this.stateDate,
    required this.price,
    this.stakeHolder,
    this.partialSaleAmount,
  });

  /// Разбирает событие из JSON.
  factory ProductInquiryHistory.fromJson(Map<String, dynamic> json) {
    return ProductInquiryHistory(
      declarationNumber: asInt(json['declarationNumber']) ?? 0,
      state: ProductState.fromWire(json['state']) ?? ProductState.stock,
      stateDate: asDateTime(json['stateDate']) ?? DateTime.now().toUtc(),
      price: asDouble(json['price']) ?? 0,
      stakeHolder: json['stakeHolder'] as String?,
      partialSaleAmount: asDouble(json['partialSaleAmount']),
    );
  }

  /// Номер декларации, по которой произошло событие.
  final int declarationNumber;

  /// Состояние упаковки, установленное данным событием.
  final ProductState state;

  /// Дата и время события.
  final DateTime stateDate;

  /// Цена упаковки на момент события.
  final double price;

  /// Наименование организации, у которой находилась упаковка.
  final String? stakeHolder;

  /// Количество, реализованное при частичной продаже в рамках события.
  final double? partialSaleAmount;
}

/// Ответ `POST /api/TrackAndTrace/GetPartialSaleInfo` — сведения о частично
/// проданной упаковке.
///
/// В опубликованной OpenAPI-схеме для этого метода ошибочно указан тип
/// `GetStockInheldListByGtinResponse`; фактический ответ имеет эту форму.
class GetPartialSaleInfoResponse {
  /// Создаёт ответ.
  const GetPartialSaleInfoResponse({
    required this.partialSaleAmount,
    this.gtin,
    this.fullBrandName,
    this.serialNumber,
  });

  /// Разбирает ответ из JSON.
  factory GetPartialSaleInfoResponse.fromJson(Map<String, dynamic> json) {
    return GetPartialSaleInfoResponse(
      partialSaleAmount: asDouble(json['partialSaleAmount']) ?? 0,
      gtin: json['gtin'] as String?,
      fullBrandName: json['fullBrandName'] as String?,
      serialNumber: json['serialNumber'] as String?,
    );
  }

  /// Количество препарата, уже реализованное при частичной продаже.
  final double partialSaleAmount;

  /// GTIN препарата.
  final String? gtin;

  /// Полное торговое наименование препарата.
  final String? fullBrandName;

  /// Серийный номер упаковки.
  final String? serialNumber;
}

/// Ответ `POST /api/TrackAndTrace/GetMedicineList` — справочник лекарственных
/// средств.
class GetMedicineListResponse {
  /// Создаёт ответ.
  const GetMedicineListResponse(
      {required this.numberOfMedicines, this.medicineList});

  /// Разбирает ответ из JSON.
  factory GetMedicineListResponse.fromJson(Map<String, dynamic> json) {
    return GetMedicineListResponse(
      numberOfMedicines: asInt(json['numberOfMedicines']) ?? 0,
      medicineList: asMapList(json['medicineList'])
          ?.map(MedicineInfo.fromJson)
          .toList(growable: false),
    );
  }

  /// Общее количество препаратов в ответе.
  final int numberOfMedicines;

  /// Список препаратов.
  final List<MedicineInfo>? medicineList;
}

/// Сведения о лекарственном средстве в справочнике.
class MedicineInfo {
  /// Создаёт сведения.
  const MedicineInfo({
    required this.trackAndTraceStatus,
    required this.lastUpdate,
    this.gtin,
    this.brandName,
    this.fullBrandName,
    this.manufacturerCompany,
    this.country,
    this.atcCode,
    this.formula,
    this.innList,
  });

  /// Разбирает сведения из JSON.
  factory MedicineInfo.fromJson(Map<String, dynamic> json) {
    return MedicineInfo(
      trackAndTraceStatus:
          TrackAndTraceStatus.fromWire(json['trackAndTraceStatus']) ??
              TrackAndTraceStatus.notTracked,
      lastUpdate: asDateTime(json['lastUpdate']) ?? DateTime.now().toUtc(),
      gtin: json['gtin'] as String?,
      brandName: json['brandName'] as String?,
      fullBrandName: json['fullBrandName'] as String?,
      manufacturerCompany: json['manufacturerCompany'] as String?,
      country: json['country'] as String?,
      atcCode: json['atcCode'] as String?,
      formula: json['formula'] as String?,
      innList: json['innList'] as String?,
    );
  }

  /// Статус обязательности маркировки и прослеживаемости препарата.
  final TrackAndTraceStatus trackAndTraceStatus;

  /// Дата и время последнего изменения карточки препарата.
  ///
  /// Используйте максимальное значение этого поля как `lastUpdate`
  /// в следующем запросе для инкрементальной синхронизации.
  final DateTime lastUpdate;

  /// GTIN препарата.
  final String? gtin;

  /// Торговое наименование препарата (краткое).
  final String? brandName;

  /// Полное торговое наименование препарата с дозировкой и формой выпуска.
  final String? fullBrandName;

  /// Наименование компании-производителя.
  final String? manufacturerCompany;

  /// Страна происхождения препарата.
  final String? country;

  /// Код АТХ (анатомо-терапевтическо-химическая классификация).
  final String? atcCode;

  /// Лекарственная форма или состав препарата.
  final String? formula;

  /// Перечень действующих веществ (МНН).
  final String? innList;
}
