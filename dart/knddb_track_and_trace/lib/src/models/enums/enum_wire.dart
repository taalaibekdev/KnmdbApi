/// Внутренние помощники для разбора значений перечислений.
///
/// Сервер KNMDB передаёт перечисления числами (Newtonsoft.Json без строкового
/// конвертера), поэтому SDK отправляет и ожидает числа. При чтении SDK
/// дополнительно принимает строковые представления: имя члена (`Pharmacy`)
/// и человекочитаемое название (`Label And TraceMandatory`) — это делает
/// интеграцию устойчивой к изменениям формата на стороне сервера.
library;

/// Находит значение перечисления по числу или строке.
///
/// Сопоставление идёт без учёта регистра по:
/// - числовому значению ([valueOf]);
/// - имени члена перечисления;
/// - человекочитаемому названию ([labelOf]).
///
/// Возвращает `null`, если значение неизвестно.
T? enumFromWire<T extends Enum>(
  Object? wire,
  List<T> values,
  int Function(T value) valueOf,
  String Function(T value) labelOf,
) {
  if (wire is num) {
    final number = wire.toInt();
    for (final value in values) {
      if (valueOf(value) == number) {
        return value;
      }
    }
    return null;
  }

  if (wire is String && wire.isNotEmpty) {
    final normalized = wire.trim().toLowerCase();
    for (final value in values) {
      if (value.name.toLowerCase() == normalized ||
          labelOf(value).toLowerCase() == normalized) {
        return value;
      }
    }
  }

  return null;
}
