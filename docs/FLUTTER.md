# Flutter: подключение SDK KNMDB Track and Trace

Практическое руководство по интеграции SDK в приложение Flutter: от установки
до готовых экранов. Всё, что нужно для мобильного приложения, — в одном месте.

- [Пакеты](#пакеты)
- [Установка](#установка)
- [Настройка платформ](#настройка-платформ)
- [Структура приложения](#структура-приложения)
- [Экран входа](#экран-входа)
- [Основной экран и вызовы API](#основной-экран-и-вызовы-api)
- [Проверка лекарства по QR-коду](#проверка-лекарства-по-qr-коду)
- [Несколько учётных записей](#несколько-учётных-записей)
- [Сохранение входа](#сохранение-входа)
- [Обработка ошибок в интерфейсе](#обработка-ошибок-в-интерфейсе)
- [Сборка для разных платформ](#сборка-для-разных-платформ)
- [Что дальше](#что-дальше)

---

## Пакеты

| Пакет | Что даёт | Обязателен |
|---|---|---|
| `knddb_track_and_trace` | Клиент API, все 23 метода, модели, перечисления, сессии | **Да** |
| `knddb_track_and_trace_flutter` | Защищённое хранение токенов между запусками, контроллер состояния входа для UI | Рекомендуется |

Базовый пакет — чистый Dart, поэтому его можно использовать и в Dart-приложениях
без Flutter. Второй пакет добавляет то, что нужно именно в приложении:
`FlutterSecureSessionStorage` и `KnddbAuthController`.

---

## Установка

```bash
flutter pub add knddb_track_and_trace_flutter
```

Базовый пакет подтянется автоматически как зависимость. Если он нужен напрямую
(например, для типов запросов):

```bash
flutter pub add knddb_track_and_trace
```

Требуется Flutter **3.27** и Dart **3.6** или новее.

---

## Настройка платформ

`FlutterSecureSessionStorage` использует `flutter_secure_storage`. Для Android
и iOS дополнительная настройка **не нужна**: современные версии плагина
работают без правок `AndroidManifest.xml` и `Info.plist`.

| Платформа | Хранилище токенов | Что нужно |
|---|---|---|
| Android | Шифрованное хранилище на базе Android Keystore | Ничего, минимальная версия — `minSdkVersion` 23 |
| iOS, macOS | Keychain | Ничего |
| Windows | DPAPI | Ничего |
| Linux | libsecret | Установить `libsecret-1-dev` и `libjsoncpp-dev` в системе |
| Web | WebCrypto с `localStorage` | Ничего |

### Android: минимальная версия SDK

Проверьте `android/app/build.gradle.kts`:

```kotlin
android {
    defaultConfig {
        minSdk = 23   // требуется flutter_secure_storage
    }
}
```

### Linux

```bash
sudo apt-get install libsecret-1-dev libjsoncpp-dev
```

### Release-сборка Android

Для шифрованного хранилища в release-сборке может потребоваться отключить
минификацию плагина — добавьте в `android/app/proguard-rules.pro`:

```proguard
-keep class com.it_nomads.fluttersecurestorage.** { *; }
```

---

## Структура приложения

Три шага, и приложение готово к работе:

```text
1. Создать KnddbAuthController один раз на всё приложение
2. Вызвать restore() при старте — восстановит сохранённый вход
3. Показывать экран по auth.state
```

```dart
// lib/main.dart
import 'package:flutter/material.dart';
import 'package:knddb_track_and_trace/knddb_track_and_trace.dart';
import 'package:knddb_track_and_trace_flutter/knddb_track_and_trace_flutter.dart';

void main() => runApp(const MyApp());

class MyApp extends StatefulWidget {
  const MyApp({super.key});

  @override
  State<MyApp> createState() => _MyAppState();
}

class _MyAppState extends State<MyApp> {
  late final KnddbAuthController _auth;

  @override
  void initState() {
    super.initState();

    _auth = KnddbAuthController(
      options: KnddbClientOptions(
        environment: KnddbEnvironment.test,   // для боевого — production
        userAgent: 'MyPharmacyApp/1.0',
      ),
    );

    _auth.restore();
  }

  @override
  void dispose() {
    _auth.dispose();   // закрывает HTTP-клиент SDK
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => MaterialApp(
        title: 'KNMDB',
        theme: ThemeData(colorSchemeSeed: Colors.teal, useMaterial3: true),
        home: ListenableBuilder(
          listenable: _auth,
          builder: (context, _) => switch (_auth.state) {
            KnddbAuthState.idle || KnddbAuthState.loading =>
              const Scaffold(body: Center(child: CircularProgressIndicator())),
            KnddbAuthState.authenticated => HomeScreen(auth: _auth),
            _ => LoginScreen(auth: _auth),
          },
        ),
      );
}
```

> **Передавайте контроллер вниз по дереву.** Он создан один раз и держит
> HTTP-клиент. Не создавайте новый `KnddbAuthController` в каждом виджете:
> каждый экземпляр открывает свой HTTP-клиент и свою сессию.

Если проект использует `provider`, `riverpod` или `bloc`, зарегистрируйте
контроллер там — он уже реализует `ChangeNotifier`, дополнительных обёрток
не требуется.

---

## Экран входа

```dart
class LoginScreen extends StatefulWidget {
  const LoginScreen({required this.auth, super.key});

  final KnddbAuthController auth;

  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  final _login = TextEditingController();
  final _password = TextEditingController();

  @override
  void dispose() {
    _login.dispose();
    _password.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    FocusScope.of(context).unfocus();

    final ok = await widget.auth.signIn(_login.text.trim(), _password.text);

    if (!ok && mounted) {
      final error = widget.auth.error;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(error?.message ?? 'Не удалось войти')),
      );
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(title: const Text('Вход в KNMDB')),
        body: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              TextField(
                controller: _login,
                autocorrect: false,
                decoration: const InputDecoration(labelText: 'Логин'),
              ),
              const SizedBox(height: 12),
              TextField(
                controller: _password,
                obscureText: true,
                onSubmitted: (_) => _submit(),
                decoration: const InputDecoration(labelText: 'Пароль'),
              ),
              const SizedBox(height: 24),
              FilledButton(onPressed: _submit, child: const Text('Войти')),
              const SizedBox(height: 16),
              const Text(
                'Логин и пароль выдаёт администратор KNMDB. '
                'Права настраиваются автоматически; при ошибке 403 '
                'обратитесь к администратору департамента лекарственных средств.',
                textAlign: TextAlign.center,
                style: TextStyle(fontSize: 12, color: Colors.black54),
              ),
            ],
          ),
        ),
      );
}
```

### Что возвращает `signIn`

```dart
final ok = await widget.auth.signIn(login, password);
```

- `true` — вход выполнен, состояние стало `KnddbAuthState.authenticated`;
- `false` — вход не удался, причина в `widget.auth.error`.

Ошибка при этом не выбрасывается: контроллер переводит состояние
в `KnddbAuthState.error` и сохраняет исключение. Это удобно для UI.

---

## Основной экран и вызовы API

Клиент SDK доступен как `auth.client`:

```dart
class HomeScreen extends StatelessWidget {
  const HomeScreen({required this.auth, super.key});

  final KnddbAuthController auth;

  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(
          title: Text(auth.userName ?? 'KNMDB'),
          actions: [
            IconButton(
              tooltip: 'Выйти',
              icon: const Icon(Icons.logout),
              onPressed: auth.signOut,
            ),
          ],
        ),
        body: RefreshIndicator(
          onRefresh: () async => setState(() {}),
          child: FutureBuilder<GetStockInheldListResponse>(
            future: auth.client.getStockInheldList(),
            builder: (context, snapshot) {
              if (snapshot.connectionState != ConnectionState.done) {
                return const Center(child: CircularProgressIndicator());
              }

              if (snapshot.hasError) {
                return Center(child: Text('${snapshot.error}'));
              }

              final positions = snapshot.data?.stockInheldSummaryList ?? const [];

              if (positions.isEmpty) {
                return const Center(child: Text('Остатков нет'));
              }

              return ListView.separated(
                itemCount: positions.length,
                separatorBuilder: (_, __) => const Divider(height: 1),
                itemBuilder: (context, index) {
                  final item = positions[index];
                  return ListTile(
                    title: Text(item.fullBrandName ?? ''),
                    subtitle: Text(item.gtin ?? ''),
                    trailing: Text('${item.amount}'),
                  );
                },
              );
            },
          ),
        ),
      );
}
```

### Доступные методы

```dart
final client = auth.client;

// Справочники
await client.getAllStakeholders();
await client.getMedicineList();                 // с lastUpdate для синхронизации

// Ввод в оборот
await client.importDeclaration(request);
await client.productionDeclaration(request);

// Перемещения
await client.transferDeclaration(request);
await client.transferAccept(declarationId);
await client.transferCancel(declarationId);
await client.transferReturn(request);
await client.transferReturnCancel(declarationId);
await client.getTransferDeclaration(declarationId);
await client.getTransferListByFilter(filter);

// Склад
await client.stockDeclaration(request);
await client.stockDeclarationCancel(declarationId);
await client.getStockInheldList();
await client.getStockInheldListByGtin(gtin);
await client.getPartialSaleInfo(qrCode);

// Реализация
await client.salesDeclaration(request);
await client.salesDeclarationCancel(declarationId);
await client.salesDeclarationBoxCancel(qrCode);

// Выбытие
await client.deactivateDeclaration(request);
```

Полное описание полей каждого запроса — в
[API-REFERENCE.md](API-REFERENCE.md). Пошаговые бизнес-сценарии «от импортёра
до аптеки» — в [SCENARIOS.md](SCENARIOS.md) (примеры там на C#, имена методов
и полей в Dart совпадают).

---

## Проверка лекарства по QR-коду

Проверка доступна **без авторизации** — это самый частый сценарий мобильного
приложения для покупателя. В ответе есть готовый вердикт, не нужно собирать
логику самостоятельно.

```dart
class MedicineCheckScreen extends StatefulWidget {
  const MedicineCheckScreen({required this.auth, super.key});

  final KnddbAuthController auth;

  @override
  State<MedicineCheckScreen> createState() => _MedicineCheckScreenState();
}

class _MedicineCheckScreenState extends State<MedicineCheckScreen> {
  final _qrCode = TextEditingController();
  ProductInquiryResult? _result;
  String? _error;
  bool _loading = false;

  @override
  void dispose() {
    _qrCode.dispose();
    super.dispose();
  }

  Future<void> _check() async {
    setState(() {
      _loading = true;
      _error = null;
      _result = null;
    });

    try {
      final result = await widget.auth.client.productInquiryByQrCode(_qrCode.text.trim());
      setState(() => _result = result);
    } on KnddbApiException catch (error) {
      setState(() {
        _error = error.isNotFound ? 'Упаковка не найдена' : error.message;
      });
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final result = _result;

    return Scaffold(
      appBar: AppBar(title: const Text('Проверка лекарства')),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          TextField(
            controller: _qrCode,
            decoration: const InputDecoration(
              labelText: 'Код Data Matrix',
              helperText: 'Отсканируйте или введите код с упаковки',
            ),
          ),
          const SizedBox(height: 12),
          FilledButton(
            onPressed: _loading ? null : _check,
            child: const Text('Проверить'),
          ),
          if (_error != null) ...[
            const SizedBox(height: 16),
            Text(_error!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
          ],
          if (result != null) ...[
            const SizedBox(height: 16),
            _VerdictCard(result: result),
          ],
        ],
      ),
    );
  }
}
```

### Вердикт для интерфейса

```dart
final (icon, color, action) = switch (result.verificationVerdict) {
  'recalled' => (
      Icons.dangerous,
      Colors.red,
      'Упаковка отозвана — не продавайте и сообщите в департамент',
    ),
  'expired' => (
      Icons.warning_amber,
      Colors.orange,
      'Срок годности истёк',
    ),
  'notForSale' => (
      Icons.info_outline,
      Colors.blueGrey,
      'Упаковка уже выбыла из оборота',
    ),
  _ => (
      Icons.verified,
      Colors.green,
      'Упаковка легальна и доступна к продаже',
    ),
};
```

`result.verificationMessage` уже содержит готовый текст: например,
«Упаковка отозвана: Брак серии».

### Что показывать на карточке

```dart
result.productName                  // наименование препарата
result.manufacturerName             // производитель
result.gtin                         // GTIN
result.serialNumber                 // серийный номер упаковки
result.batchNumber                  // номер партии
result.expirationDate               // срок годности
result.stakeHolderName              // текущий держатель упаковки
result.productState.label           // последнее событие
result.productStatus.label          // складской статус
result.partialSaleRemainingAmount   // остаток после частичной продажи
result.suspendRecallInfo?.reason    // причина отзыва
result.productInquiryHistory        // история движения упаковки
```

### Сканирование камерой

Для сканирования добавьте `mobile_scanner`:

```bash
flutter pub add mobile_scanner
```

```dart
MobileScanner(
  onDetect: (capture) {
    final code = capture.barcodes.firstOrNull?.rawValue;

    if (code != null) {
      _qrCode.text = code;
      _check();
    }
  },
)
```

---

## Несколько учётных записей

Если приложение обслуживает несколько организаций (например, сеть аптек),
каждая учётная запись хранится под своим ключом сессии.

### Вариант 1: переключение в рамках одного приложения

```dart
// Вход под каждой учётной записью при первом использовании
await auth.signIn(branchALogin, branchAPassword, sessionKey: 'branch:a');
await auth.signIn(branchBLogin, branchBPassword, sessionKey: 'branch:b');

// Переключение без обращения к сети
await auth.switchUser('branch:a');
final stockA = await auth.client.getStockInheldList();

await auth.switchUser('branch:b');
final stockB = await auth.client.getStockInheldList();
```

### Вариант 2: раздельное хранение на устройстве

```dart
final authA = KnddbAuthController(
  options: KnddbClientOptions(environment: KnddbEnvironment.production),
  storage: FlutterSecureSessionStorage(storageKey: 'knddb.session.branch-a'),
);

final authB = KnddbAuthController(
  options: KnddbClientOptions(environment: KnddbEnvironment.production),
  storage: FlutterSecureSessionStorage(storageKey: 'knddb.session.branch-b'),
);
```

### Временная работа от имени другого пользователя

Для операции, которую нужно выполнить один раз от чужого имени:

```dart
auth.client.beginUserScope('branch:b');
try {
  await auth.client.transferAccept(declarationId);
} finally {
  auth.client.endUserScope();
}
```

---

## Сохранение входа

`KnddbAuthController` по умолчанию использует `FlutterSecureSessionStorage`:
вход переживает перезапуск приложения. Восстановление — одна строка:

```dart
await _auth.restore();
```

Что происходит внутри:

1. Из защищённого хранилища читается сохранённая сессия.
2. Если токен доступа ещё действителен — состояние сразу
   `authenticated`.
3. Если токен истёк, SDK обновляет его по `refresh_token` при первом же
   запросе — пользователь не увидит экран входа.
4. Если `refresh_token` отклонён — состояние станет
   `unauthenticated`, и приложение покажет вход.

**Пароль не сохраняется.** В хранилище попадают только токены, логин и ключ
сессии — этого достаточно для продления по `refresh_token`. Это зафиксировано
тестом: в сохранённой записи нет полей `password` и `credentials`.

Если приложение не должно сохранять вход между запусками, реализуйте
`KnddbSessionStorage` с пустой реализацией `save` и `load`:

```dart
class NoPersistence implements KnddbSessionStorage {
  @override
  Future<void> save(KnddbSession session) async {}

  @override
  Future<KnddbSession?> load() async => null;

  @override
  Future<void> clear() async {}
}
```

---

## Обработка ошибок в интерфейсе

> **Важно для мобильных приложений.** Ошибки бизнес-логики KNMDB приходят
> **с HTTP-статусом 200** в конверте `{ resultCode, resultMessage, actionResult }`.
> Например, повторная продажа уже проданной упаковки — это `resultCode 6023`
> и HTTP 200. SDK распознаёт такие ответы и выбрасывает `KnddbApiException`
> с заполненным `resultCode`, поэтому в приложении достаточно ловить исключение.

### Операторские ошибки: показываем понятный текст

| Код | Что случилось | Что показать пользователю |
|---|---|---|
| `6022` | Упаковка не найдена | «Упаковка не найдена. Проверьте код и повторите сканирование» |
| `6023` | Упаковка не подходит для продажи | «Упаковка уже продана или не подходит для продажи» |
| `6004` | QR-код уже в системе | «Эта упаковка уже введена в оборот» |
| `6033` | Упаковка уже объявлена | «Упаковка уже объявлена ранее» |
| `6034` | Декларация другой организации | «Документ принадлежит другой организации» |
| `6039` | Пара «GTIN + серийный номер» не найдена | «Упаковка не найдена. Проверьте GTIN и серийный номер» |
| `6045` | Упаковку нельзя деактивировать | «Упаковка продана — деактивация невозможна» |
| `1` | Ошибка валидации | Показать поля из `error.errors` рядом с полями формы |

### Матрица по HTTP-статусам

| Ситуация | Что показать пользователю |
|---|---|
| `isForbidden` (403) | «Недостаточно прав. Обратитесь к администратору департамента лекарственных средств» — это задача администратора, не сбой приложения |
| `isProductNotFound` (404 **или** коды 6022/6039) | «Упаковка не найдена» — для проверки лекарства это нормальный результат |
| `isProductNotSuitableForSale` (код 6023) | «Упаковка уже продана или не подходит для продажи» |
| `isBadRequest` (400) | Показать поля из `error.errors` рядом с соответствующими полями формы |
| `isConflict` (409) | «Операция несовместима с текущим состоянием упаковки» — предложить обновить данные |
| `isTransientFailure` | «Сервер временно недоступен, повторите» — кнопка «Повторить» |
| `KnddbAuthenticationException` | Сбросить состояние и показать экран входа |

### Готовый разбор ошибки

```dart
/// Возвращает текст для показа пользователю.
String describeError(KnddbApiException error) => switch (error) {
      // Ошибки бизнес-логики (приходят с HTTP 200)
      _ when error.resultCode == KnddbResultCodes.productWithQrCodeNotFound =>
        'Упаковка не найдена. Проверьте код и повторите сканирование.',
      _ when error.resultCode == KnddbResultCodes.productWithQrCodeNotSuitableForSale =>
        'Упаковка уже продана или не подходит для продажи.',
      _ when error.resultCode == KnddbResultCodes.similarQrCodeInDatabase =>
        'Эта упаковка уже введена в оборот.',
      _ when error.resultCode == KnddbResultCodes.productAlreadyDeclared =>
        'Упаковка уже объявлена ранее.',
      _ when error.resultCode == KnddbResultCodes.declarationDoesntBelongToYou =>
        'Документ принадлежит другой организации.',
      _ when error.resultCode == KnddbResultCodes.productCannotBeDeactivated =>
        'Упаковка продана — деактивация невозможна.',

      // Любая другая ошибка бизнес-логики — русское описание кода
      _ when error.isBusinessError =>
        error.resultDescription ?? 'Операция отклонена сервером KNMDB.',

      // Ошибки уровня ASP.NET Core
      _ when error.isForbidden =>
        'Недостаточно прав. Обратитесь к администратору департамента '
            'лекарственных средств.',
      _ when error.isBadRequest && (error.errors?.isNotEmpty ?? false) =>
        error.errors!.entries.map((e) => '${e.key}: ${e.value}').join('\n'),
      _ when error.isTransientFailure =>
        'Сервер временно недоступен. Повторите позже.',

      _ => error.message,
    };
```

Использование в экране:

```dart
Future<void> _submit() async {
  try {
    await widget.auth.client.salesDeclaration(request);
    // ...
  } on KnddbAuthenticationException {
    await widget.auth.signOut();
  } on KnddbApiException catch (error) {
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(describeError(error))),
    );
  }
}
```

### Полный обработчик с журналированием

```dart
Future<void> _safeAction(Future<void> Function() action) async {
  try {
    await action();
  } on KnddbAuthenticationException {
    await _auth.signOut();   // сессия недействительна — нужен повторный вход
  } on KnddbApiException catch (error) {
    if (!mounted) return;

    final message = switch (error) {
      _ when error.isForbidden =>
        'Недостаточно прав. Обратитесь к администратору департамента '
            'лекарственных средств.',
      _ when error.isBadRequest && (error.errors?.isNotEmpty ?? false) =>
        error.errors!.entries.map((e) => '${e.key}: ${e.value}').join('\n'),
      _ when error.isTransientFailure => 'Сервер временно недоступен. Повторите позже.',
      _ => error.message,
    };

    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));
  }
}
```

### Журналирование для поддержки

`traceId` из ошибки помогает службе поддержки найти запрос:

```dart
} on KnddbApiException catch (error) {
  debugPrint('KNMDB ${error.statusCode} ${error.method} ${error.requestUri} '
      'traceId=${error.traceId}: ${error.message}');
}
```

---

## Сборка для разных платформ

```bash
# Android
flutter build apk --release
flutter build appbundle --release     # для Google Play

# iOS
flutter build ipa --release

# Windows
flutter build windows --release

# macOS
flutter build macos --release

# Linux
flutter build linux --release

# Web
flutter build web --release
```

### Проверка перед релизом

```bash
flutter analyze
flutter test
```

Для боевого контура не забудьте:

```dart
KnddbClientOptions(environment: KnddbEnvironment.production)
```

> **Осторожно.** На боевом контуре операции необратимы: созданные декларации
> влияют на фактический оборот лекарственных средств. Проверяйте интеграцию
> на тестовом контуре.

---

## Что дальше

| Документ | Для чего |
|---|---|
| [README пакета Flutter](https://github.com/taalaibekdev/KnmdbApi/tree/main/flutter/knddb_track_and_trace_flutter) | Краткий обзор, установка, хранение токенов |
| [Пример приложения](https://github.com/taalaibekdev/KnmdbApi/tree/main/flutter/knddb_track_and_trace_flutter/example) | Готовый экран входа и проверка лекарства |
| [README Dart-пакета](https://github.com/taalaibekdev/KnmdbApi/tree/main/dart/knddb_track_and_trace) | Клиент API без Flutter |
| [SCENARIOS.md](SCENARIOS.md) | Бизнес-сценарии «от импортёра до аптеки» |
| [API-REFERENCE.md](API-REFERENCE.md) | Все методы и поля |
| [ENUMS.md](ENUMS.md) | Значения перечислений |
