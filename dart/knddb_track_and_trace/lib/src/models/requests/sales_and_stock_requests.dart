import '../../json.dart';
import '../enums/consumption_type.dart';

/// Запрос `POST /api/TrackAndTrace/StockDeclaration` — постановка упаковок
/// на складской учёт.
///
/// Применяется после физической приёмки. После успешного вызова упаковки
/// получают состояние `ProductState.stock`.
final class StockDeclarationRequest {
  /// Создаёт запрос.
  const StockDeclarationRequest({
    required this.declarationDate,
    required this.stakeholderCode,
    this.description,
    this.details,
  });

  /// Дата декларации.
  final DateTime declarationDate;

  /// Код стейкхолдера-склада, на который ставится товар.
  final int stakeholderCode;

  /// Произвольное текстовое описание декларации.
  final String? description;

  /// Перечень упаковок.
  ///
  /// Если список пуст или не задан, сервер ставит на учёт всю партию
  /// соответствующей декларации импорта или производства.
  final List<StockDeclarationDetail>? details;

  /// Преобразует запрос в JSON для отправки на сервер.
  Map<String, dynamic> toJson() => compactJson({
        'declarationDate': formatDate(declarationDate),
        'stakeholderCode': stakeholderCode,
        'description': description,
        'details': details?.map((item) => item.toJson()).toList(),
      });
}

/// Элемент списка упаковок в [StockDeclarationRequest].
final class StockDeclarationDetail {
  /// Создаёт элемент.
  const StockDeclarationDetail({
    this.batchNumber,
    this.expirationDate,
    required this.qrCode,
  });

  /// Номер партии упаковки.
  final String? batchNumber;

  /// Дата истечения срока годности упаковки.
  final DateTime? expirationDate;

  /// QR-код (содержимое Data Matrix) упаковки.
  final String qrCode;

  /// Преобразует элемент в JSON.
  Map<String, dynamic> toJson() => compactJson({
        'batchNumber': batchNumber,
        'expirationDate': formatDate(expirationDate),
        'qrCode': qrCode,
      });
}

/// Запрос `POST /api/TrackAndTrace/StockDeclarationCancel` — отмена ранее
/// созданной складской декларации.
final class StockDeclarationCancelRequest {
  /// Создаёт запрос.
  const StockDeclarationCancelRequest({required this.declarationId});

  /// Идентификатор отменяемой складской декларации.
  final int declarationId;

  /// Преобразует запрос в JSON.
  Map<String, dynamic> toJson() => {'declarationId': declarationId};
}

/// Запрос `POST /api/TrackAndTrace/SalesDeclaration` — декларация реализации
/// (продажи или расхода) упаковок.
///
/// Операция выбытия: упаковки помечаются как проданные
/// (`ProductState.sales`) либо частично проданные (`ProductState.partialSales`).
///
/// ```dart
/// // Аптека: розничная продажа
/// final sale = SalesDeclarationRequest(
///   isPharmacyConsumption: true,
///   details: [
///     SalesDeclarationDetail(qrCode: qrCode, price: 165.0),
///   ],
/// );
///
/// // Больница: расход по требованию отделения
/// final consumption = SalesDeclarationRequest(
///   isPharmacyConsumption: false,
///   requestNumber: 'ТРЕБ-2025-0042',
///   departmentName: 'Кардиологическое отделение',
///   details: [SalesDeclarationDetail(qrCode: qrCode, price: 0)],
/// );
/// ```
final class SalesDeclarationRequest {
  /// Создаёт запрос.
  const SalesDeclarationRequest({
    required this.isPharmacyConsumption,
    required this.details,
    this.prescriptionId,
    this.patientId,
    this.requestNumber,
    this.branchDefId,
    this.departmentName,
  });

  /// Признак аптечной продажи.
  ///
  /// `true` — реализация через аптеку (розница);
  /// `false` — расход медицинской организации.
  final bool isPharmacyConsumption;

  /// Перечень реализуемых упаковок.
  final List<SalesDeclarationDetail> details;

  /// Идентификатор рецепта.
  final String? prescriptionId;

