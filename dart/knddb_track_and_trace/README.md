# knddb_track_and_trace

Dart/Flutter SDK для **Track and Trace API** системы **KNMDB / KNDDB**
(Кыргызская национальная база лекарственных средств).

Работает в мобильных приложениях (Flutter, Android, iOS), desktop-приложениях
(Windows, macOS, Linux), веб-приложениях и консольных утилитах на Dart.

[![pub package](https://img.shields.io/pub/v/knddb_track_and_trace.svg)](https://pub.dev/packages/knddb_track_and_trace)
[![license](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

---

## Возможности

| Возможность | Описание |
|---|---|
| **Все методы API** | 23 метода Track and Trace + вход и выход OAuth 2.0 |
| **Автопродление токена** | По `refresh_token`, с автоматическим повторным входом |
| **Смена пользователя «на ходу»** | Ключи сессий и вкладываемые области |
| **Сохранение входа** | `KnddbSessionStorage` — вход переживает перезапуск приложения |
| **Типизированные модели** | Запросы и ответы с русской документацией каждого поля |
| **Минимум зависимостей** | Только `http` и `meta` |
| **Терпимый к формату разбор** | Перечисления принимаются как числа, имена и названия |

---

## Установка

```bash
flutter pub add knddb_track_and_trace
```

Или добавьте в `pubspec.yaml`:

```yaml
dependencies:
  knddb_track_and_trace: ^1.0.0
```

Требуется Dart **3.6** или новее.

---

## Контуры: тестовый и боевой

| Контур | Базовый адрес | Когда использовать |
|---|---|---|
| `KnddbEnvironment.test` | `https://testndbapi.med.kg/` | Разработка и отладка. **По умолчанию.** |
| `KnddbEnvironment.production` | `https://ndbapi.med.kg/` | Промышленная эксплуатация |

```dart
// Тестовый контур — значение по умолчанию.
final test = KnddbClientOptions();

// Боевой контур.
final production = KnddbClientOptions(environment: KnddbEnvironment.production);

print(production.effectiveBaseUrl);   // https://ndbapi.med.kg/
print(production.isProduction);       // true
```

> **Важно.** На боевом контуре все операции реальны и необратимы: созданные
> декларации влияют на фактический оборот лекарственных средств. По умолчанию
> SDK использует тестовый контур — так случайная ошибка в конфигурации
> не затронет промышленные данные.

Адрес можно задать вручную для локального стенда или прокси:

```dart
final options = KnddbClientOptions(baseUrl: 'http://localhost:5001/');
```

---

## Быстрый старт

```dart
import 'package:knddb_track_and_trace/knddb_track_and_trace.dart';

Future<void> main() async {
  final client = KnddbApiClient(
    options: KnddbClientOptions(environment: KnddbEnvironment.test),
  );

  // Вход
  await client.signIn('ваш_логин', 'ваш_пароль');

  // Список организаций
  final stakeholders = await client.getAllStakeholders();
  for (final organization in stakeholders.stakeholders ?? const []) {
    print('${organization.code}: ${organization.name} (${organization.type.label})');
  }

  client.close();
}
```

### Проверка лекарства без авторизации

```dart
final client = KnddbApiClient();

final medicine = await client.productInquiryByQrCode('010460000000001721SN...');

print('${medicine.productName}, годен до ${medicine.expirationDate}');
print(medicine.verificationMessage);   // «Упаковка легальна и доступна к продаже»

switch (medicine.verificationVerdict) {
  case 'recalled':
    // Упаковка отозвана — показываем красный экран
    break;
  case 'expired':
    // Срок годности истёк
    break;
  case 'ok':
    // Всё в порядке
    break;
}

client.close();
```

---

## Права доступа

Роли API настраиваются администратором департамента лекарственных средств
через административную панель KNMDB. Интеграции достаточно логина и пароля:
запрашивать или проверять права не нужно.

Если метод вернул `403`, обратитесь к администратору департамента
лекарственных средств:

```dart
try {
  await client.importDeclaration(request);
} on KnddbApiException catch (error) {
  if (error.isForbidden) {
    // Показываем пользователю: вопрос к администратору, а не сбой приложения
  }
}
```

---

## Смена пользователя «на ходу»

```dart
final client = KnddbApiClient(
  options: KnddbClientOptions(environment: KnddbEnvironment.production),
);

// Два пользователя в одном клиенте
await client.signIn('pharmacist', 'secret', sessionKey: 'user:42');
await client.signIn('warehouse', 'secret', sessionKey: 'user:77');

// Переключиться без обращения к сети
client.useSession('user:42');
final stock1 = await client.getStockInheldList();

// Временная область — от имени другого пользователя
client.beginUserScope('user:77');
try {
  final stock2 = await client.getStockInheldList();
} finally {
  client.endUserScope();
}

// Проверки
print(client.currentSessionKey);    // user:42
print(client.currentUserName);      // pharmacist
print(client.isAuthenticated);      // true

// Выход одного пользователя — остальные сессии сохраняются
await client.signOutSession('user:42');
```

Каждая сессия хранит собственные токены, поэтому переключение пользователя
не требует повторного запроса токена и не влияет на других пользователей.

---

## Сохранение входа между запусками

Реализуйте `KnddbSessionStorage` поверх защищённого хранилища платформы —
готовый адаптер для Flutter есть в
[`flutter/knddb_track_and_trace_flutter`](https://github.com/taalaibekdev/KnmdbApi/tree/main/flutter/knddb_track_and_trace_flutter).

```dart
final client = KnddbApiClient(
  options: KnddbClientOptions(environment: KnddbEnvironment.production),
  storage: FlutterSecureSessionStorage(),
);

// При старте приложения
if (await client.restoreSession()) {
  // Пользователь уже вошёл — показываем основной экран
} else {
  // Нужен вход
  await client.signIn(login, password);
}
```

Пароль на диск не сохраняется: в хранилище попадают только токены и логин.

---

## Обработка ошибок

> **Главное, что нужно знать об API KNMDB.** Ошибки бизнес-логики приходят
> **с HTTP-статусом 200** в конверте `{ resultCode, resultMessage, actionResult }`.
> Отличить успех от ошибки по статусу невозможно — признак ошибки это
> ненулевой `resultCode`.
>
> SDK проверяет `resultCode` сам и выбрасывает `KnddbApiException`, поэтому
> достаточно ловить исключение: код, проверяющий только статус, получил бы
> «успех» и пустой объект данных.

```dart
try {
  await client.salesDeclaration(request);
} on KnddbApiException catch (error) when (error.isBusinessError) {
  // HTTP-статус здесь 200, а причина — в resultCode
  switch (error.resultCode) {
    case KnddbResultCodes.productWithQrCodeNotFound:            // 6022
      print('Упаковка с таким QR-кодом не найдена в системе');
    case KnddbResultCodes.productWithQrCodeNotSuitableForSale:  // 6023
      print('Упаковка уже продана или не подходит для продажи');
    default:
      print(error.resultDescription);   // русское описание кода
  }
}
```

### Операторские ошибки

| Код | Что случилось | Что показать пользователю |
|---|---|---|
| `6022` | Упаковка не найдена | «Упаковка не найдена. Проверьте код и повторите сканирование» |
| `6023` | Упаковка не подходит для продажи | «Упаковка уже продана или не подходит для продажи» |
| `6004` | QR-код уже в системе | «Эта упаковка уже введена в оборот» |
| `6033` | Упаковка уже объявлена | «Упаковка уже объявлена ранее» |
| `6034` | Декларация другой организации | «Документ принадлежит другой организации» |
| `6045` | Упаковку нельзя деактивировать | «Упаковка продана — деактивация невозможна» |

### Общая обработка

```dart
try {
  await client.importDeclaration(request);
} on KnddbAuthenticationException catch (error) {
  // 401 или ошибка входа — нужен повторный вход
  await client.signIn(login, password);
} on KnddbApiException catch (error) {
  if (error.isProductNotFound) {
    // HTTP 404 или код 6022/6039 — удобно для проверки лекарства
    print('Упаковка не найдена');
  } else if (error.isProductNotSuitableForSale) {
    print('Упаковка уже продана');
  } else if (error.isBusinessError) {
    print('${error.resultCode}: ${error.resultDescription}');
  } else if (error.isForbidden) {
    // Права настраиваются администратором департамента
    print('Нужны права: обратитесь к администратору департамента');
  } else {
    print('HTTP ${error.statusCode}: ${error.message}');
    print('traceId: ${error.traceId}');

    for (final entry in (error.errors ?? const {}).entries) {
      print('  ${entry.key}: ${entry.value}');
    }
  }
}
```

### Свойства исключения

| Свойство | Описание |
|---|---|
| `resultCode` | **Код результата из конверта.** `null`, если конверта в ответе не было |
| `resultMessage` | Сообщение сервера из конверта (при ошибке — на английском) |
| `isBusinessError` | Конверт с ненулевым `resultCode` — ошибка бизнес-логики с HTTP 200 |
| `isProductNotFound` | HTTP 404 **или** код 6022/6039 |
| `isProductNotSuitableForSale` | Код 6023 |
| `resultDescription` | Русское описание кода; если код неизвестен — `resultMessage` |
| `statusCode` | HTTP-статус; для ошибок бизнес-логики — **200** |
| `isNotFound`, `isForbidden`, `isBadRequest`, `isConflict` | HTTP 404 / 403 / 400 / 409 |
| `isTransientFailure` | 5xx или 408 — имеет смысл повторить |
| `traceId`, `errors`, `problemDetails` | Диагностика для поддержки |

| Тип исключения | Когда возникает |
|---|---|
| `KnddbApiException` | Неуспешный HTTP-статус **или** ненулевой `resultCode` в конверте |
| `KnddbAuthenticationException` | 401, неверный логин или пароль, отсутствие учётных данных, ошибка на `/connect/token` |
| `KnddbConfigurationException` | Настройки некорректны, сессия не найдена |

### Если что-то не работает

| Симптом | Причина | Что делать |
|---|---|---|
| Вход падает с `resultCode 2` «An error occurred while saving the entity changes» | Не отправлен заголовок `user-agent`: сервер записывает его в базу при входе | SDK отправляет его всегда. Если задаёте свой — проверьте, что он не пустой |
| Метод с датой падает с `resultCode 2` | Дата передана с временем (`2026-03-15T12:00:00Z`) | SDK отбрасывает время автоматически; сервер принимает только `yyyy-MM-dd` |
| Код считает ошибку успехом и получает пустой объект | Проверяется только HTTP-статус, а ошибка пришла с HTTP 200 | Проверяйте `resultCode` или ловите `KnddbApiException` |

Полный список кодов результата:
[API-REFERENCE.md, Коды результата](https://github.com/taalaibekdev/KnmdbApi/blob/main/docs/API-REFERENCE.md#коды-результата).


---

## Методы API — краткий обзор

| Метод | HTTP |
|---|---|
| `getAllStakeholders()` | `GET .../GetAllStakeholders` |
| `getMedicineList(request?)` | `POST .../GetMedicineList` |
| `productInquiryByQrCode(qrCode)` | `POST .../ProductInquiryQRCode` — без авторизации |
| `productInquiryByGtinSn(gtin, serialNumber)` | `POST .../ProductInquiryGtinSn` — без авторизации |
| `importDeclaration(request)` | `POST .../ImportDeclaration` |
| `productionDeclaration(request)` | `POST .../ProductionDeclaration` |
| `transferDeclaration(request)` | `POST .../TransferDeclaration` |
| `transferAccept(declarationId)` | `POST .../TransferAccept` |
| `transferCancel(declarationId)` | `POST .../TransferCancel` |
| `transferReturn(request)` | `POST .../TransferReturn` |
| `transferReturnCancel(declarationId)` | `POST .../TransferReturnCancel` |
| `getTransferDeclaration(declarationId)` | `GET .../GetTransferDeclaration` |
| `getTransferListByFilter(request?)` | `POST .../GetTransferListByFilter` |
| `stockDeclaration(request)` | `POST .../StockDeclaration` |
| `stockDeclarationCancel(declarationId)` | `POST .../StockDeclarationCancel` |
| `getStockInheldList()` | `POST .../GetStockInheldList` |
| `getStockInheldListByGtin(gtin)` | `POST .../GetStockInheldListByGtin` |
| `getPartialSaleInfo(qrCode)` | `POST .../GetPartialSaleInfo` |
| `salesDeclaration(request)` | `POST .../SalesDeclaration` |
| `salesDeclarationCancel(declarationId)` | `POST .../SalesDeclarationCancel` |
| `salesDeclarationBoxCancel(qrCode)` | `POST .../SalesDeclarationBoxCancel` |
| `deactivateDeclaration(request)` | `POST .../DeactivateDeclaration` |

### Устаревшее (deprecated)

| Метод | Причина | Что делать вместо него |
|---|---|---|
| `getSupportedQrTypes()` | Метод `GET /api/TrackAndTrace/GetSupportedQRTypes` исключён из API департамента лекарственных средств | Значение `qrTypeId` согласовать с департаментом и задать константой в конфигурации |

Метод и типы `GetSupportedQRTypesResponse`, `QRTypeInfo` помечены `@Deprecated`
и будут удалены из пакета, когда окончательно исчезнут из API.

---

## Перечисления

Все перечисления содержат числовое значение сервера, человекочитаемое название
и метод `fromWire`, принимающий число, имя члена или название:

```dart
StakeholderType.fromWire(5);               // StakeholderType.pharmacy
StakeholderType.fromWire('Pharmacy');      // StakeholderType.pharmacy
ProductState.fromWire('Transfer Initiated'); // ProductState.transferInitiated
ConsumptionType.fromWire(40);              // ConsumptionType.disposalForDeadline
```

| Перечисление | Значения |
|---|---|
| `StakeholderType` | `producer` (1) … `warehouse` (7) |
| `ProductState` | `production` (1) … `deactivationCancelled` (18) |
| `ProductStatus` | `inStock` (1), `stockOut` (2), `inTransfer` (3) |
| `TransferState` | `initiated` (1), `accepted` (2), `cancelled` (3) |
| `TransferType` | `initiate` (1), `accept` (2) |
| `ConsumptionType` | `systemIn` (0) … `sample` (100) |
| `TrackAndTraceStatus` | `notTracked` (1), `labelMandatory` (2), `labelAndTraceMandatory` (3) |

Для причин списания есть готовое русское название:

```dart
print(ConsumptionType.disposalForDeadline.reasonRu);   // «Истёк срок годности»
```

Полное описание значений — в
[docs/ENUMS.md](https://github.com/taalaibekdev/KnmdbApi/blob/main/docs/ENUMS.md).

---

## Документация

| Документ | Для чего |
|---|---|
| **README.md** (этот файл) | Установка, контуры, быстрый старт, смена пользователя |
| [FLUTTER.md](https://github.com/taalaibekdev/KnmdbApi/blob/main/docs/FLUTTER.md) | **Для приложений Flutter**: экраны входа, хранение токенов, сборка |
| [SCENARIOS.md](https://github.com/taalaibekdev/KnmdbApi/blob/main/docs/SCENARIOS.md) | **Начните отсюда для интеграции**: пошаговые сценарии «от импортёра до аптеки» с кодом |
| [API-REFERENCE.md](https://github.com/taalaibekdev/KnmdbApi/blob/main/docs/API-REFERENCE.md) | Все методы, каждое поле запросов и ответов |
| [ENUMS.md](https://github.com/taalaibekdev/KnmdbApi/blob/main/docs/ENUMS.md) | Все перечисления: числа, названия, пояснения |

Документация описывает серверный API, поэтому примеры в ней приведены на C#
(эталонный SDK). Имена методов и полей моделей в Dart совпадают с C# —
отличается только регистр: `GetAllStakeholdersAsync` → `getAllStakeholders`.

### Работаете с Flutter?

Этот пакет — чистый Dart: он не знает о защищённых хранилищах платформы
и не содержит элементов интерфейса. Для приложения Flutter добавьте пакет
[`knddb_track_and_trace_flutter`](https://github.com/taalaibekdev/KnmdbApi/tree/main/flutter/knddb_track_and_trace_flutter):
он даёт сохранение входа между запусками (Keychain, Android Keystore, DPAPI)
и готовый `KnddbAuthController` для экрана входа. Полное руководство —
[FLUTTER.md](https://github.com/taalaibekdev/KnmdbApi/blob/main/docs/FLUTTER.md).

---

## Лицензия

MIT — см. [LICENSE](LICENSE).
