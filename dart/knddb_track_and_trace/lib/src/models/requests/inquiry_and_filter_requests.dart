import '../../json.dart';
import '../enums/transfer.dart';

/// Запрос `POST /api/TrackAndTrace/ProductInquiryQRCode` — проверка
/// лекарственного средства по QR-коду (Data Matrix).
///
/// Метод доступен **без авторизации** и предназначен для мобильных приложений
/// потребителей: пользователь сканирует код и получает сведения об упаковке.
/// Если упаковка не найдена, сервер отвечает `404 Not Found` —
/// [KnddbApiException.isNotFound] будет `true`.
final class ProductInquiryWithQrCodeRequest {
  /// Создаёт запрос.
  const ProductInquiryWithQrCodeRequest({required this.qrCode});

  /// QR-код (содержимое Data Matrix), считанный с упаковки.
  ///
  /// Строка должна полностью совпадать с зарегистрированным кодом, включая
  /// идентификаторы применения: например,
  /// `01{GTIN}21{Serial}17{YYMMDD}10{Batch}`.
  final String qrCode;

  /// Преобразует запрос в JSON.
  Map<String, dynamic> toJson() => {'qrCode': qrCode};
}

/// Запрос `POST /api/TrackAndTrace/ProductInquiryGtinSn` — проверка
/// лекарственного средства по паре «GTIN + серийный номер».
///
/// Альтернатива сканированию QR-кода: используется, когда GTIN и серийный номер
/// известны по отдельности. Метод доступен **без авторизации**.
final class ProductInquiryWithGtinSnRequest {
  /// Создаёт запрос.
  const ProductInquiryWithGtinSnRequest({
    required this.gtin,
    required this.serialNumber,
  });

  /// GTIN препарата (14 знаков).
  final String gtin;

  /// Серийный номер упаковки (до 18 знаков).
  final String serialNumber;

  /// Преобразует запрос в JSON.
  Map<String, dynamic> toJson() => {
        'gtin': gtin,
        'serialNumber': serialNumber,
      };
}

/// Запрос `POST /api/TrackAndTrace/GetStockInheldListByGtin` — остатки на складе
/// по конкретному GTIN.
final class GetStockInheldListByGtinRequest {
  /// Создаёт запрос.
  const GetStockInheldListByGtinRequest({required this.gtin});

  /// GTIN препарата, по которому запрашиваются остатки.
  final String gtin;

  /// Преобразует запрос в JSON.
  Map<String, dynamic> toJson() => {'gtin': gtin};
}

/// Запрос `POST /api/TrackAndTrace/GetPartialSaleInfo` — сведения о частично
/// проданной упаковке.
///
/// Позволяет узнать, сколько препарата уже реализовано из упаковки, чтобы
/// корректно оформить следующую продажу или списание остатка.
final class GetPartialSaleInfoRequest {
  /// Создаёт запрос.
  const GetPartialSaleInfoRequest({required this.qrCode});

  /// QR-код (содержимое Data Matrix) упаковки.
  final String qrCode;

  /// Преобразует запрос в JSON.
  Map<String, dynamic> toJson() => {'qrCode': qrCode};
}

/// Запрос `POST /api/TrackAndTrace/GetTransferListByFilter` — поиск деклараций
/// перемещения по набору необязательных фильтров.
///
/// ```dart
/// // Входящие перемещения, ожидающие приёмки
/// final pending = GetTransferListByFilterRequest(
///   currentState: TransferState.initiated,
///   transferType: TransferType.accept,
/// );
/// ```
final class GetTransferListByFilterRequest {
  /// Создаёт запрос.
  const GetTransferListByFilterRequest({
    this.declarationId,
    this.documentNo,
    this.documentDate,
    this.declarationDateFrom,
    this.declarationDateTo,
    this.currentState,
    this.transferType,
    this.isReturn,
  });

  /// Идентификатор декларации перемещения — точный поиск одной записи.
  final int? declarationId;

  /// Номер сопроводительного документа.
  final String? documentNo;

  /// Дата сопроводительного документа.
  final DateTime? documentDate;

  /// Начало диапазона дат декларации (включительно).
  final DateTime? declarationDateFrom;

  /// Конец диапазона дат декларации (включительно).
  final DateTime? declarationDateTo;

  /// Состояние перемещения.
  final TransferState? currentState;

  /// Роль организации в перемещении (отправитель или получатель).
  final TransferType? transferType;

  /// Признак возвратного перемещения.
  ///
  /// `true` — только возвраты, `false` — только обычные перемещения,
  /// `null` — без фильтра.
  final bool? isReturn;

  /// Преобразует запрос в JSON для отправки на сервер.
  Map<String, dynamic> toJson() => compactJson({
        'declarationId': declarationId,
        'documentNo': documentNo,
        'documentDate': formatDate(documentDate),
        'declarationDateFrom': formatDate(declarationDateFrom),
        'declarationDateTo': formatDate(declarationDateTo),
        'currentState': currentState?.value,
        'transferType': transferType?.value,
        'isReturn': isReturn,
      });
}

/// Запрос `POST /api/TrackAndTrace/GetMedicineList` — список лекарственных
/// средств, изменённых после указанной даты.
///
/// Используется для инкрементальной синхронизации локального справочника:
/// в первый раз передайте `null` и получите полный список, далее передавайте
/// максимальную дату `lastUpdate` из предыдущего ответа.
final class GetMedicineListRequest {
  /// Создаёт запрос.
  const GetMedicineListRequest({this.lastUpdate});

  /// Нижняя граница даты изменения препарата.
  ///
  /// Если не указана, возвращается весь справочник.
  final DateTime? lastUpdate;

  /// Преобразует запрос в JSON.
  Map<String, dynamic> toJson() => compactJson({
        'lastUpdate': lastUpdate?.toUtc().toIso8601String(),
      });
}
