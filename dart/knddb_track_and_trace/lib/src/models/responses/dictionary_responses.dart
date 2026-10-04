import '../../json.dart';
import '../enums/product_state.dart';
import '../enums/stakeholder_type.dart';
import '../enums/transfer.dart';

/// Ответ `GET /api/TrackAndTrace/GetAllStakeholders` — список всех организаций,
/// зарегистрированных в системе.
class GetAllStakeholdersResponse {
  /// Создаёт ответ.
  const GetAllStakeholdersResponse({
    required this.numberOfStakeholders,
    this.stakeholders,
  });

  /// Разбирает ответ из JSON.
  factory GetAllStakeholdersResponse.fromJson(Map<String, dynamic> json) {
    return GetAllStakeholdersResponse(
      numberOfStakeholders: asInt(json['numberOfStakeholders']) ?? 0,
      stakeholders: asMapList(json['stakeholders'])
          ?.map(StakeholderInfo.fromJson)
          .toList(growable: false),
    );
  }

  /// Общее количество организаций в ответе.
  final int numberOfStakeholders;

  /// Список организаций.
  final List<StakeholderInfo>? stakeholders;
}

/// Сведения об организации-участнике оборота лекарственных средств.
class StakeholderInfo {
  /// Создаёт сведения.
  const StakeholderInfo({
    required this.code,
    required this.type,
    this.name,
    this.taxNumber,
    this.address,
    this.city,
    this.district,
    this.parentCode,
    this.parentName,
  });

  /// Разбирает сведения из JSON.
  factory StakeholderInfo.fromJson(Map<String, dynamic> json) {
    return StakeholderInfo(
      code: asInt(json['code']) ?? 0,
      type: StakeholderType.fromWire(json['type']) ?? StakeholderType.company,
      name: json['name'] as String?,
      taxNumber: json['taxNumber'] as String?,
      address: json['address'] as String?,
      city: json['city'] as String?,
      district: json['district'] as String?,
      parentCode: asInt(json['parentCode']),
      parentName: json['parentName'] as String?,
    );
  }

  /// Уникальный код организации в системе KNMDB.
  ///
  /// Именно это значение передаётся в поля `stakeholderCode`,
  /// `fromStakeholder`, `toStakeholder` других запросов.
  final int code;

  /// Тип организации.
  final StakeholderType type;

  /// Полное наименование организации.
  final String? name;

  /// ИНН (налоговый номер) организации.
  final String? taxNumber;

  /// Адрес организации (улица, дом).
  final String? address;

  /// Город организации.
  final String? city;

  /// Район (область) организации.
  final String? district;

  /// Код головной (родительской) организации, если текущая является филиалом.
  final int? parentCode;

  /// Наименование головной (родительской) организации.
  final String? parentName;
}

/// Ответ `GET /api/TrackAndTrace/GetSupportedQRTypes` — перечень поддерживаемых
/// типов QR-кодов.
///
/// **Устаревший тип.** Метод `getSupportedQrTypes` исключён из API департамента
/// лекарственных средств и будет удалён из SDK, когда окончательно исчезнет
/// из API. Новую интеграцию на него не закладывайте: значение `qrTypeId`
/// согласуйте с департаментом и задайте константой в конфигурации.
@Deprecated(
  'Метод GetSupportedQRTypes исключён из API департамента лекарственных средств. '
  'Тип будет удалён из SDK вместе с методом getSupportedQrTypes.',
)
class GetSupportedQRTypesResponse {
  /// Создаёт ответ.
  const GetSupportedQRTypesResponse({this.qrTypes});

  /// Разбирает ответ из JSON.
  factory GetSupportedQRTypesResponse.fromJson(Map<String, dynamic> json) {
    return GetSupportedQRTypesResponse(
      qrTypes: asMapList(json['qrTypes'])
          ?.map(QRTypeInfo.fromJson)
          .toList(growable: false),
    );
  }

  /// Список поддерживаемых типов QR-кодов.
  final List<QRTypeInfo>? qrTypes;
}

/// Описание одного типа QR-кода (формата Data Matrix).
///
/// **Устаревший тип.** Используется только методом `getSupportedQrTypes`,
/// который исключён из API департамента лекарственных средств.
@Deprecated(
  'Тип QR-кода используется только устаревшим методом getSupportedQrTypes. '
  'Тип будет удалён из SDK вместе с ним.',
)
class QRTypeInfo {
  /// Создаёт описание.
  const QRTypeInfo({required this.id, this.name, this.description});

