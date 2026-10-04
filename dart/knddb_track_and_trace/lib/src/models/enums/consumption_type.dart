import 'enum_wire.dart';

/// Тип расходной операции (тип списания) при деактивации упаковки.
///
/// Соответствует серверному перечислению
/// `KNDDBModel.TTConsumptionDeclaration.ConsumptionTypeEnum`.
///
/// Используется в:
///
/// - поле `consumptionType` запроса `DeactivateDeclarationRequest` — тип списания
///   при деактивации;
/// - поле `consumptionType` ответа `ProductInquiryResult` — тип списания, по
///   которому упаковка выбыла из оборота.
///
/// Обратите внимание: значения идут с шагом 10, а не подряд. Это позволяет
/// добавлять новые типы между существующими без изменения уже сохранённых
/// в базе данных значений. Не полагайтесь на «последовательность» чисел —
/// сравнивайте с членами перечисления.
enum ConsumptionType {
  /// Системное поступление — техническая операция ввода остатков в систему.
  systemIn(0, 'SYSTEM IN'),

  /// Системное выбытие — техническая операция вывода остатков из системы.
  systemOut(10, 'SYSTEM OUT'),

  /// Производственные потери — брак и отходы на этапе производства.
  productionWastage(20, 'PRODUCTION WASTAGE'),

  /// Уничтожение в связи с отзывом партии из обращения.
  disposalDueToWithdrawal(30, 'DISPOSAL DUE TO WITHDRAWAL'),

  /// Уничтожение по истечении срока годности — самый частый случай на складе.
  disposalForDeadline(40, 'DISPOSAL FOR DEADLINE'),

  /// Ревизия — корректировка остатков по результатам инвентаризации.
  revision(50, 'REVISION'),

  /// Потребление (расход) — обычное выбытие в процессе использования.
  consumption(60, 'CONSUMPTION'),

  /// Утеряно.
  lost(70, 'LOST'),

  /// Повреждено.
  damaged(80, 'DAMAGED'),

  /// Похищено.
  stolen(90, 'STOLEN'),

  /// Образец — выдача препарата в качестве образца.
  sample(100, 'SAMPLE');

  const ConsumptionType(this.value, this.label);

  /// Числовое значение на сервере.
  final int value;

  /// Человекочитаемое название значения.
  final String label;

  /// Находит значение по числу или строке; `null`, если значение неизвестно.
  static ConsumptionType? fromWire(Object? wire) => enumFromWire(
        wire,
        ConsumptionType.values,
        (value) => value.value,
        (value) => value.label,
      );

  /// Название причины на русском языке — для актов списания и интерфейса.
  String get reasonRu {
    switch (this) {
      case ConsumptionType.systemIn:
        return 'Системное поступление';
      case ConsumptionType.systemOut:
        return 'Системное выбытие';
      case ConsumptionType.productionWastage:
        return 'Производственный брак';
      case ConsumptionType.disposalDueToWithdrawal:
        return 'Отзыв партии из обращения';
      case ConsumptionType.disposalForDeadline:
        return 'Истёк срок годности';
      case ConsumptionType.revision:
        return 'Инвентаризационная недостача';
      case ConsumptionType.consumption:
        return 'Расход';
      case ConsumptionType.lost:
        return 'Утеря';
      case ConsumptionType.damaged:
        return 'Повреждение';
      case ConsumptionType.stolen:
        return 'Хищение';
      case ConsumptionType.sample:
        return 'Образец';
    }
  }
}

/// Статус обязательности маркировки лекарственного средства.
///
/// Соответствует серверному перечислению
/// `KNDDBModel.Product.TrackAndTraceStatusEnum`. Возвращается в поле
/// `trackAndTraceStatus` модели `MedicineInfo` метода `getMedicineList`.
///
/// **Значение [notSpecified] (0) в серверном перечислении отсутствует, но реально
/// приходит в данных.** Проверено на тестовом контуре: из 3919 препаратов
/// у 3676 (94 %) значение равно нулю. Это означает, что статус маркировки
/// не определён, а не что препарат «не отслеживается». Не путайте его
/// с [notTracked]: `notTracked` — осознанно установленный статус
/// «маркировка не требуется».
enum TrackAndTraceStatus {
  /// Статус маркировки не задан (значение в базе отсутствует).
  ///
  /// Приходит как `0`. В серверном перечислении `TrackAndTraceStatusEnum`
  /// такого члена нет — это значение по умолчанию для препаратов, у которых
  /// статус не заполнен. Встречается чаще остальных, поэтому обязательно
  /// обрабатывайте его в коде.
  notSpecified(0, ''),

  /// Маркировка не требуется — препарат не подлежит прослеживаемости.
  notTracked(1, 'Not Required'),

  /// Маркировка обязательна — нужно нанесение этикетки (Data Matrix).
  labelMandatory(2, 'Required'),

  /// Обязательны и маркировка, и прослеживаемость — полный контроль движения
  /// упаковки от производителя или импортёра до конечного потребителя.
  labelAndTraceMandatory(3, 'Label And TraceMandatory');

  const TrackAndTraceStatus(this.value, this.label);

  /// Числовое значение на сервере.
  final int value;

  /// Человекочитаемое название значения.
  final String label;

  /// Находит значение по числу или строке; `null`, если значение неизвестно.
  static TrackAndTraceStatus? fromWire(Object? wire) => enumFromWire(
        wire,
        TrackAndTraceStatus.values,
        (value) => value.value,
        (value) => value.label,
      );

  /// Требуется ли регистрация каждой упаковки (полная прослеживаемость).
  bool get requiresUnitTracking =>
      this == TrackAndTraceStatus.labelAndTraceMandatory;
}
