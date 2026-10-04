import '../../json.dart';
import '../enums/transfer.dart';

/// Запрос `POST /api/TrackAndTrace/ImportDeclaration` — декларация импорта
/// партии лекарственного средства с регистрацией серийных номеров (QR-кодов).
///
/// Операция вводит в оборот партию импортированного препарата и «привязывает»
/// к ней список ранее выпущенных QR-кодов. После успешного вызова каждая
/// упаковка получает состояние [ProductState.import].
///
/// Права настраиваются администратором департамента лекарственных средств —
/// интеграции достаточно логина и пароля.
final class ImportDeclarationRequest {
  /// Создаёт запрос.
  const ImportDeclarationRequest({
    required this.gtin,
    required this.batchNo,
    required this.expirationDate,
    required this.price,
    required this.productionDate,
    required this.documentDate,
    required this.documentNo,
    required this.qrTypeId,
    required this.qrCodes,
    this.description,
    this.importApplicationRegistrationNumber,
    this.importerCompanyTaxNumber,
    this.stakeholderCode,
  });

  /// GTIN препарата — 14-значный идентификатор торговой единицы.
  ///
  /// Обязательное поле. Должен совпадать с GTIN, для которого выпущены QR-коды.
  final String gtin;

  /// Номер партии (серии) препарата.
  ///
  /// Обязательное поле. В QR-коде обычно занимает до 11 знаков.
  final String batchNo;

  /// Дата истечения срока годности.
  final DateTime expirationDate;

  /// Дата производства.
  final DateTime productionDate;

  /// Дата документа-основания (накладной, ГТД).
  final DateTime documentDate;

  /// Номер документа-основания.
  final String documentNo;

  /// Цена за единицу препарата.
  final double price;

  /// Идентификатор типа QR-кода.
  ///
  /// Обязательное поле. Определяет порядок полей внутри Data Matrix.
  /// Согласуйте значение с департаментом лекарственных средств.
  ///
  /// Устаревший метод `getSupportedQrTypes`, из которого прежде бралось это
  /// значение, исключён из API и помечен `@Deprecated`.
  final int qrTypeId;

  /// Список QR-кодов (содержимое Data Matrix) регистрируемой партии.
  final List<String> qrCodes;

  /// Произвольное текстовое описание декларации.
  final String? description;

  /// Регистрационный номер заявки на импорт.
  final int? importApplicationRegistrationNumber;

  /// ИНН компании-импортёра.
  final String? importerCompanyTaxNumber;

  /// Код стейкхолдера-склада, на который приходуется партия.
  ///
  /// Если не указан, сервер использует организацию учётной записи.
  final int? stakeholderCode;

  /// Преобразует запрос в JSON для отправки на сервер.
  Map<String, dynamic> toJson() => compactJson({
        'gtin': gtin,
        'batchNo': batchNo,
        'expirationDate': formatDate(expirationDate),
        'price': price,
        'productionDate': formatDate(productionDate),
        'documentDate': formatDate(documentDate),
        'documentNo': documentNo,
        'qrTypeId': qrTypeId,
        'qrCodes': qrCodes,
        'description': description,
        'importApplicationRegistrationNumber':
            importApplicationRegistrationNumber,
        'importerCompanyTaxNumber': importerCompanyTaxNumber,
        'stakeholderCode': stakeholderCode,
      });
}

/// Запрос `POST /api/TrackAndTrace/ProductionDeclaration` — декларация
/// производства партии лекарственного средства.
///
/// Аналогична декларации импорта, но выполняется производителем для собственной
/// продукции. После успешного вызова упаковки получают состояние
/// [ProductState.production].
final class ProductionDeclarationRequest {
  /// Создаёт запрос.
  const ProductionDeclarationRequest({
    required this.gtin,
    required this.batchNo,
    required this.expirationDate,
    required this.price,
    required this.productionDate,
    required this.documentDate,
    required this.documentNo,
    required this.declarationDate,
    required this.qrTypeId,
    required this.qrCodes,
    this.description,
    this.stakeholderCode,
  });

  /// GTIN препарата.
  final String gtin;

  /// Номер партии (серии) препарата.
  final String batchNo;

  /// Дата истечения срока годности.
  final DateTime expirationDate;

  /// Дата производства.
  final DateTime productionDate;

  /// Дата документа-основания.
  final DateTime documentDate;

  /// Номер документа-основания.
  final String documentNo;

  /// Дата самой декларации.
  final DateTime declarationDate;

  /// Цена за единицу препарата.
  final double price;

  /// Идентификатор типа QR-кода.
  ///
  /// Согласуйте значение с департаментом лекарственных средств: устаревший
  /// метод `getSupportedQrTypes` исключён из API.
  final int qrTypeId;

  /// Список QR-кодов выпускаемой партии.
  final List<String> qrCodes;

  /// Произвольное текстовое описание декларации.
  final String? description;

  /// Код стейкхолдера-производителя.
  final int? stakeholderCode;