  /// Разбирает описание из JSON.
  factory QRTypeInfo.fromJson(Map<String, dynamic> json) {
    return QRTypeInfo(
      id: asInt(json['id']) ?? 0,
      name: json['name'] as String?,
      description: json['description'] as String?,
    );
  }

  /// Числовой идентификатор типа QR-кода.
  ///
  /// Передаётся в поле `qrTypeId` запросов деклараций.
  final int id;

  /// Краткое наименование типа QR-кода.
  final String? name;

  /// Подробное описание типа: порядок и длина полей внутри Data Matrix.
  ///
  /// Пример для европейского типа:
  /// `01GTIN(14) 21SerialNumber(18) 17ExpiryDate(yymmdd)(6) 10BatchNumber(11)`.
  final String? description;
}

/// Ответ `GET /api/TrackAndTrace/GetTransferDeclaration` — полное содержимое
/// одной декларации перемещения, включая перечень упаковок.
class GetTransferDeclarationResponse {
  /// Создаёт ответ.
  const GetTransferDeclarationResponse({
    required this.declarationId,
    required this.fromStakeholder,
    required this.toStakeholder,
    required this.declarationDate,
    required this.currentState,
    required this.isReturn,
    this.documentNo,
    this.documentDate,
    this.description,
    this.details,
  });

  /// Разбирает ответ из JSON.
  factory GetTransferDeclarationResponse.fromJson(Map<String, dynamic> json) {
    return GetTransferDeclarationResponse(
      declarationId: asInt(json['declarationId']) ?? 0,
      fromStakeholder: asInt(json['fromStakeholder']) ?? 0,
      toStakeholder: asInt(json['toStakeholder']) ?? 0,
      declarationDate:
          asDateTime(json['declarationDate']) ?? DateTime.now().toUtc(),
      currentState: TransferState.fromWire(json['currentState']) ??
          TransferState.initiated,
      isReturn: asBool(json['isReturn']) ?? false,
      documentNo: json['documentNo'] as String?,
      documentDate: asDateTime(json['documentDate']),
      description: json['description'] as String?,
      details: asMapList(json['details'])
          ?.map(TransferDeclarationDetailItem.fromJson)
          .toList(growable: false),
    );
  }

  /// Идентификатор декларации перемещения.
  final int declarationId;

  /// Код стейкхолдера-отправителя.
  final int fromStakeholder;

  /// Код стейкхолдера-получателя.
  final int toStakeholder;

  /// Дата декларации перемещения.
  final DateTime declarationDate;

  /// Текущее состояние перемещения.
  final TransferState currentState;

  /// Признак возвратного перемещения.
  final bool isReturn;

  /// Номер сопроводительного документа.
  final String? documentNo;

  /// Дата сопроводительного документа.
  final DateTime? documentDate;

  /// Текстовое описание перемещения.
  final String? description;

  /// Перечень упаковок в составе перемещения.
  final List<TransferDeclarationDetailItem>? details;
}

/// Сведения об одной упаковке в составе декларации перемещения.
class TransferDeclarationDetailItem {
  /// Создаёт сведения.
  const TransferDeclarationDetailItem({
    required this.productBoxId,
    required this.drugPackageItemId,
    required this.price,
    this.fullBrandName,
    this.qrCode,
    this.gtin,
    this.batchNumber,
    this.expirationDate,
    this.serialNumber,
  });

  /// Разбирает сведения из JSON.
  factory TransferDeclarationDetailItem.fromJson(Map<String, dynamic> json) {
    return TransferDeclarationDetailItem(
      productBoxId: asInt(json['productBoxId']) ?? 0,
      drugPackageItemId: json['drugPackageItemId'] as String? ?? '',
      price: asDouble(json['price']) ?? 0,
      fullBrandName: json['fullBrandName'] as String?,
      qrCode: json['qrCode'] as String?,
      gtin: json['gtin'] as String?,
      batchNumber: json['batchNumber'] as String?,
      expirationDate: asDateTime(json['expirationDate']),
      serialNumber: json['serialNumber'] as String?,
    );
  }

  /// Внутренний идентификатор упаковки в системе.
  final int productBoxId;

  /// Идентификатор позиции упаковки препарата (`DrugPackageItem`).
  final String drugPackageItemId;

  /// Цена упаковки в составе перемещения.
  final double price;

