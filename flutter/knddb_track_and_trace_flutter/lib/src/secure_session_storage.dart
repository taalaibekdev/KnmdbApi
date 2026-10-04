import 'dart:convert';

import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:knddb_track_and_trace/knddb_track_and_trace.dart';

/// Хранилище сессии KNMDB в защищённом хранилище платформы.
///
/// Использует `flutter_secure_storage`:
///
/// | Платформа | Хранилище |
/// |---|---|
/// | iOS, macOS | Keychain |
/// | Android | шифрованное хранилище на базе Android Keystore |
/// | Linux | libsecret |
/// | Windows | DPAPI |
/// | Web | WebCrypto с `localStorage` |
///
/// **Пароль не сохраняется.** В хранилище попадают только токены, логин
/// и ключ сессии — этого достаточно, чтобы восстановить вход по
/// `refresh_token` без повторного ввода пароля.
///
/// ```dart
/// final client = KnddbApiClient(
///   options: KnddbClientOptions(environment: KnddbEnvironment.production),
///   storage: FlutterSecureSessionStorage(),
/// );
///
/// if (await client.restoreSession()) {
///   // Пользователь уже вошёл
/// }
/// ```
class FlutterSecureSessionStorage implements KnddbSessionStorage {
  /// Создаёт хранилище.
  ///
  /// [storageKey] — ключ записи в защищённом хранилище. Меняйте его, если
  /// приложение обслуживает несколько независимых учётных записей и они должны
  /// храниться раздельно.
  /// [storage] — своя реализация `FlutterSecureStorage`; полезна для тестов
  /// и для тонкой настройки параметров платформы.
  FlutterSecureSessionStorage({
    this.storageKey = defaultStorageKey,
    FlutterSecureStorage? storage,
  }) : _storage = storage ?? const FlutterSecureStorage();

  /// Ключ записи по умолчанию.
  static const String defaultStorageKey = 'knddb.session.v1';

  /// Ключ записи в защищённом хранилище.
  final String storageKey;

  final FlutterSecureStorage _storage;

  @override
  Future<void> save(KnddbSession session) async {
    await _storage.write(key: storageKey, value: jsonEncode(session.toJson()));
  }

  @override
  Future<KnddbSession?> load() async {
    final raw = await _storage.read(key: storageKey);
    if (raw == null || raw.isEmpty) {
      return null;
    }

    try {
      final decoded = jsonDecode(raw);
      if (decoded is! Map) {
        return null;
      }

      return KnddbSession.fromJson(decoded.cast<String, dynamic>());
    } on FormatException {
      // Повреждённая запись: считаем, что сохранённого входа нет.
      await clear();
      return null;
    }
  }

  @override
  Future<void> clear() => _storage.delete(key: storageKey);
}
