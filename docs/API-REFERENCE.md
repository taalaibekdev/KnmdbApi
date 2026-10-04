# API Reference — полный справочник методов и моделей

Все методы `KnddbApiClient` системы **KNMDB Track and Trace** с описанием каждого поля.

- [Общие правила](#общие-правила)
- [Аутентификация](#аутентификация)
- [Работа с сессиями](#работа-с-сессиями)
- [Методы API](#методы-api)
  - [Справочники](#справочники)
  - [Проверка лекарственного средства (без авторизации)](#проверка-лекарственного-средства-без-авторизации)
  - [Ввод в оборот](#ввод-в-оборот)
  - [Перемещение между организациями](#перемещение-между-организациями)
  - [Складской учёт](#складской-учёт)
  - [Реализация (продажа и расход)](#реализация-продажа-и-расход)
  - [Выбытие (деактивация)](#выбытие-деактивация)
- [Устаревшее (deprecated)](#устаревшее-deprecated)
- [Модели запросов](#модели-запросов)
- [Модели ответов](#модели-ответов)
- [Исключения](#исключения)

---

## Общие правила

| Правило | Описание |
|---|---|
| **Формат дат** | Поля-даты передаются как `yyyy-MM-dd` (SDK делает это автоматически через конвертер). Не переопределяйте сериализацию. |
| **Формат перечислений** | Числа. При чтении SDK дополнительно принимает строки — см. [ENUMS.md](ENUMS.md). |
| **Имена полей** | `camelCase` (SDK настраивает автоматически). |
| **Пустой ответ** | Метод возвращает `null`, если сервер вернул пустое тело. |
| **Ошибка** | Выбрасывается `KnddbApiException` (см. [Исключения](#исключения)). |
| **`CancellationToken`** | Последний параметр каждого метода. |
| **Смена пользователя** | Каждый метод выполняется от имени текущей сессии — см. [Работа с сессиями](#работа-с-сессиями). |
| **Права доступа** | Роли API настраиваются администратором департамента лекарственных средств через административную панель KNMDB. Отдельно запрашивать и проверять их не нужно. |

> **Ошибка 403.** Если метод вернул 403, обратитесь к **администратору департамента
> лекарственных средств** — права настраиваются на стороне KNMDB, со стороны интеграции
> правки не требуются.

---

## Аутентификация

### `POST /connect/token` — получение токена

Тело запроса: `application/x-www-form-urlencoded`.

| Поле | Тип | Обязательно | Описание |
|---|---|---|---|
| `grant_type` | string | да | `password` — вход по логину и паролю; `refresh_token` — обновление |
| `username` | string | при `password` | Логин пользователя KNMDB |
| `password` | string | при `password` | Пароль пользователя KNMDB |
| `refresh_token` | string | при `refresh_token` | Ранее полученный токен обновления |
| `scope` | string | нет | Области доступа; SDK запрашивает `api offline_access` |

Ответ:

| Поле | Тип | Описание |
|---|---|---|
| `access_token` | string | JWT для заголовка `Authorization: Bearer ...` |
| `token_type` | string | Тип токена, всегда `Bearer` |
| `expires_in` | int | Время жизни токена доступа **в секундах** |
| `refresh_token` | string | Токен обновления (при наличии `offline_access`) |
| `scope` | string | Выданные области доступа |
| `id_token` | string | Токен идентификации OpenID Connect (SDK не использует) |

Проверка через curl:

```bash
curl -H "content-type: application/x-www-form-urlencoded" \
     -d "grant_type=password&username=<логин>&password=<пароль>" \
     https://testndbapi.med.kg/connect/token
```

### `POST /connect/logout` — выход

SDK очищает токены локально в любом случае, даже если сервер вернул ошибку.

### Автоматическое продление токена

1. Токен обновляется за `KnddbClientOptions.TokenExpirationMargin` (по умолчанию 30 секунд) до истечения.
2. Если `refresh_token` отклонён — SDK автоматически повторяет вход по сохранённым учётным данным.
3. При ответе **401** запрос повторяется один раз с принудительно обновлённым токеном.
4. Чтобы не держать пароль в памяти: `SignInAsync(..., storeCredentials: false)`.

---

## Работа с сессиями

Один экземпляр `KnddbApiClient` обслуживает несколько учётных записей. Состояние входа
хранится в `KnddbSession` по ключу.

| Метод | Описание |
|---|---|
| `SignInAsync(userName, password, sessionKey?, storeCredentials?, ct)` | Вход; делает сессию текущей |
| `UseSession(sessionKey)` | Переключиться на существующую сессию без обращения к сети |
| `UseSessionAsync(sessionKey, credentials?, ct)` | Переключиться, при необходимости выполнив вход |
| `BeginUserScope(sessionKey)` | Временная область от имени другого пользователя (`IDisposable`) |
| `BeginUserScopeAsync(sessionKey, credentials?, ct)` | То же, с входом при необходимости |
| `SignOutAsync(ct)` | Выход текущего пользователя |
| `SignOutAsync(sessionKey, ct)` | Выход указанного пользователя |
| `FindSession(sessionKey)` | Сессия по ключу или `null` |
| `CurrentSession` | Текущая сессия или `null` |
| `CurrentUserName` | Логин текущего пользователя |
| `CurrentSessionKey` | Ключ текущей сессии |
| `IsAuthenticated` | Готов ли клиент выполнить запрос |
| `Environment` | Контур подключения (`Test` / `Production`) |
| `BaseAddress` | Адрес сервера |

### Свойства `KnddbSession`

| Свойство | Тип | Описание |
|---|---|---|
| `Key` | string | Ключ сессии |
| `UserName` | string? | Логин пользователя |
| `Credentials` | `KnddbCredentials?` | Учётные данные для автопродления |
| `AccessToken` | string? | Токен доступа |
| `RefreshToken` | string? | Токен обновления |
| `TokenType` | string | Тип токена (`Bearer`) |
| `AccessTokenExpiresAt` | `DateTimeOffset` | Момент истечения токена (UTC) |
| `LastRefreshedAt` | `DateTimeOffset` | Момент последнего обновления (UTC) |
| `Scope` | string? | Выданные области доступа |
| `IsAuthenticated` | bool | Есть ли токен доступа |
| `IsAccessTokenValid(margin?, utcNow?)` | bool | Действителен ли токен |

---

## Методы API

### Справочники

#### `GetAllStakeholdersAsync` — все организации

| | |
|---|---|
| HTTP | `GET /api/TrackAndTrace/GetAllStakeholders` |
| Результат | `GetAllStakeholdersResponse` |

Коды организаций используются в `StakeholderCode`, `FromStakeholder`, `ToStakeholder`.

```csharp
var response = await client.GetAllStakeholdersAsync();
```

#### ~~`GetSupportedQrTypesAsync` — типы QR-кодов~~ (устаревший)

| | |
|---|---|
| HTTP | `GET /api/TrackAndTrace/GetSupportedQRTypes` |
| Результат | `GetSupportedQRTypesResponse` |
| Статус | **Deprecated** — см. [Устаревшее](#устаревшее-deprecated) |

Метод исключён из API департамента лекарственных средств и в SDK помечен
`[Obsolete]`. Не используйте его в новой интеграции: значение `qrTypeId` согласуйте
с департаментом лекарственных средств и задайте константой в конфигурации приложения.

#### `GetMedicineListAsync` — справочник препаратов

| | |
|---|---|
| HTTP | `POST /api/TrackAndTrace/GetMedicineList` |
| Запрос | `GetMedicineListRequest` |
| Результат | `GetMedicineListResponse` |

Для инкрементальной синхронизации передавайте максимальное значение `LastUpdate`
из предыдущего ответа. Пример — в [SCENARIOS.md](SCENARIOS.md#синхронизация-справочника-лекарств).

### Проверка лекарственного средства (без авторизации)

#### `ProductInquiryByQrCodeAsync` — по QR-коду

| | |
|---|---|
| HTTP | `POST /api/TrackAndTrace/ProductInquiryQRCode` |
| Авторизация | **не требуется** |
| Параметры | `string qrCode` |
| Результат | `ProductInquiryResult` |

#### `ProductInquiryByGtinSnAsync` — по GTIN и серийному номеру

| | |
|---|---|
| HTTP | `POST /api/TrackAndTrace/ProductInquiryGtinSn` |
| Авторизация | **не требуется** |
| Параметры | `string gtin`, `string serialNumber` |
| Результат | `ProductInquiryResult` |

Если упаковка не найдена — сервер возвращает **404**, `KnddbApiException.IsNotFound == true`.

### Ввод в оборот

#### `ImportDeclarationAsync` — декларация импорта

| | |
|---|---|
| HTTP | `POST /api/TrackAndTrace/ImportDeclaration` |
| Запрос | `ImportDeclarationRequest` |
| Результат | `ImportDeclarationResponse` |

#### `ProductionDeclarationAsync` — декларация производства

| | |
|---|---|
| HTTP | `POST /api/TrackAndTrace/ProductionDeclaration` |
| Запрос | `ProductionDeclarationRequest` |
| Результат | `ProductionDeclarationResponse` |

### Перемещение между организациями

| Метод | HTTP | Параметры → Результат |
|---|---|---|
| `TransferDeclarationAsync` | `POST .../TransferDeclaration` | `TransferDeclarationRequest` → `TransferDeclarationResponse` |
| `TransferAcceptAsync` | `POST .../TransferAccept` | `long declarationId` → `TransferDeclarationResponse` |
| `TransferCancelAsync` | `POST .../TransferCancel` | `long declarationId` → `TransferDeclarationCancelResponse` |
| `TransferReturnAsync` | `POST .../TransferReturn` | `TransferReturnRequest` → `TransferDeclarationResponse` |
| `TransferReturnCancelAsync` | `POST .../TransferReturnCancel` | `long declarationId` → `TransferDeclarationCancelResponse` |
| `GetTransferDeclarationAsync` | `GET .../GetTransferDeclaration?declarationId=` | `long declarationId` → `GetTransferDeclarationResponse` |
| `GetTransferListByFilterAsync` | `POST .../GetTransferListByFilter` | `GetTransferListByFilterRequest?` → `GetTransferListByFilterResponse` |

> `GetTransferListByFilterAsync()` без аргументов вернёт список без фильтров.
> `GetMedicineListAsync()` без аргументов вернёт весь справочник.

### Складской учёт

| Метод | HTTP | Параметры → Результат |
|---|---|---|
| `StockDeclarationAsync` | `POST .../StockDeclaration` | `StockDeclarationRequest` → `StockDeclarationResponse` |
| `StockDeclarationCancelAsync` | `POST .../StockDeclarationCancel` | `long declarationId` → `StockDeclarationCancelResponse` |
| `GetStockInheldListAsync` | `POST .../GetStockInheldList` | — → `GetStockInheldListResponse` |
| `GetStockInheldListByGtinAsync` | `POST .../GetStockInheldListByGtin` | `string gtin` → `GetStockInheldListByGtinResponse` |
| `GetPartialSaleInfoAsync` | `POST .../GetPartialSaleInfo` | `string qrCode` → `GetPartialSaleInfoResponse` |

> `StockDeclarationCancelResponse.IsSuccess` может быть `false` при HTTP-статусе 200 —
> проверяйте его и поле `Message`.

### Реализация (продажа и расход)

| Метод | HTTP | Параметры → Результат |
|---|---|---|
| `SalesDeclarationAsync` | `POST .../SalesDeclaration` | `SalesDeclarationRequest` → `SalesDeclarationResponse` |
| `SalesDeclarationCancelAsync` | `POST .../SalesDeclarationCancel` | `long declarationId` → `SalesDeclarationResponse` |
| `SalesDeclarationBoxCancelAsync` | `POST .../SalesDeclarationBoxCancel` | `string qrCode` → `SalesDeclarationResponse` |

### Выбытие (деактивация)

#### `DeactivateDeclarationAsync` — списание упаковок

| | |
|---|---|
| HTTP | `POST /api/TrackAndTrace/DeactivateDeclaration` |
| Запрос | `DeactivateDeclarationRequest` |
| Результат | `DeactivateDeclarationResponse` |

---

## Устаревшее (deprecated)

### `GetSupportedQrTypesAsync` и типы QR-кодов

| Что помечено | Как помечено |
|---|---|
| `KnddbApiClient.GetSupportedQrTypesAsync` | `[Obsolete(DiagnosticId = "KNMDB0001")]` |
| `GetSupportedQRTypesResponse` | `[Obsolete(DiagnosticId = "KNMDB0001")]` |
| `QRTypeInfo` | `[Obsolete(DiagnosticId = "KNMDB0001")]` |

**Причина.** Метод `GET /api/TrackAndTrace/GetSupportedQRTypes` исключён из API департамента
лекарственных средств.

**Что делать.** Метод и оба типа будут удалены из SDK, когда окончательно исчезнут из API.
Новую интеграцию на них не закладывайте:

1. Значение `qrTypeId` согласуйте с департаментом лекарственных средств.
2. Задайте его в конфигурации приложения как обычную константу.
3. Не вызывайте `GetSupportedQrTypesAsync()` в рабочем коде.

```csharp
// Вместо вызова устаревшего метода — значение из конфигурации.
// appsettings.json: { "Knddb": { "QrTypeId": 1 } }

public sealed class KnddbDeclarationSettings
{
    /// <summary>Тип QR-кода (формата Data Matrix), согласованный с департаментом.</summary>
    public required int QrTypeId { get; init; }
}
```

Потребитель, который всё же вызывает устаревший метод, получит предупреждение компилятора
с кодом **KNMDB0001** (не ошибку — сборка продолжится).

---

## Модели запросов

### `ImportDeclarationRequest`

| Поле | Тип | Обяз. | Описание |
|---|---|---|---|
| `Gtin` | string | **да** | GTIN препарата, 14 знаков |
| `BatchNo` | string | **да** | Номер партии (серии) |
| `ExpirationDate` | `DateTimeOffset` | **да** | Дата истечения срока годности, `yyyy-MM-dd` |
| `ProductionDate` | `DateTimeOffset` | **да** | Дата производства, `yyyy-MM-dd` |
| `DocumentDate` | `DateTimeOffset` | **да** | Дата документа-основания, `yyyy-MM-dd` |
| `DocumentNo` | string | **да** | Номер документа-основания |
| `Price` | decimal | **да** | Цена за единицу препарата |
| `QrTypeId` | int | **да** | Тип QR-кода из `GetSupportedQrTypesAsync` |
| `QrCodes` | `IList<string>` | **да** | Список QR-кодов регистрируемой партии |
| `Description` | string? | нет | Произвольный комментарий |
| `ImportApplicationRegistrationNumber` | long? | нет | Регистрационный номер заявки на импорт |
| `ImporterCompanyTaxNumber` | string? | нет | ИНН компании-импортёра |
| `StakeholderCode` | long? | нет | Код склада, на который приходуется партия |

### `ProductionDeclarationRequest`

| Поле | Тип | Описание |
|---|---|---|
| `Gtin` | string? | GTIN препарата |
| `BatchNo` | string? | Номер партии |
| `ExpirationDate` | `DateTimeOffset` | Дата истечения срока годности, `yyyy-MM-dd` |
| `ProductionDate` | `DateTimeOffset` | Дата производства, `yyyy-MM-dd` |
| `DocumentDate` | `DateTimeOffset` | Дата документа, `yyyy-MM-dd` |
| `DocumentNo` | string? | Номер документа |
| `DeclarationDate` | `DateTimeOffset` | Дата декларации, `yyyy-MM-dd` |
| `Price` | decimal | Цена за единицу |
| `QrTypeId` | int | Тип QR-кода |
| `QrCodes` | `IList<string>`? | Список QR-кодов |
| `Description` | string? | Комментарий |
| `StakeholderCode` | long? | Код производителя |

### `TransferDeclarationRequest` / `TransferDeclarationDetail`

| Поле | Тип | Описание |
|---|---|---|
| `FromStakeholder` | long | Код организации-отправителя |
| `ToStakeholder` | long | Код организации-получателя |
| `DeclarationDate` | `DateTimeOffset` | Дата перемещения, `yyyy-MM-dd` |
| `DocumentNo` | string? | Номер сопроводительного документа |
| `DocumentDate` | `DateTimeOffset?` | Дата документа, `yyyy-MM-dd` |
| `Description` | string? | Комментарий |
| `Details` | `IList<TransferDeclarationDetail>`? | Передаваемые упаковки |
| `TransferDeclarationDetail.QrCode` | string? | QR-код упаковки |
| `TransferDeclarationDetail.Price` | decimal | Цена упаковки |

### `TransferReturnRequest` / `TransferReturnRequestDetail`

| Поле | Тип | Описание |
|---|---|---|
| `DocumentNo` | string? | Номер документа возврата |
| `DocumentDate` | `DateTimeOffset?` | Дата документа, `yyyy-MM-dd` |
| `Description` | string? | Комментарий |
| `Details` | `IList<TransferReturnRequestDetail>`? | Возвращаемые упаковки |
| `TransferReturnRequestDetail.QrCode` | string? | QR-код упаковки |

### `StockDeclarationRequest` / `StockDeclarationDetail`

| Поле | Тип | Описание |
|---|---|---|
| `DeclarationDate` | `DateTimeOffset` | Дата декларации, `yyyy-MM-dd` |
| `StakeholderCode` | long | Код склада |
| `Description` | string? | Комментарий |
| `Details` | `IList<StockDeclarationDetail>`? | Упаковки; если пусто — вся партия |
| `StockDeclarationDetail.BatchNumber` | string? | Номер партии упаковки |
| `StockDeclarationDetail.ExpirationDate` | `DateTimeOffset?` | Срок годности, `yyyy-MM-dd` |
| `StockDeclarationDetail.QrCode` | string? | QR-код упаковки |

### `SalesDeclarationRequest` / `SalesDeclarationDetail`

| Поле | Тип | Описание |
|---|---|---|
| `PrescriptionId` | string? | Идентификатор рецепта |
| `PatientId` | string? | ПИН гражданина |
| `RequestNumber` | string? | Номер требования (только для больниц) |
| `BranchDefId` | string? | Идентификатор подразделения (только для больниц) |
| `DepartmentName` | string? | Наименование отделения (только для больниц) |
| `IsPharmacyConsumption` | bool | `true` — аптечная продажа, `false` — расход медорганизации |
| `Details` | `IList<SalesDeclarationDetail>`? | Реализуемые упаковки |
| `SalesDeclarationDetail.QrCode` | string? | QR-код упаковки |
| `SalesDeclarationDetail.Price` | decimal | Цена реализации |
| `SalesDeclarationDetail.IsPartialSale` | bool | Признак частичной продажи |
| `SalesDeclarationDetail.PartialSaleAmount` | decimal | Количество при частичной продаже |

### `DeactivateDeclarationRequest` / `DeactivateDeclarationDetail`

| Поле | Тип | Описание |
|---|---|---|
| `ConsumptionType` | `ConsumptionType` | Тип расходной операции (см. [ENUMS.md](ENUMS.md#consumptiontype)) |
| `Description` | string? | Комментарий / номер акта |
| `Details` | `IList<DeactivateDeclarationDetail>`? | Списываемые упаковки |
| `DeactivateDeclarationDetail.QrCode` | string? | QR-код упаковки |

### `GetTransferListByFilterRequest`

| Поле | Тип | Описание |
|---|---|---|
| `DeclarationId` | long? | Идентификатор перемещения |
| `DocumentNo` | string? | Номер документа |
| `DocumentDate` | `DateTimeOffset?` | Дата документа |
| `DeclarationDateFrom` | `DateTimeOffset?` | Начало диапазона дат декларации |
| `DeclarationDateTo` | `DateTimeOffset?` | Конец диапазона дат декларации |
| `CurrentState` | `TransferState?` | Состояние перемещения |
| `TransferType` | `TransferType?` | Роль организации в перемещении |
| `IsReturn` | bool? | Только возвратные перемещения |

### `GetMedicineListRequest`

| Поле | Тип | Описание |
|---|---|---|
| `LastUpdate` | `DateTimeOffset?` | Вернуть препараты, изменённые после этой даты (ISO-8601) |

### `GetStockInheldListByGtinRequest` / `GetPartialSaleInfoRequest`

| Поле | Тип | Описание |
|---|---|---|
| `Gtin` | string? | GTIN препарата |
| `QrCode` | string? | QR-код упаковки |

---

## Модели ответов

### `GetAllStakeholdersResponse` / `StakeholderInfo`

| Поле | Тип | Описание |
|---|---|---|
| `NumberOfStakeholders` | int | Общее количество организаций |
| `Stakeholders` | `IList<StakeholderInfo>`? | Список организаций |
| `StakeholderInfo.Code` | long | Уникальный код организации — используется в других запросах |
| `StakeholderInfo.Name` | string? | Полное наименование |
| `StakeholderInfo.Type` | `StakeholderType` | Тип организации |
| `StakeholderInfo.TaxNumber` | string? | ИНН |
| `StakeholderInfo.Address` | string? | Адрес |
| `StakeholderInfo.City` | string? | Город |
| `StakeholderInfo.District` | string? | Район (область) |
| `StakeholderInfo.ParentCode` | long? | Код головной организации |
| `StakeholderInfo.ParentName` | string? | Наименование головной организации |

### `GetSupportedQRTypesResponse` / `QRTypeInfo`

| Поле | Тип | Описание |
|---|---|---|
| `QrTypes` | `IList<QRTypeInfo>`? | Список типов |
| `QRTypeInfo.Id` | int | Идентификатор — передаётся в `qrTypeId` |
| `QRTypeInfo.Name` | string? | Краткое наименование |
| `QRTypeInfo.Description` | string? | Порядок и длина полей внутри Data Matrix |

### `ImportDeclarationResponse` / `ProductionDeclarationResponse`

| Поле | Тип | Описание |
|---|---|---|
| `StakeholderCode` | long | Код организации, на которую оформлена декларация |
| `DeclarationId` | long | Идентификатор созданной декларации |
| `DeclarationDate` | `DateTimeOffset` | Дата и время регистрации |
| `Gtin` | string? | GTIN партии |
| `ProductionDate` | `DateTimeOffset` | Дата производства |
| `ExpirationDate` | `DateTimeOffset` | Дата истечения срока годности |
| `BatchNo` | string? | Номер партии |

### `TransferDeclarationResponse`

| Поле | Тип | Описание |
|---|---|---|
| `DeclarationId` | long | Идентификатор перемещения |
| `DeclarationDate` | `DateTimeOffset` | Дата и время создания |

### `TransferDeclarationCancelResponse`

| Поле | Тип | Описание |
|---|---|---|
| `DeclarationId` | long | Идентификатор отменённого перемещения |
| `DeclarationDate` | `DateTimeOffset` | Дата и время отмены |

### `GetTransferDeclarationResponse`

| Поле | Тип | Описание |
|---|---|---|
| `DeclarationId` | long | Идентификатор перемещения |
| `DocumentNo` | string? | Номер сопроводительного документа |
| `DocumentDate` | `DateTimeOffset?` | Дата документа |
| `FromStakeholder` | long | Код отправителя |
| `ToStakeholder` | long | Код получателя |
| `DeclarationDate` | `DateTimeOffset` | Дата перемещения |
| `Description` | string? | Комментарий |
| `CurrentState` | `TransferState` | Состояние перемещения |
| `IsReturn` | bool | Признак возврата |
| `Details` | `IList<TransferDeclarationDetailItem>`? | Состав перемещения |

### `TransferDeclarationDetailItem`

| Поле | Тип | Описание |
|---|---|---|
| `ProductBoxId` | long | Внутренний идентификатор упаковки |
| `DrugPackageItemId` | `Guid` | Идентификатор позиции упаковки препарата |
| `FullBrandName` | string? | Полное торговое наименование |
| `QrCode` | string? | QR-код упаковки |
| `Gtin` | string? | GTIN |
| `BatchNumber` | string? | Номер партии |
| `ExpirationDate` | `DateTimeOffset?` | Срок годности |
| `SerialNumber` | string? | Серийный номер |
| `Price` | decimal | Цена упаковки в перемещении |

### `GetTransferListByFilterResponse` / `TransferDeclarationInfo`

| Поле | Тип | Описание |
|---|---|---|
| `TransferDeclarationList` | `IList<TransferDeclarationInfo>`? | Найденные перемещения (без состава) |
| `TransferDeclarationInfo.DeclarationId` | long | Идентификатор перемещения |
| `TransferDeclarationInfo.DocumentNo` | string? | Номер документа |
| `TransferDeclarationInfo.DocumentDate` | `DateTimeOffset?` | Дата документа |
| `TransferDeclarationInfo.DeclarationDate` | `DateTimeOffset` | Дата перемещения |
| `TransferDeclarationInfo.FromStakeholderCode` | long | Код отправителя |
| `TransferDeclarationInfo.FromStakeholder` | string? | Наименование отправителя |
| `TransferDeclarationInfo.ToStakeholderCode` | long | Код получателя |
| `TransferDeclarationInfo.ToStakeholder` | string? | Наименование получателя |
| `TransferDeclarationInfo.TotalCount` | long | Количество упаковок |
| `TransferDeclarationInfo.Description` | string? | Комментарий |
| `TransferDeclarationInfo.CurrentState` | `TransferState` | Состояние перемещения |
| `TransferDeclarationInfo.TransferType` | `TransferType` | Роль организации |
| `TransferDeclarationInfo.IsReturn` | bool | Признак возврата |

### `StockDeclarationResponse`

| Поле | Тип | Описание |
|---|---|---|
| `DeclarationId` | long | Идентификатор складской декларации |
| `DeclarationDate` | `DateTimeOffset` | Дата и время создания |

### `StockDeclarationCancelResponse`

| Поле | Тип | Описание |
|---|---|---|
| `DeclarationId` | long | Идентификатор декларации |
| `IsSuccess` | bool | Успешность отмены (может быть `false` при статусе 200) |
| `Message` | string? | Пояснение результата |

### `GetStockInheldListResponse` / `StockInheldSummaryInfo`

| Поле | Тип | Описание |
|---|---|---|
| `StockInheldSummaryList` | `IList<StockInheldSummaryInfo>`? | Сводка остатков |
| `StockInheldSummaryInfo.Gtin` | string? | GTIN препарата |
| `StockInheldSummaryInfo.FullBrandName` | string? | Полное торговое наименование |
| `StockInheldSummaryInfo.Amount` | decimal | Суммарное количество в наличии |

### `GetStockInheldListByGtinResponse` / `StockInheldInfo`

| Поле | Тип | Описание |
|---|---|---|
| `StockInheldList` | `IList<StockInheldInfo>`? | Перечень упаковок |
| `StockInheldInfo.Id` | long | Внутренний идентификатор упаковки |
| `StockInheldInfo.CurrentStakeholderId` | `Guid` | Идентификатор организации-держателя |
| `StockInheldInfo.DrugPackageItemId` | `Guid` | Идентификатор позиции упаковки препарата |
| `StockInheldInfo.Gtin` | string? | GTIN |
| `StockInheldInfo.FullBrandName` | string? | Полное торговое наименование |
| `StockInheldInfo.BatchNumber` | string? | Номер партии |
| `StockInheldInfo.ExpirationDate` | `DateTimeOffset` | Срок годности |
| `StockInheldInfo.SerialNumber` | string? | Серийный номер |
| `StockInheldInfo.DeclarationDate` | `DateTimeOffset?` | Дата поступления в наличие |
| `StockInheldInfo.Price` | decimal? | Цена упаковки |
| `StockInheldInfo.PartialSaleAmount` | decimal? | Реализовано при частичной продаже |
| `StockInheldInfo.CurrentState` | `ProductState` | Текущее состояние упаковки |
| `StockInheldInfo.QrCode` | string? | QR-код упаковки |

### `GetPartialSaleInfoResponse`

| Поле | Тип | Описание |
|---|---|---|
| `Gtin` | string? | GTIN препарата |
| `FullBrandName` | string? | Полное торговое наименование |
| `SerialNumber` | string? | Серийный номер упаковки |
| `PartialSaleAmount` | decimal | Уже реализованное количество |

> В опубликованной OpenAPI-схеме для этого метода ошибочно указан тип
> `GetStockInheldListByGtinResponse`. Фактический ответ имеет форму выше.

### `SalesDeclarationResponse`

| Поле | Тип | Описание |
|---|---|---|
| `StakeholderCode` | long | Код организации, оформившей реализацию |
| `DeclarationId` | long | Идентификатор декларации реализации |
| `DeclarationDate` | `DateTimeOffset` | Дата и время операции |

### `DeactivateDeclarationResponse`

| Поле | Тип | Описание |
|---|---|---|
| `DeclarationId` | long | Идентификатор декларации деактивации |
| `DeclarationDate` | `DateTimeOffset` | Дата и время деактивации |

### `GetMedicineListResponse` / `MedicineInfo`

| Поле | Тип | Описание |
|---|---|---|
| `NumberOfMedicines` | int | Общее количество препаратов |
| `MedicineList` | `IList<MedicineInfo>`? | Список препаратов |
| `MedicineInfo.Gtin` | string? | GTIN препарата |
| `MedicineInfo.BrandName` | string? | Краткое торговое наименование |
| `MedicineInfo.FullBrandName` | string? | Полное торговое наименование с дозировкой |
| `MedicineInfo.ManufacturerCompany` | string? | Компания-производитель |
| `MedicineInfo.Country` | string? | Страна происхождения |
| `MedicineInfo.AtcCode` | string? | Код АТХ |
| `MedicineInfo.Formula` | string? | Лекарственная форма / состав |
| `MedicineInfo.InnList` | string? | Действующие вещества (МНН) |
| `MedicineInfo.TrackAndTraceStatus` | `TrackAndTraceStatus` | Статус обязательности маркировки |
| `MedicineInfo.LastUpdate` | `DateTimeOffset` | Дата последнего изменения карточки |

### `ProductInquiryResult`

| Поле | Тип | Описание |
|---|---|---|
| `ProductBoxId` | long | Внутренний идентификатор упаковки |
| `ProductPackageItemId` | `Guid` | Идентификатор позиции упаковки препарата |
| `ProductName` | string? | Наименование препарата |
| `Gtin` | string? | GTIN |
| `SerialNumber` | string? | Серийный номер |
| `BatchNumber` | string? | Номер партии |
| `ProductionDate` | `DateTimeOffset` | Дата производства |
| `ExpirationDate` | `DateTimeOffset` | Дата истечения срока годности |
| `QrCode` | string? | QR-код упаковки |
| `StakeHolderName` | string? | Текущий держатель упаковки |
| `StakeholderTaxNumber` | string? | ИНН текущего держателя |
| `IsExpired` | bool | Срок годности истёк |
| `IsAvailableForSale` | bool | Упаковка доступна к продаже |
| `IsSuspendedOrRecalled` | bool | Упаковка приостановлена или отозвана |
| `ProductStatus` | `ProductStatus` | Складской статус |
| `ProductState` | `ProductState` | Последнее событие с упаковкой |
| `SuspendRecallInfo` | `SuspendRecallInfo?` | Сведения о приостановке/отзыве |
| `ProductInquiryHistory` | `IList<ProductInquiryHistory>`? | История движения упаковки |
| `OverallRetailPrice` | string? | Предельная розничная цена |
| `CertificateNumber` | string? | Номер регистрационного удостоверения |
| `InstructionForUse` | string? | Инструкция по применению |
| `InstructionForUseDocId` | `Guid?` | Идентификатор документа с инструкцией |
| `PackagingImageDocId` | `Guid?` | Идентификатор изображения упаковки |
| `IsFomsDrug` | bool? | Препарат льготного обеспечения |
| `Compensation` | string? | Размер компенсации |
| `ConsumptionType` | `ConsumptionType?` | Тип списания, по которому упаковка выбыла |
| `ManufacturerName` | string? | Производитель |
| `PartialSaleRemainingAmount` | decimal? | Остаток после частичной продажи |

> Поле `StakeholderTaxNumber` присутствует в фактическом ответе API,
> но отсутствует в опубликованной OpenAPI-схеме.

### `SuspendRecallInfo`

| Поле | Тип | Описание |
|---|---|---|
| `StartDate` | `DateTimeOffset?` | Начало периода приостановки/отзыва |
| `EndDate` | `DateTimeOffset?` | Окончание периода (`null` — бессрочно) |
| `Reason` | string? | Причина |

### `ProductInquiryHistory`

| Поле | Тип | Описание |
|---|---|---|
| `DeclarationNumber` | long | Номер декларации события |
| `StakeHolder` | string? | Организация на момент события |
| `State` | `ProductState` | Состояние, установленное событием |
| `StateDate` | `DateTimeOffset` | Дата и время события |
| `Price` | decimal | Цена на момент события |
| `PartialSaleAmount` | decimal? | Реализовано при частичной продаже |

---

## Исключения

Все неуспешные HTTP-статусы приводятся к `KnddbApiException`.

| Член | Описание |
|---|---|
| `StatusCode` | HTTP-статус ответа |
| `Method`, `RequestUri` | Метод и путь запроса |
| `ResponseBody` | «Сырое» тело ответа |
| `ProblemDetails` | Разобранный ProblemDetails (RFC 7807) |
| `TraceId` | Идентификатор запроса для обращения в поддержку |
| `Errors` | Ошибки валидации: имя поля → текст |
| `IsNotFound` | HTTP 404 |
| `IsForbidden` | HTTP 403 — не хватает роли API |
| `IsBadRequest` | HTTP 400 |
| `IsAuthenticationFailure` | HTTP 401 или 403 |
| `IsTransientFailure` | HTTP 5xx или 408 — имеет смысл повторить |

| Тип исключения | Когда возникает |
|---|---|
| `KnddbApiException` | Любой неуспешный HTTP-статус |
| `KnddbAuthenticationException` | 401, неверный логин/пароль, отсутствие учётных данных, отклонённый `refresh_token` |
| `KnddbConfigurationException` | Настройки некорректны, сессия не найдена (ошибка до обращения к сети) |

```csharp
try
{
    var medicine = await client.ProductInquiryByQrCodeAsync(qrCode);
}
catch (KnddbAuthenticationException ex)
{
    await client.SignInAsync(login, password);   // сессия недействительна — входим заново
}
catch (KnddbApiException ex) when (ex.IsNotFound)
{
    // упаковка не найдена
}
catch (KnddbApiException ex)
{
    Console.WriteLine($"HTTP {ex.StatusCode}: {ex.Message}; traceId={ex.TraceId}");
    foreach (var (field, message) in ex.Errors ?? [])
    {
        Console.WriteLine($"  {field}: {message}");
    }
}
```

---

## Права доступа

**Роли API настраиваются автоматически** — администратором департамента лекарственных средств
через административную панель KNMDB. Интеграции ничего запрашивать и проверять не нужно:
достаточно получить логин и пароль.

Если метод возвращает **403 Forbidden**, обратитесь к **администратору департамента
лекарственных средств** и уточните настройку прав для вашей учётной записи.

### Справочная таблица ролей (для обращения в департамент)

Таблица приведена только для того, чтобы при обращении назвать конкретную роль.

| Роль | Метод |
|---|---|
| `ApiGetAllStakeholders` | `GetAllStakeholdersAsync` |
| `ApiGetSupportedQRTypes` | `GetSupportedQrTypesAsync` — **устаревший**, см. [Устаревшее](#устаревшее-deprecated) |
| `ApiGetMedicineList` | `GetMedicineListAsync` |
| `ApiImportDeclaration` | `ImportDeclarationAsync` |
| `ApiProductionDeclaration` | `ProductionDeclarationAsync` |
| `ApiTransferDeclaration` | `TransferDeclarationAsync` |
| `ApiTransferAccept` | `TransferAcceptAsync` |
| `ApiTransferCancel` | `TransferCancelAsync` |
| `ApiTransferReturn` | `TransferReturnAsync` |
| `ApiTransferReturnCancel` | `TransferReturnCancelAsync` |
| `ApiGetTransferDeclaration` | `GetTransferDeclarationAsync` |
| `ApiGetTransferListByFilter` | `GetTransferListByFilterAsync` |
| `ApiStockDeclaration` | `StockDeclarationAsync` |
| `ApiStockDeclarationCancel` | `StockDeclarationCancelAsync` |
| `ApiGetStockInheldList` | `GetStockInheldListAsync` |
| `ApiGetStockInheldListByGtin` | `GetStockInheldListByGtinAsync` |
| `ApiGetPartialSaleInfo` | `GetPartialSaleInfoAsync` |
| `ApiSalesDeclaration` | `SalesDeclarationAsync` |
| `ApiSalesDeclarationCancel` | `SalesDeclarationCancelAsync` |
| `ApiSalesDeclarationBoxCancel` | `SalesDeclarationBoxCancelAsync` |
| `ApiDeactivateDeclaration` | `DeactivateDeclarationAsync` |
| — (без авторизации) | `ProductInquiryByQrCodeAsync`, `ProductInquiryByGtinSnAsync` |
