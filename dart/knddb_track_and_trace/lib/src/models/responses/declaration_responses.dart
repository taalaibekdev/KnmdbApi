import '../../json.dart';

/// Ответ `POST /api/TrackAndTrace/ImportDeclaration` — результат регистрации
/// партии импорта.
class ImportDeclarationResponse {
  /// Создаёт ответ.
  const ImportDeclarationResponse({
    required this.stakeholderCode,
    required this.declarationId,
    required this.declarationDate,
    required this.productionDate,
    required this.expirationDate,
    this.gtin,
    this.batchNo,
  });

  /// Разбирает ответ из JSON.
  factory ImportDeclarationResponse.fromJson(Map<String, dynamic> json) {
    return ImportDeclarationResponse(
      stakeholderCode: asInt(json['stakeholderCode']) ?? 0,
      declarationId: asInt(json['declarationId']) ?? 0,
      declarationDate:
          asDateTime(json['declarationDate']) ?? DateTime.now().toUtc(),
      productionDate:
          asDateTime(json['productionDate']) ?? DateTime.now().toUtc(),
      expirationDate:
          asDateTime(json['expirationDate']) ?? DateTime.now().toUtc(),
      gtin: json['gtin'] as String?,
      batchNo: json['batchNo'] as String?,
    );
  }

  /// Код организации, на которую приходована партия.
  final int stakeholderCode;

  /// Идентификатор созданной декларации импорта.
  ///
  /// Сохраните его у себя: по нему можно сопоставить операцию.
  final int declarationId;

  /// Дата и время регистрации декларации.
  final DateTime declarationDate;

  /// Дата производства партии.
  final DateTime productionDate;

  /// Дата истечения срока годности партии.
  final DateTime expirationDate;

  /// GTIN зарегистрированной партии.
  final String? gtin;

  /// Номер партии.
  final String? batchNo;
}

/// Ответ `POST /api/TrackAndTrace/ProductionDeclaration` — результат регистрации
/// партии производства.
class ProductionDeclarationResponse {
  /// Создаёт ответ.
  const ProductionDeclarationResponse({
    required this.stakeholderCode,
    required this.declarationId,
    required this.declarationDate,
    required this.productionDate,
    required this.expirationDate,
    this.gtin,
    this.batchNo,
  });

  /// Разбирает ответ из JSON.
  factory ProductionDeclarationResponse.fromJson(Map<String, dynamic> json) {
    return ProductionDeclarationResponse(
      stakeholderCode: asInt(json['stakeholderCode']) ?? 0,
      declarationId: asInt(json['declarationId']) ?? 0,
      declarationDate:
          asDateTime(json['declarationDate']) ?? DateTime.now().toUtc(),
      productionDate:
          asDateTime(json['productionDate']) ?? DateTime.now().toUtc(),
      expirationDate:
          asDateTime(json['expirationDate']) ?? DateTime.now().toUtc(),
      gtin: json['gtin'] as String?,
      batchNo: json['batchNo'] as String?,
    );
  }

  /// Код организации-производителя.
  final int stakeholderCode;

  /// Идентификатор созданной декларации производства.
  final int declarationId;

  /// Дата и время регистрации декларации.
  final DateTime declarationDate;

  /// Дата производства партии.
  final DateTime productionDate;

  /// Дата истечения срока годности партии.
  final DateTime expirationDate;

  /// GTIN зарегистрированной партии.
  final String? gtin;

  /// Номер партии.
  final String? batchNo;
}

/// Ответ `POST /api/TrackAndTrace/TransferDeclaration` — результат создания
/// перемещения.
class TransferDeclarationResponse {
  /// Создаёт ответ.
  const TransferDeclarationResponse({
    required this.declarationId,
    required this.declarationDate,
  });

  /// Разбирает ответ из JSON.
  factory TransferDeclarationResponse.fromJson(Map<String, dynamic> json) {
    return TransferDeclarationResponse(
      declarationId: asInt(json['declarationId']) ?? 0,
      declarationDate:
          asDateTime(json['declarationDate']) ?? DateTime.now().toUtc(),
    );
  }

  /// Идентификатор созданной декларации перемещения.
  ///
  /// Передайте его получателю для подтверждения методом `transferAccept`.
  final int declarationId;

  /// Дата и время создания декларации перемещения.
  final DateTime declarationDate;
}