  /// Полное торговое наименование препарата с дозировкой и формой выпуска.
  final String? fullBrandName;

  /// QR-код (содержимое Data Matrix) упаковки.
  final String? qrCode;

  /// GTIN препарата.
  final String? gtin;

  /// Номер партии упаковки.
  final String? batchNumber;

  /// Дата истечения срока годности упаковки.
  final DateTime? expirationDate;

  /// Серийный номер упаковки.
  final String? serialNumber;
}

/// Ответ `POST /api/TrackAndTrace/GetTransferListByFilter` — список деклараций
/// перемещения, удовлетворяющих фильтру.
///
/// Элементы содержат только «шапку» перемещения, без перечня упаковок. Чтобы
/// получить состав, вызовите `getTransferDeclaration` с нужным `declarationId`.
class GetTransferListByFilterResponse {
  /// Создаёт ответ.
  const GetTransferListByFilterResponse({this.transferDeclarationList});

  /// Разбирает ответ из JSON.
  factory GetTransferListByFilterResponse.fromJson(Map<String, dynamic> json) {
    return GetTransferListByFilterResponse(
      transferDeclarationList: asMapList(json['transferDeclarationList'])
          ?.map(TransferDeclarationInfo.fromJson)
          .toList(growable: false),
    );
  }

  /// Найденные декларации перемещения.
  final List<TransferDeclarationInfo>? transferDeclarationList;
}

/// Краткая информация о декларации перемещения (элемент списка).
class TransferDeclarationInfo {
  /// Создаёт информацию.
  const TransferDeclarationInfo({
    required this.declarationId,
    required this.declarationDate,
    required this.fromStakeholderCode,
    required this.toStakeholderCode,
    required this.totalCount,
    required this.currentState,
    required this.transferType,
    required this.isReturn,
    this.documentNo,
    this.documentDate,
    this.fromStakeholder,
    this.toStakeholder,
    this.description,
  });

  /// Разбирает информацию из JSON.
  factory TransferDeclarationInfo.fromJson(Map<String, dynamic> json) {
    return TransferDeclarationInfo(
      declarationId: asInt(json['declarationId']) ?? 0,
      declarationDate:
          asDateTime(json['declarationDate']) ?? DateTime.now().toUtc(),
      fromStakeholderCode: asInt(json['fromStakeholderCode']) ?? 0,
      toStakeholderCode: asInt(json['toStakeholderCode']) ?? 0,
      totalCount: asInt(json['totalCount']) ?? 0,
      currentState: TransferState.fromWire(json['currentState']) ??
          TransferState.initiated,
      transferType:
          TransferType.fromWire(json['transferType']) ?? TransferType.initiate,
      isReturn: asBool(json['isReturn']) ?? false,
      documentNo: json['documentNo'] as String?,
      documentDate: asDateTime(json['documentDate']),
      fromStakeholder: json['fromStakeholder'] as String?,
      toStakeholder: json['toStakeholder'] as String?,
      description: json['description'] as String?,
    );
  }

  /// Идентификатор декларации перемещения.
  final int declarationId;

  /// Дата декларации перемещения.
  final DateTime declarationDate;

  /// Код стейкхолдера-отправителя.
  final int fromStakeholderCode;

  /// Код стейкхолдера-получателя.
  final int toStakeholderCode;

  /// Общее количество упаковок в перемещении.
  final int totalCount;

  /// Текущее состояние перемещения.
  final TransferState currentState;

  /// Роль текущей организации в перемещении.
  final TransferType transferType;

  /// Признак возвратного перемещения.
  final bool isReturn;

  /// Номер сопроводительного документа.
  final String? documentNo;

  /// Дата сопроводительного документа.
  final DateTime? documentDate;

  /// Наименование стейкхолдера-отправителя.
  final String? fromStakeholder;

  /// Наименование стейкхолдера-получателя.
  final String? toStakeholder;

  /// Текстовое описание перемещения.
  final String? description;
}

/// Ответ `POST /api/TrackAndTrace/GetStockInheldList` — сводные остатки
/// по всем препаратам, доступным текущей организации.
class GetStockInheldListResponse {
  /// Создаёт ответ.
  const GetStockInheldListResponse({this.stockInheldSummaryList});

