import 'enum_wire.dart';

/// Состояние перемещения (передачи) упаковок между стейкхолдерами.
///
/// Соответствует серверному перечислению
/// `KNDDBModel.TTTransferDeclaration.TransferStates`. Возвращается в поле
/// `currentState` моделей `GetTransferDeclarationResponse` и
/// `TransferDeclarationInfo`, а также принимается в фильтре
/// `GetTransferListByFilterRequest`.
///
/// Практическое правило: все перемещения со значением [initiated] старше
/// нескольких дней — «зависшие» приёмки, их стоит контролировать отдельным
/// отчётом.
enum TransferState {
  /// Инициировано — получатель ещё не подтвердил приём; упаковки недоступны
  /// для продажи.
  initiated(1, 'Initiated'),

  /// Принято — право собственности перешло получателю.
  accepted(2, 'Accepted'),

  /// Отменено — перемещение аннулировано отправителем до подтверждения.
  cancelled(3, 'Cancelled');

  const TransferState(this.value, this.label);

  /// Числовое значение на сервере.
  final int value;

  /// Человекочитаемое название значения.
  final String label;

  /// Находит значение по числу или строке; `null`, если значение неизвестно.
  static TransferState? fromWire(Object? wire) => enumFromWire(
        wire,
        TransferState.values,
        (value) => value.value,
        (value) => value.label,
      );
}

/// Роль стейкхолдера в конкретной декларации перемещения.
///
/// Соответствует серверному перечислению
/// `KNDDBModel.TTTransferDeclaration.TransferTypes`. Возвращается в поле
/// `transferType` модели `TransferDeclarationInfo` и принимается в фильтре
/// `GetTransferListByFilterRequest`.
enum TransferType {
  /// Инициатор — стейкхолдер выступает отправителем перемещения.
  initiate(1, 'Initiate'),

  /// Получатель — стейкхолдер принимает перемещение.
  accept(2, 'Accept');

  const TransferType(this.value, this.label);

  /// Числовое значение на сервере.
  final int value;

  /// Человекочитаемое название значения.
  final String label;

  /// Находит значение по числу или строке; `null`, если значение неизвестно.
  static TransferType? fromWire(Object? wire) => enumFromWire(
        wire,
        TransferType.values,
        (value) => value.value,
        (value) => value.label,
      );
}