  /// Преобразует запрос в JSON для отправки на сервер.
  Map<String, dynamic> toJson() => compactJson({
        'gtin': gtin,
        'batchNo': batchNo,
        'expirationDate': formatDate(expirationDate),
        'price': price,
        'productionDate': formatDate(productionDate),
        'documentDate': formatDate(documentDate),
        'documentNo': documentNo,
        'declarationDate': formatDate(declarationDate),
        'qrTypeId': qrTypeId,
        'qrCodes': qrCodes,
        'description': description,
        'stakeholderCode': stakeholderCode,
      });
}

/// Запрос `POST /api/TrackAndTrace/TransferDeclaration` — декларация
/// перемещения упаковок от одного стейкхолдера другому.
///
/// Создаёт перемещение в состоянии [TransferState.initiated]. Получатель должен
/// подтвердить приём методом `transferAccept`, иначе упаковки останутся
/// в состоянии [ProductState.transferInitiated].
final class TransferDeclarationRequest {
  /// Создаёт запрос.
  const TransferDeclarationRequest({
    required this.fromStakeholder,
    required this.toStakeholder,
    required this.declarationDate,
    this.documentNo,
    this.documentDate,
    this.description,
    this.details,
  });

  /// Код организации-отправителя.
  final int fromStakeholder;

  /// Код организации-получателя.
  final int toStakeholder;

  /// Дата декларации перемещения.
  final DateTime declarationDate;

  /// Номер сопроводительного документа.
  final String? documentNo;

  /// Дата сопроводительного документа.
  final DateTime? documentDate;

  /// Произвольное текстовое описание перемещения.
  final String? description;

  /// Перечень передаваемых упаковок.
  final List<TransferDeclarationDetail>? details;

  /// Преобразует запрос в JSON для отправки на сервер.
  Map<String, dynamic> toJson() => compactJson({
        'fromStakeholder': fromStakeholder,
        'toStakeholder': toStakeholder,
        'declarationDate': formatDate(declarationDate),
        'documentNo': documentNo,
        'documentDate': formatDate(documentDate),
        'description': description,
        'details': details?.map((item) => item.toJson()).toList(),
      });
}

/// Элемент списка перемещаемых упаковок в [TransferDeclarationRequest].
final class TransferDeclarationDetail {
  /// Создаёт элемент.
  const TransferDeclarationDetail({required this.qrCode, required this.price});

  /// QR-код (содержимое Data Matrix) перемещаемой упаковки.
  final String qrCode;

  /// Цена упаковки, по которой она передаётся получателю.
  final double price;

  /// Преобразует элемент в JSON.
  Map<String, dynamic> toJson() => {'qrCode': qrCode, 'price': price};
}

/// Запрос `POST /api/TrackAndTrace/TransferAccept` — подтверждение приёма
/// перемещения получателем.
final class TransferAcceptRequest {
  /// Создаёт запрос.
  const TransferAcceptRequest({required this.declarationId});

  /// Идентификатор подтверждаемой декларации перемещения.
  final int declarationId;

  /// Преобразует запрос в JSON.
  Map<String, dynamic> toJson() => {'declarationId': declarationId};
}

/// Запрос отмены перемещения.
///
/// Используется методами:
///
/// - `POST /api/TrackAndTrace/TransferCancel` — отмена исходящего перемещения;
/// - `POST /api/TrackAndTrace/TransferReturnCancel` — отмена возвратного
///   перемещения.
final class TransferCancelRequest {
  /// Создаёт запрос.
  const TransferCancelRequest({required this.declarationId});

  /// Идентификатор отменяемой декларации перемещения.
  final int declarationId;

  /// Преобразует запрос в JSON.
  Map<String, dynamic> toJson() => {'declarationId': declarationId};
}

/// Запрос `POST /api/TrackAndTrace/TransferReturn` — возврат упаковок
/// предыдущему держателю.
///
/// Отправителя и получателя сервер определяет автоматически по истории
/// движения упаковок, поэтому указывать их не нужно.
final class TransferReturnRequest {
  /// Создаёт запрос.
  const TransferReturnRequest({
    this.documentNo,
    this.documentDate,
    this.description,
    this.details,
  });

  /// Номер сопроводительного документа возврата.
  final String? documentNo;

  /// Дата сопроводительного документа возврата.
  final DateTime? documentDate;

  /// Произвольное текстовое описание возврата.
  final String? description;

  /// Перечень возвращаемых упаковок.
  final List<TransferReturnRequestDetail>? details;

  /// Преобразует запрос в JSON для отправки на сервер.
  Map<String, dynamic> toJson() => compactJson({
        'documentNo': documentNo,
        'documentDate': formatDate(documentDate),
        'description': description,
        'details': details?.map((item) => item.toJson()).toList(),
      });
}

/// Элемент списка возвращаемых упаковок в [TransferReturnRequest].
final class TransferReturnRequestDetail {
  /// Создаёт элемент.
  const TransferReturnRequestDetail({required this.qrCode});

  /// QR-код (содержимое Data Matrix) возвращаемой упаковки.
  final String qrCode;

  /// Преобразует элемент в JSON.
  Map<String, dynamic> toJson() => {'qrCode': qrCode};
}
