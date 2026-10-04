# knddb_track_and_trace_flutter

Интеграция [Dart/Flutter SDK KNMDB Track and Trace](https://pub.dev/packages/knddb_track_and_trace)
с Flutter: сохранение входа в защищённом хранилище платформы и контроллер
состояния аутентификации для интерфейса приложения.

[![pub package](https://img.shields.io/pub/v/knddb_track_and_trace_flutter.svg)](https://pub.dev/packages/knddb_track_and_trace_flutter)
[![license](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

---

## Зачем этот пакет

Базовый пакет `knddb_track_and_trace` — чистый Dart: он не зависит от Flutter
и не знает о платформенных хранилищах. Этот пакет добавляет две вещи, нужные
в мобильном приложении:

| Что | Зачем |
|---|---|
| `FlutterSecureSessionStorage` | Вход переживает перезапуск приложения: токены хранятся в Keychain, EncryptedSharedPreferences, libsecret или DPAPI |
| `KnddbAuthController` | Готовый `ChangeNotifier` с состояниями входа — не нужно писать свой стейт-менеджмент для экрана входа |

---

## Установка

```bash
flutter pub add knddb_track_and_trace_flutter
```

Требуется Flutter **3.27** и Dart **3.6** или новее.

---

## Быстрый старт

```dart
import 'package:knddb_track_and_trace/knddb_track_and_trace.dart';
import 'package:knddb_track_and_trace_flutter/knddb_track_and_trace_flutter.dart';

final auth = KnddbAuthController(
  options: KnddbClientOptions(environment: KnddbEnvironment.production),
);

// При запуске приложения — восстановить сохранённый вход.
await auth.restore();
```

Экран, который сам реагирует на состояние входа:

```dart
ListenableBuilder(
  listenable: auth,
  builder: (context, _) => switch (auth.state) {
    KnddbAuthState.idle || KnddbAuthState.loading =>
      const Scaffold(body: Center(child: CircularProgressIndicator())),
    KnddbAuthState.authenticated => const HomeScreen(),
    _ => LoginScreen(onSubmit: auth.signIn),
  },
);
```

Вход, выход и вызовы API:

```dart
// Вход
final ok = await auth.signIn(login, password);
if (!ok) {
  print(auth.error?.message);   // показываем ошибку пользователю
}

// Клиент SDK для вызовов
final stock = await auth.client.getStockInheldList();

// Выход: токены отзываются и сохранённая сессия очищается
await auth.signOut();
```

Не забудьте освободить контроллер:

```dart
@override
void dispose() {
  auth.dispose();   // закрывает HTTP-клиент SDK
  super.dispose();
}
```

---

## Хранение токенов

`FlutterSecureSessionStorage` использует `flutter_secure_storage`:

| Платформа | Хранилище |
|---|---|
| iOS, macOS | Keychain |
| Android | шифрованное хранилище на базе Android Keystore |
| Linux | libsecret |
| Windows | DPAPI |
| Web | WebCrypto с `localStorage` |

**Пароль не сохраняется.** В хранилище попадают только токены, логин и ключ
сессии — этого достаточно, чтобы восстановить вход по `refresh_token` без
повторного ввода пароля. Проверяется тестом: в сохранённой записи нет полей
`password` и `credentials`.

### Своё хранилище

Нужен `SharedPreferences` вместо защищённого хранилища, или вы храните сессии
на сервере — реализуйте интерфейс `KnddbSessionStorage`:

```dart
class MyStorage implements KnddbSessionStorage {
  @override
  Future<void> save(KnddbSession session) async { /* ... */ }

  @override
  Future<KnddbSession?> load() async { /* ... */ }

  @override
  Future<void> clear() async { /* ... */ }
}

final auth = KnddbAuthController(storage: MyStorage());
```

### Несколько учётных записей

Используйте свой ключ записи на каждую учётную запись:

```dart
final storageA = FlutterSecureSessionStorage(storageKey: 'knddb.session.branch-a');
final storageB = FlutterSecureSessionStorage(storageKey: 'knddb.session.branch-b');
```

Переключение пользователя без повторного входа:

```dart
await auth.switchUser('branch:b');   // ключ сессии, под которым выполнялся вход
```

---

## Контуры

| Контур | Базовый адрес | Когда использовать |
|---|---|---|
| `KnddbEnvironment.test` | `https://testndbapi.med.kg/` | Разработка и отладка. **По умолчанию.** |
| `KnddbEnvironment.production` | `https://ndbapi.med.kg/` | Промышленная эксплуатация |

> На боевом контуре все операции реальны и необратимы. Начинайте с тестового.

---

## Права доступа

Роли API настраиваются администратором департамента лекарственных средств
через административную панель KNMDB: приложению достаточно логина и пароля.

Если метод вернул `403`, это повод обратиться к администратору департамента,
а не ошибка приложения:

```dart
if (auth.error?.isForbidden ?? false) {
  // «Обратитесь к администратору департамента лекарственных средств»
}
```

---

## Пример приложения

В каталоге [`example/`](example) — приложение Flutter с экраном входа
и проверкой лекарства по QR-коду:

```bash
cd example
flutter run
```

---

## Документация API

> **Сквозное руководство по Flutter** — установка, настройка платформ
> (Android, iOS, Windows, Linux, Web), экраны входа и проверки лекарства,
> несколько учётных записей, обработка ошибок в интерфейсе, сборка релизов:
> [docs/FLUTTER.md](https://github.com/taalaibekdev/KnmdbApi/blob/main/docs/FLUTTER.md).

Пакет предоставляет все методы Track and Trace API через `auth.client`:

```dart
final client = auth.client;

await client.getAllStakeholders();
await client.getMedicineList();
await client.productInquiryByQrCode(qrCode);       // без авторизации
await client.importDeclaration(request);
await client.transferDeclaration(request);
await client.transferAccept(declarationId);
await client.stockDeclaration(request);
await client.getStockInheldList();
await client.salesDeclaration(request);
await client.deactivateDeclaration(request);
```

Полное описание методов, полей и перечислений — в документации базового SDK:
[SCENARIOS.md](https://github.com/taalaibekdev/KnmdbApi/blob/main/docs/SCENARIOS.md),
[API-REFERENCE.md](https://github.com/taalaibekdev/KnmdbApi/blob/main/docs/API-REFERENCE.md),
[ENUMS.md](https://github.com/taalaibekdev/KnmdbApi/blob/main/docs/ENUMS.md).

---

## Лицензия

MIT — см. [LICENSE](LICENSE).