  /// Разбирает ответ из JSON.
  factory GetStockInheldListResponse.fromJson(Map<String, dynamic> json) {
    return GetStockInheldListResponse(
      stockInheldSummaryList: asMapList(json['stockInheldSummaryList'])
          ?.map(StockInheldSummaryInfo.fromJson)
          .toList(growable: false),
    );
  }

  /// Сводка остатков в разрезе препаратов.
  final List<StockInheldSummaryInfo>? stockInheldSummaryList;
}

/// Сводная строка остатков по одному препарату.
class StockInheldSummaryInfo {
  /// Создаёт строку.
  const StockInheldSummaryInfo({
    required this.amount,
    this.gtin,
    this.fullBrandName,
  });

  /// Разбирает строку из JSON.
  factory StockInheldSummaryInfo.fromJson(Map<String, dynamic> json) {
    return StockInheldSummaryInfo(
      amount: asDouble(json['amount']) ?? 0,
      gtin: json['gtin'] as String?,
      fullBrandName: json['fullBrandName'] as String?,
    );
  }

  /// Суммарное количество препарата в наличии.
  final double amount;

  /// GTIN препарата.
  final String? gtin;

  /// Полное торговое наименование препарата.
  final String? fullBrandName;
}

/// Ответ `POST /api/TrackAndTrace/GetStockInheldListByGtin` — детальные остатки
/// по конкретному GTIN (упаковка за упаковкой).
class GetStockInheldListByGtinResponse {
  /// Создаёт ответ.
  const GetStockInheldListByGtinResponse({this.stockInheldList});

  /// Разбирает ответ из JSON.
  factory GetStockInheldListByGtinResponse.fromJson(Map<String, dynamic> json) {
    return GetStockInheldListByGtinResponse(
      stockInheldList: asMapList(json['stockInheldList'])
          ?.map(StockInheldInfo.fromJson)
          .toList(growable: false),
    );
  }

  /// Перечень упаковок в наличии.
  final List<StockInheldInfo>? stockInheldList;
}

/// Сведения об одной упаковке в остатках.
class StockInheldInfo {
  /// Создаёт сведения.
  const StockInheldInfo({
    required this.id,
    required this.currentStakeholderId,
    required this.drugPackageItemId,
    required this.expirationDate,
    required this.currentState,
    this.gtin,
    this.fullBrandName,
    this.batchNumber,
    this.serialNumber,
    this.declarationDate,
    this.price,
    this.partialSaleAmount,
    this.qrCode,
  });

  /// Разбирает сведения из JSON.
  factory StockInheldInfo.fromJson(Map<String, dynamic> json) {
    return StockInheldInfo(
      id: asInt(json['id']) ?? 0,
      currentStakeholderId: json['currentStakeholderId'] as String? ?? '',
      drugPackageItemId: json['drugPackageItemId'] as String? ?? '',
      expirationDate:
          asDateTime(json['expirationDate']) ?? DateTime.now().toUtc(),
      currentState:
          ProductState.fromWire(json['currentState']) ?? ProductState.stock,
      gtin: json['gtin'] as String?,
      fullBrandName: json['fullBrandName'] as String?,
      batchNumber: json['batchNumber'] as String?,
      serialNumber: json['serialNumber'] as String?,
      declarationDate: asDateTime(json['declarationDate']),
      price: asDouble(json['price']),
      partialSaleAmount: asDouble(json['partialSaleAmount']),
      qrCode: json['qrCode'] as String?,
    );
  }

  /// Внутренний идентификатор упаковки.
  final int id;

  /// Идентификатор организации, у которой упаковка в наличии.
  ///
  /// Обратите внимание: это внутренний `Guid`, а не код стейкхолдера.
  final String currentStakeholderId;

  /// Идентификатор позиции упаковки препарата (`DrugPackageItem`).
  final String drugPackageItemId;

  /// Дата истечения срока годности упаковки.
  final DateTime expirationDate;

  /// Текущее состояние упаковки.
  final ProductState currentState;

  /// GTIN препарата.
  final String? gtin;

  /// Полное торговое наименование препарата.
  final String? fullBrandName;

  /// Номер партии упаковки.
  final String? batchNumber;

  /// Серийный номер упаковки.
  final String? serialNumber;

  /// Дата декларации, по которой упаковка поступила в наличие.
  final DateTime? declarationDate;

  /// Цена упаковки.
  final double? price;

  /// Количество, уже реализованное при частичной продаже.
  final double? partialSaleAmount;

  /// QR-код (содержимое Data Matrix) упаковки.
  final String? qrCode;
}
