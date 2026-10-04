import 'session.dart';

/// Хранилище сессий KNMDB.
///
/// Позволяет сохранять токены между запусками приложения. На мобильных
/// платформах реализуйте его поверх защищённого хранилища
/// (`flutter_secure_storage`, Keychain, EncryptedSharedPreferences):
/// готовый адаптер см. в `knddb_track_and_trace_flutter`.
///
/// ```dart
/// final client = KnddbApiClient(
///   options: KnddbClientOptions(environment: KnddbEnvironment.production),
///   storage: MySecureStorage(),
/// );
///
/// // При старте приложения — восстановить сохранённый вход.
/// await client.restoreSession();
/// ```
abstract interface class KnddbSessionStorage {
  /// Сохраняет сессию.
  ///
  /// Вызывается после успешного входа и после каждого обновления токена.
  Future<void> save(KnddbSession session);

  /// Загружает сохранённую сессию или `null`, если её нет.
  Future<KnddbSession?> load();

  /// Удаляет сохранённую сессию.
  Future<void> clear();
}