/// Ответ методов отмены перемещения — `transferCancel`
/// и `transferReturnCancel`.
class TransferDeclarationCancelResponse {
  /// Создаёт ответ.
  const TransferDeclarationCancelResponse({
    required this.declarationId,
    required this.declarationDate,
  });

  /// Разбирает ответ из JSON.
  factory TransferDeclarationCancelResponse.fromJson(
      Map<String, dynamic> json) {
    return TransferDeclarationCancelResponse(
      declarationId: asInt(json['declarationId']) ?? 0,
      declarationDate:
          asDateTime(json['declarationDate']) ?? DateTime.now().toUtc(),
    );
  }

  /// Идентификатор отменённой декларации перемещения.
  final int declarationId;

  /// Дата и время отмены.
  final DateTime declarationDate;
}

/// Ответ `POST /api/TrackAndTrace/StockDeclaration` — результат постановки
/// упаковок на склад.
class StockDeclarationResponse {
  /// Создаёт ответ.
  const StockDeclarationResponse({
    required this.declarationId,
    required this.declarationDate,
  });

  /// Разбирает ответ из JSON.
  factory StockDeclarationResponse.fromJson(Map<String, dynamic> json) {
    return StockDeclarationResponse(
      declarationId: asInt(json['declarationId']) ?? 0,
      declarationDate:
          asDateTime(json['declarationDate']) ?? DateTime.now().toUtc(),
    );
  }

  /// Идентификатор созданной складской декларации.
  final int declarationId;

  /// Дата и время создания складской декларации.
  final DateTime declarationDate;
}

/// Ответ `POST /api/TrackAndTrace/StockDeclarationCancel` — результат отмены
/// складской декларации.
class StockDeclarationCancelResponse {
  /// Создаёт ответ.
  const StockDeclarationCancelResponse({
    required this.declarationId,
    required this.isSuccess,
    this.message,
  });

  /// Разбирает ответ из JSON.
  factory StockDeclarationCancelResponse.fromJson(Map<String, dynamic> json) {
    return StockDeclarationCancelResponse(
      declarationId: asInt(json['declarationId']) ?? 0,
      isSuccess: asBool(json['isSuccess']) ?? false,
      message: json['message'] as String?,
    );
  }

  /// Идентификатор отменённой складской декларации.
  final int declarationId;

  /// Признак успешности отмены.
  ///
  /// Если сервер вернул `false`, причина описана в [message]. Обратите
  /// внимание: при этом HTTP-ответ всё равно имеет статус 200, поэтому
  /// проверяйте это поле.
  final bool isSuccess;

  /// Текстовое пояснение результата отмены.
  final String? message;
}

/// Ответ методов реализации — `salesDeclaration`, `salesDeclarationCancel`
/// и `salesDeclarationBoxCancel`.
class SalesDeclarationResponse {
  /// Создаёт ответ.
  const SalesDeclarationResponse({
    required this.stakeholderCode,
    required this.declarationId,
    required this.declarationDate,
  });

  /// Разбирает ответ из JSON.
  factory SalesDeclarationResponse.fromJson(Map<String, dynamic> json) {
    return SalesDeclarationResponse(
      stakeholderCode: asInt(json['stakeholderCode']) ?? 0,
      declarationId: asInt(json['declarationId']) ?? 0,
      declarationDate:
          asDateTime(json['declarationDate']) ?? DateTime.now().toUtc(),
    );
  }

  /// Код организации, оформившей реализацию.
  final int stakeholderCode;

  /// Идентификатор декларации реализации.
  final int declarationId;

  /// Дата и время операции.
  final DateTime declarationDate;
}

/// Ответ `POST /api/TrackAndTrace/DeactivateDeclaration` — результат
/// деактивации упаковок.
class DeactivateDeclarationResponse {
  /// Создаёт ответ.
  const DeactivateDeclarationResponse({
    required this.declarationId,
    required this.declarationDate,
  });

  /// Разбирает ответ из JSON.
  factory DeactivateDeclarationResponse.fromJson(Map<String, dynamic> json) {
    return DeactivateDeclarationResponse(
      declarationId: asInt(json['declarationId']) ?? 0,
      declarationDate:
          asDateTime(json['declarationDate']) ?? DateTime.now().toUtc(),
    );
  }

  /// Идентификатор созданной декларации деактивации.
  final int declarationId;

  /// Дата и время деактивации.
  final DateTime declarationDate;
}
