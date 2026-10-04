import 'package:flutter_secure_storage/test/test_flutter_secure_storage_platform.dart';
import 'package:flutter_secure_storage_platform_interface/flutter_secure_storage_platform_interface.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:knddb_track_and_trace/knddb_track_and_trace.dart';
import 'package:knddb_track_and_trace_flutter/knddb_track_and_trace_flutter.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  group('FlutterSecureSessionStorage', () {
    late Map<String, String> backing;
    late FlutterSecureSessionStorage storage;

    setUp(() {
      backing = <String, String>{};
      // Официальная тестовая реализация платформенного канала — хранит данные
      // в обычной карте в памяти.
      FlutterSecureStoragePlatform.instance =
          TestFlutterSecureStoragePlatform(backing);
      storage = FlutterSecureSessionStorage();
    });

    test('сохраняет и загружает сессию', () async {
      await storage.save(
        KnddbSession(
          key: 'user:42',
          userName: 'pharmacist',
          accessToken: 'access-token',
          refreshToken: 'refresh-token',
          accessTokenExpiresAt: DateTime.utc(2026, 1, 1, 12),
        ),
      );

      expect(backing, isNotEmpty);

      final loaded = await storage.load();

      expect(loaded, isNotNull);
      expect(loaded!.key, 'user:42');
      expect(loaded.userName, 'pharmacist');
      expect(loaded.accessToken, 'access-token');
      expect(loaded.refreshToken, 'refresh-token');
      expect(loaded.accessTokenExpiresAt, DateTime.utc(2026, 1, 1, 12));
      expect(loaded.isAuthenticated, isTrue);
    });

    test('пароль не попадает в защищённое хранилище', () async {
      final session = KnddbSession(key: 'default', userName: 'user')
        ..credentials =
            const KnddbCredentials(userName: 'user', password: 'secret')
        ..accessToken = 'token';

      await storage.save(session);

      final raw = backing.values.single;

      expect(raw.contains('secret'), isFalse);
      expect(raw.contains('password'), isFalse);
      expect(raw.contains('credentials'), isFalse);
    });

    test('без сохранённых данных возвращает null', () async {
      expect(await storage.load(), isNull);
    });

    test('повреждённая запись не ломает загрузку и очищается', () async {
      backing[FlutterSecureSessionStorage.defaultStorageKey] = 'not-a-json';

      expect(await storage.load(), isNull);
      expect(backing, isEmpty);
    });

    test('clear удаляет запись', () async {
      await storage.save(KnddbSession(key: 'default', accessToken: 'token'));
      expect(await storage.load(), isNotNull);

      await storage.clear();

      expect(await storage.load(), isNull);
    });

    test('свой ключ записи позволяет хранить несколько учётных записей',
        () async {
      final first = FlutterSecureSessionStorage(storageKey: 'knddb.session.a');
      final second = FlutterSecureSessionStorage(storageKey: 'knddb.session.b');

      await first.save(KnddbSession(key: 'a', accessToken: 'token-a'));
      await second.save(KnddbSession(key: 'b', accessToken: 'token-b'));

      expect((await first.load())!.accessToken, 'token-a');
      expect((await second.load())!.accessToken, 'token-b');
    });
  });

  group('KnddbAuthController', () {
    late Map<String, String> backing;

    setUp(() {
      backing = <String, String>{};
      FlutterSecureStoragePlatform.instance =
          TestFlutterSecureStoragePlatform(backing);
    });

    test('исходное состояние — idle', () {
      final controller = KnddbAuthController(options: KnddbClientOptions());

      expect(controller.state, KnddbAuthState.idle);
      expect(controller.isAuthenticated, isFalse);
      expect(controller.error, isNull);

      controller.dispose();
    });

    test('restore без сохранённых данных переводит в unauthenticated',
        () async {
      final controller = KnddbAuthController(options: KnddbClientOptions());

      expect(await controller.restore(), isFalse);
      expect(controller.state, KnddbAuthState.unauthenticated);

      controller.dispose();
    });

    test('switchUser для несуществующей сессии переводит в состояние ошибки',
        () async {
      final controller = KnddbAuthController(options: KnddbClientOptions());

      expect(await controller.switchUser('unknown'), isFalse);
      expect(controller.state, KnddbAuthState.error);
      expect(controller.error, isA<KnddbConfigurationException>());

      controller.dispose();
    });

    test('уведомляет слушателей об изменении состояния', () async {
      final controller = KnddbAuthController(options: KnddbClientOptions());
      var notifications = 0;
      controller.addListener(() => notifications++);

      await controller.restore();

      // loading → unauthenticated
      expect(notifications, 2);

      controller.dispose();
    });
  });
}
