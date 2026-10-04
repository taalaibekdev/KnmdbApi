import 'enum_wire.dart';

/// Тип организации-участника оборота лекарственных средств (стейкхолдера).
///
/// Соответствует серверному перечислению
/// `KNDDBModel.TTStakeholder.StakeholderType`. Возвращается в поле `type`
/// модели `StakeholderInfo` метода `getAllStakeholders`.
///
/// По сети передаётся числом. При чтении SDK дополнительно принимает строковые
/// представления:
///
/// ```dart
/// StakeholderType.fromWire('Pharmacy');   // StakeholderType.pharmacy
/// StakeholderType.fromWire(5);            // StakeholderType.pharmacy
/// StakeholderType.fromWire('Warehouse');  // StakeholderType.warehouse
/// ```
enum StakeholderType {
  /// Производитель — организация, выпускающая лекарственные средства.
  producer(1, 'Producer'),

  /// Импортёр — организация, ввозящая лекарственные средства в страну.
  importer(2, 'Importer'),

  /// Компания — юридическое лицо общего профиля (дистрибьютор, представительство).
  company(3, 'Company'),

  /// Завод-изготовитель.
  manufacturer(4, 'Manufacturer'),

  /// Аптека — организация розничной реализации.
  pharmacy(5, 'Pharmacy'),

  /// Медицинская организация (больница, поликлиника).
  medicalOrganization(6, 'MedicalOrganization'),

  /// Склад — складское хранение и оптовая отгрузка.
  warehouse(7, 'Warehouse');

  const StakeholderType(this.value, this.label);

  /// Числовое значение на сервере.
  final int value;

  /// Человекочитаемое название значения.
  final String label;

  /// Находит значение по числу или строке; `null`, если значение неизвестно.
  static StakeholderType? fromWire(Object? wire) => enumFromWire(
        wire,
        StakeholderType.values,
        (value) => value.value,
        (value) => value.label,
      );
}
