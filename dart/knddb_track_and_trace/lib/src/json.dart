/// Внутренние помощники разбора и формирования JSON API KNMDB.
library;

/// Формат даты без времени, ожидаемый API: `yyyy-MM-dd`.
const String knddbDateFormat = 'yyyy-MM-dd';

/// Формирует строку даты `yyyy-MM-dd` для полей-дат запроса.
///
/// Время отбрасывается всегда, даже если вызывающая сторона задала его явно.
///
/// Это важно: сервер KNMDB принимает в полях-датах только `yyyy-MM-dd`.
/// Значение с временем (`2026-03-15T12:00:00Z`) он отклоняет, отвечая кодом
/// результата 2 — «An error occurred while saving the entity changes».
///
/// День берётся в той зоне, которую указал вызывающий: дата
/// `2026-03-15 00:30 +06:00` уходит как `2026-03-15`, а не как `2026-03-14`,
/// хотя в UTC это ещё предыдущий день. Заменять зону на UTC нельзя — иначе
/// дата операции сдвигалась бы на день назад.
String knddbDate(DateTime value) {
  final month = value.month.toString().padLeft(2, '0');
  final day = value.day.toString().padLeft(2, '0');

  return '${value.year.toString().padLeft(4, '0')}-$month-$day';
}

/// То же, что [knddbDate], но для необязательного поля.
String? knddbDateOrNull(DateTime? value) =>
    value == null ? null : knddbDate(value);

/// Приводит значение из JSON к `Map<String, dynamic>`.
///
/// Возвращает `null`, если значение не является объектом.
Map<String, dynamic>? asMap(Object? value) {
  if (value is Map<String, dynamic>) {
    return value;
  }

  if (value is Map) {
    return value.map((key, item) => MapEntry(key.toString(), item));
  }

  return null;
}

/// Приводит значение из JSON к списку объектов.
List<Map<String, dynamic>>? asMapList(Object? value) {
  if (value is! List) {
    return null;
  }

  final result = <Map<String, dynamic>>[];
  for (final item in value) {
    final map = asMap(item);
    if (map != null) {
      result.add(map);
    }
  }

  return result;
}

/// Приводит значение из JSON к списку строк.
List<String>? asStringList(Object? value) {
  if (value is! List) {
    return null;
  }

  return value.whereType<String>().toList(growable: false);
}

/// Приводит значение из JSON к целому числу.
int? asInt(Object? value) {
  if (value is num) {
    return value.toInt();
  }

  if (value is String) {
    return int.tryParse(value);
  }

  return null;
}

/// Приводит значение из JSON к числу с плавающей точкой.
double? asDouble(Object? value) {
  if (value is num) {
    return value.toDouble();
  }

  if (value is String) {
    return double.tryParse(value);
  }

  return null;
}

/// Приводит значение из JSON к логическому типу.
bool? asBool(Object? value) {
  if (value is bool) {
    return value;
  }

  if (value is String) {
    if (value.toLowerCase() == 'true') {
      return true;
    }
    if (value.toLowerCase() == 'false') {
      return false;
    }
  }

  return null;
}

/// Разбирает дату или дату-время.
///
/// Понимает формат `yyyy-MM-dd` (как в запросах),
/// полный ISO-8601 (как в ответах) и `yymmdd` (как внутри Data Matrix).
DateTime? asDateTime(Object? value) {
  if (value is DateTime) {
    return value;
  }

  if (value is! String || value.isEmpty) {
    return null;
  }

  final direct = DateTime.tryParse(value);
  if (direct != null) {
    return direct;
  }

  final dateOnly = tryParseDateOnly(value);
  if (dateOnly != null) {
    return dateOnly;
  }

  // Формат yymmdd, используемый в QR-кодах: 27 = 2027 год.
  if (value.length == 6 && int.tryParse(value) != null) {
    final year = 2000 + int.parse(value.substring(0, 2));
    final month = int.parse(value.substring(2, 4));
    final day = int.parse(value.substring(4, 6));

    if (month >= 1 && month <= 12 && day >= 1 && day <= 31) {
      return DateTime.utc(year, month, day);
    }
  }

  return null;
}

/// Разбирает строку строго в формате `yyyy-MM-dd`.
DateTime? tryParseDateOnly(String value) {
  if (value.length != 10) {
    return null;
  }

  final year = int.tryParse(value.substring(0, 4));
  final month = int.tryParse(value.substring(5, 7));
  final day = int.tryParse(value.substring(8, 10));

  if (year == null || month == null || day == null) {
    return null;
  }

  if (value[4] != '-' || value[7] != '-') {
    return null;
  }

  if (month < 1 || month > 12 || day < 1 || day > 31) {
    return null;
  }

  return DateTime.utc(year, month, day);
}

/// Формирует дату в формате `yyyy-MM-dd` — именно так её ждёт сервер.
///
/// Время отбрасывается всегда, даже если оно задано явно.
///
/// Это важно: сервер KNMDB принимает в полях-датах только `yyyy-MM-dd`.
/// Значение с временем (`2026-03-15T12:00:00Z`) он отклоняет, отвечая кодом
/// результата 2 — «An error occurred while saving the entity changes».
///
/// Если нужна полная отметка времени (например, для поля `lastUpdate` при
/// синхронизации справочника), используйте `value.toUtc().toIso8601String()`.
String? formatDate(DateTime? value) => value == null ? null : knddbDate(value);

/// Убирает из карты пары со значением `null`.
///
/// Сервер KNMDB корректно обрабатывает отсутствующие необязательные поля,
/// а компактное тело запроса проще читать в журналах.
Map<String, dynamic> compactJson(Map<String, dynamic> source) {
  final result = <String, dynamic>{};
  for (final entry in source.entries) {
    if (entry.value != null) {
      result[entry.key] = entry.value;
    }
  }

  return result;
}

/// Приводит карту к виду, пригодному для `jsonEncode`.
///
/// Рекурсивно обрабатывает вложенные карты и списки, отбрасывая `null`
/// в значениях объектов.
Object? toJsonValue(Object? value) {
  if (value == null) {
    return null;
  }

  if (value is DateTime) {
    return value.toUtc().toIso8601String();
  }

  if (value is Map) {
    final result = <String, dynamic>{};
    for (final entry in value.entries) {
      final converted = toJsonValue(entry.value);
      if (converted != null) {
        result[entry.key.toString()] = converted;
      }
    }
    return result;
  }

  if (value is Iterable) {
    return value
        .map(toJsonValue)
        .where((item) => item != null)
        .toList(growable: false);
  }

  return value;
}