  /// ПИН (персональный идентификационный номер) гражданина-покупателя.
  final String? patientId;

  /// Номер требования медицинской организации (только для больниц).
  final String? requestNumber;

  /// Идентификатор подразделения (отделения) медицинской организации.
  final String? branchDefId;

  /// Наименование отделения медицинской организации (только для больниц).
  final String? departmentName;

  /// Преобразует запрос в JSON для отправки на сервер.
  Map<String, dynamic> toJson() => compactJson({
        'isPharmacyConsuption': isPharmacyConsumption,
        'details': details.map((item) => item.toJson()).toList(),
        'prescriptionId': prescriptionId,
        'patientId': patientId,
        'requestNumber': requestNumber,
        'branchDefId': branchDefId,
        'departmentName': departmentName,
      });
}

/// Элемент списка реализуемых упаковок в [SalesDeclarationRequest].
final class SalesDeclarationDetail {
  /// Создаёт элемент.
  const SalesDeclarationDetail({
    required this.qrCode,
    required this.price,
    this.isPartialSale = false,
    this.partialSaleAmount,
  });

  /// QR-код (содержимое Data Matrix) упаковки.
  final String qrCode;

  /// Цена реализации упаковки или её проданной части.
  final double price;

  /// Признак частичной продажи упаковки.
  ///
  /// При `true` обязателен [partialSaleAmount].
  final bool isPartialSale;

  /// Количество, реализуемое при частичной продаже.
  final double? partialSaleAmount;

  /// Преобразует элемент в JSON.
  Map<String, dynamic> toJson() => compactJson({
        'qrCode': qrCode,
        'price': price,
        'isPartialSale': isPartialSale,
        'partialSaleAmount': partialSaleAmount,
      });
}

/// Запрос `POST /api/TrackAndTrace/SalesDeclarationCancel` — отмена декларации
/// реализации целиком.
final class SalesDeclarationCancelRequest {
  /// Создаёт запрос.
  const SalesDeclarationCancelRequest({required this.declarationId});

  /// Идентификатор отменяемой декларации реализации.
  final int declarationId;

  /// Преобразует запрос в JSON.
  Map<String, dynamic> toJson() => {'declarationId': declarationId};
}

/// Запрос `POST /api/TrackAndTrace/SalesDeclarationBoxCancel` — отмена
/// реализации отдельной упаковки по её QR-коду.
final class SalesDeclarationBoxCancelRequest {
  /// Создаёт запрос.
  const SalesDeclarationBoxCancelRequest({required this.qrCode});

  /// QR-код упаковки, продажу которой нужно отменить.
  final String qrCode;

  /// Преобразует запрос в JSON.
  Map<String, dynamic> toJson() => {'qrCode': qrCode};
}

/// Запрос `POST /api/TrackAndTrace/DeactivateDeclaration` — деактивация
/// (списание) упаковок с указанием типа расходной операции.
final class DeactivateDeclarationRequest {
  /// Создаёт запрос.
  const DeactivateDeclarationRequest({
    required this.consumptionType,
    this.description,
    this.details,
  });

  /// Тип расходной операции, по которой упаковки выводятся из оборота.
  ///
  /// Список значений и их смысл см. в [ConsumptionType].
  final ConsumptionType consumptionType;

  /// Произвольное текстовое описание (комментарий, номер акта).
  final String? description;

  /// Перечень деактивируемых упаковок.
  final List<DeactivateDeclarationDetail>? details;

  /// Преобразует запрос в JSON для отправки на сервер.
  Map<String, dynamic> toJson() => compactJson({
        'consumptionType': consumptionType.value,
        'description': description,
        'details': details?.map((item) => item.toJson()).toList(),
      });
}

/// Элемент списка деактивируемых упаковок в [DeactivateDeclarationRequest].
final class DeactivateDeclarationDetail {
  /// Создаёт элемент.
  const DeactivateDeclarationDetail({required this.qrCode});

  /// QR-код (содержимое Data Matrix) деактивируемой упаковки.
  final String qrCode;

  /// Преобразует элемент в JSON.
  Map<String, dynamic> toJson() => {'qrCode': qrCode};
}
