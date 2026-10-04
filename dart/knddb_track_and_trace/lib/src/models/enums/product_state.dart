import 'enum_wire.dart';

/// Состояние (историческое событие) упаковки лекарственного средства.
///
/// Соответствует серверному перечислению `KNDDBModel.TTProductBox.StatesEnum`.
/// Возвращается в полях:
///
/// - `productState` модели `ProductInquiryResult` — текущее состояние упаковки;
/// - `state` модели `ProductInquiryHistory` — событие в истории движения;
/// - `currentState` модели `StockInheldInfo` — состояние остатка.
///
/// Значение описывает **последнее событие**, произошедшее с упаковкой, а не
/// складской статус. Для складского статуса используйте [ProductStatus].
///
/// Нормальный жизненный цикл упаковки:
///
/// ```text
/// production → import → stock → transferInitiated → transferAccepted → sales
/// ```
enum ProductState {
  /// Производство — упаковка выпущена производителем.
  production(1, 'Production'),

  /// Импорт — упаковка ввезена на территорию страны.
  import(2, 'Import'),

  /// Продажа — упаковка реализована конечному потребителю.
  sales(3, 'Sales'),

  /// Деактивация — упаковка выведена из оборота (списана).
  deactivation(4, 'Deactivation'),

  /// Экспорт — упаковка вывезена за пределы страны.
  export(5, 'Export'),

  /// Возврат продажи — покупатель вернул ранее проданную упаковку.
  salesReturn(6, 'Sales Return'),

  /// Подтверждение закупки — приёмка упаковки покупателем.
  purchaseConfirmation(7, 'Purchase Confirmation'),

  /// Продажа отменена — операция продажи аннулирована.
  salesCancelled(8, 'Sales Cancelled'),

  /// Перемещение инициировано — отправитель создал передачу.
  transferInitiated(9, 'Transfer Initiated'),

  /// Перемещение принято — получатель подтвердил приём упаковки.
  transferAccepted(10, 'Transfer Accepted'),

  /// Перемещение отменено — передача аннулирована отправителем.
  transferCancelled(11, 'Transfer Cancelled'),

  /// Частичная продажа — упаковка продана частично.
  partialSales(12, 'Partial Sales'),

  /// Склад — упаковка поставлена на складской учёт.
  stock(13, 'Stock'),

  /// Возвратное перемещение инициировано — создан возврат поставщику.
  returnTransferInitiated(14, 'Return Transfer Initiated'),

  /// Возвратное перемещение принято.
  returnTransferAccepted(15, 'Return Transfer Accepted'),

  /// Возвратное перемещение отменено.
  returnTransferCancelled(16, 'Return Transfer Cancelled'),

  /// Частичная продажа отменена.
  partialSalesCancelled(17, 'Partial Sales Cancelled'),

  /// Деактивация отменена — упаковка возвращена в оборот.
  deactivationCancelled(18, 'Deactivation Cancelled');

  const ProductState(this.value, this.label);

  /// Числовое значение на сервере.
  final int value;

  /// Человекочитаемое название значения.
  final String label;

  /// Находит значение по числу или строке; `null`, если значение неизвестно.
  static ProductState? fromWire(Object? wire) => enumFromWire(
        wire,
        ProductState.values,
        (value) => value.value,
        (value) => value.label,
      );
}

/// Складской статус упаковки лекарственного средства.
///
/// Соответствует серверному перечислению `KNDDBModel.TTProductBox.StatusEnum`.
/// Возвращается в поле `productStatus` модели `ProductInquiryResult`.
///
/// Отличие от [ProductState]: здесь три значения, описывающих текущее складское
/// положение, а не историю событий.
enum ProductStatus {
  /// В наличии — упаковка на складе текущего держателя.
  inStock(1, 'InStock'),

  /// Выбыла — упаковка покинула склад (продана, экспортирована, деактивирована).
  stockOut(2, 'StockOut'),

  /// В перемещении — упаковка передаётся между организациями.
  inTransfer(3, 'InTransfer');

  const ProductStatus(this.value, this.label);

  /// Числовое значение на сервере.
  final int value;

  /// Человекочитаемое название значения.
  final String label;

  /// Находит значение по числу или строке; `null`, если значение неизвестно.
  static ProductStatus? fromWire(Object? wire) => enumFromWire(
        wire,
        ProductStatus.values,
        (value) => value.value,
        (value) => value.label,
      );
}
