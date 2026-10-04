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
| **Формат ответа** | Все методы Track and Trace возвращают результат в конверте `{ resultCode, resultMessage, actionResult }` — см. [Конверт ответа](#конверт-ответа-и-коды-результата). SDK разбирает его автоматически. |
| **Формат дат** | Поля-даты передаются как `yyyy-MM-dd` **без времени**. Значение с временем (`2026-03-15T12:00:00Z`) сервер отклоняет с кодом результата 2. SDK отбрасывает время автоматически. |
| **Заголовок `User-Agent`** | Обязателен: сервер записывает его в базу при входе. Без заголовка вход падает с кодом результата 2. SDK отправляет его всегда — см. [Заголовок User-Agent](#заголовок-user-agent). |
| **Формат перечислений** | Числа. При чтении SDK дополнительно принимает строки — см. [ENUMS.md](ENUMS.md). |
| **Имена полей** | `camelCase` (SDK настраивает автоматически). |
| **Пустой ответ** | Метод возвращает `null`, если сервер вернул пустое тело или `actionResult: null`. |
| **Ошибка** | Выбрасывается `KnddbApiException` (см. [Исключения](#исключения)). **Ошибки бизнес-логики приходят с HTTP 200** — проверяйте `ResultCode`. |
| **`CancellationToken`** | Последний параметр каждого метода. |
| **Смена пользователя** | Каждый метод выполняется от имени текущей сессии — см. [Работа с сессиями](#работа-с-сессиями). |
| **Права доступа** | Роли API настраиваются администратором департамента лекарственных средств через административную панель KNMDB. Отдельно запрашивать и проверять их не нужно. |

> **Ошибка 403.** Если метод вернул 403, обратитесь к **администратору департамента
> лекарственных средств** — права настраиваются на стороне KNMDB, со стороны интеграции
> правки не требуются.

---

## Конверт ответа и коды результата

### Формат конверта

Все методы Track and Trace отвечают одним и тем же конвертом:

```json
{
  "resultCode": 0,
  "resultMessage": "Action completed successfully.",
  "actionResult": {
    "declarationId": 15234,
    "declarationDate": "2026-03-15T00:00:00"
  }
}
```

| Поле | Тип | Описание |
|---|---|---|
| `resultCode` | `int` | **Код результата.** `0` — успех, любое другое значение — ошибка. |
| `resultMessage` | `string` | Сообщение сервера. При ошибке — описание причины, как правило на английском. |
| `actionResult` | `object` | Полезная нагрузка метода. При ошибке — `null`. |

Конверт добавляет фильтр `ActionResultFilterAttribute` сервера KNMDB: он
заменяет результат каждого действия на `Ok(KNDDBActionResult)`. Поэтому данные
метода лежат **не в корне JSON**, а в поле `actionResult`.

### Главное: ошибки бизнес-логики приходят с HTTP 200

Обработчик исключений сервера сериализует бизнес-исключение в **тот же конверт**
и оставляет HTTP-статус **`200 OK`**:

```http
HTTP/1.1 200 OK
Content-Type: application/json

{"resultCode":6023,"resultMessage":"Product with QRCode 0104… is not suitable for sale","actionResult":null}
```

**Отличить успех от ошибки по HTTP-статусу невозможно.** Признак ошибки —
ненулевой `resultCode`. Это самая частая ловушка при самостоятельной интеграции:
код, проверяющий только `response.IsSuccessStatusCode` / `response.ok`, считает
такую ошибку успехом и получает пустой объект данных.

SDK делает эту проверку за вас: при `resultCode != 0` выбрасывается
`KnddbApiException` с заполненными `ResultCode` и `ResultMessage`.

### Коды результата

Общие коды:

| Код | Константа SDK | Значение |
|---|---|---|
| `0` | `KnddbResultCodes.Success` | Успех. |
| `1` | `KnddbResultCodes.ValidationError` | Ошибка проверки входных данных. |
| `2` | `KnddbResultCodes.UnexpectedError` | Неожиданная ошибка сервера. |
| `3` | — | Неожиданная ошибка (без подробностей). |

Коды бизнес-логики Track and Trace:

| Код | Константа SDK | Значение |
|---|---|---|
| `6003` | `SimilarQrCode` | В списке повторяющиеся QR-коды. |
| `6004` | `SimilarQrCodeInDatabase` | QR-код уже зарегистрирован в системе. |
| `6005` | `DifferentBatchNo` | В списке разные номера партий. |
| `6006` | `DifferentExpirationDate` | В списке разные даты истечения срока годности. |
| `6010` | `ProductionDateAfterDeclaration` | Дата производства позже даты декларации. |
| `6012` | `NoSuitableProducts` | Нет препаратов, подходящих для операции. |
| `6021` | `SerialNumbersAlreadyDeclared` | Серийные номера уже объявлены. |
| **`6022`** | `ProductWithQrCodeNotFound` | **Упаковка с таким QR-кодом не найдена.** |
| **`6023`** | `ProductWithQrCodeNotSuitableForSale` | **Упаковка не подходит для продажи** (чаще всего — повторная продажа уже проданной упаковки). |
| `6024` | `QrCodeNotSuitableToModel` | QR-код не соответствует выбранной модели (типу). |
| `6025` | `QrCodeGtinMismatch` | GTIN в QR-коде не совпадает с GTIN в запросе. |
| `6026` | `QrCodeExpirationDateMismatch` | Дата годности в QR-коде не совпадает с датой в запросе. |
| `6027` | `QrCodeBatchNumberMismatch` | Номер партии в QR-коде не совпадает с номером в запросе. |
| `6028` | `MedicineNotSuitableForPartialSale` | Препарат не подлежит частичной продаже. |
| `6029` | `PartialSaleAmountMustBePositive` | Количество при частичной продаже должно быть > 0. |
| `6030` | `NotEnoughAmountForPartialSale` | Недостаточно препарата для частичной продажи. |
| `6031` | `MustBePartiallySold` | Препарат должен продаваться частично. |
| `6032` | `QrCodeCouldNotBeParsed` | QR-код не удалось разобрать. |
| `6033` | `ProductAlreadyDeclared` | Упаковка уже объявлена ранее. |
| `6034` | `DeclarationDoesntBelongToYou` | Декларация принадлежит другой организации. |
| `6035` | `DeclarationCannotBeCancelled` | Декларацию нельзя отменить: позиции уже обработаны. |
| `6036` | `QrCodeModelNotFound` | Модель QR-кода не найдена (проверьте `qrTypeId`). |
| `6037` | `OrganizationIsNotTrackAndTraceStakeholder` | Организация не участник прослеживаемости. |
| `6038` | `ImporterWithTaxNumberNotFound` | Импортёр с указанным ИНН не найден. |
| `6039` | `ProductWithSerialNumberNotFound` | Пара «GTIN + серийный номер» не найдена. |
| `6040` | `WarehouseDoesntBelongToYourOrganization` | Склад не принадлежит вашей организации. |
| `6042` | `TransferDeclarationDetailsIsEmpty` | Список упаковок для перемещения пуст. |
| `6043` | `DeactivateDeclarationProductsMustBeSame` | В деактивации должны быть упаковки одного препарата. |
| `6044` | `DeactivateDeclarationDetailsIsEmpty` | Список упаковок для деактивации пуст. |
| `6045` | `ProductCannotBeDeactivated` | Упаковку нельзя деактивировать: она продана. |
| `6046` | `ProductAlreadySalesCancelled` | Продажа упаковки уже отменена. |
| `6047` | `TransferDeclarationIsNotInitiated` | Перемещение уже не в состоянии «инициировано». |
| `6048` | `QrCodeNonAsciiCharFound` | QR-код содержит недопустимые символы. |
| `6049` | `QrCodeParseError` | Ошибка разбора QR-кода. |
| `6050` | `ProductIsDeactivated` | Упаковка деактивирована. |
| `6051` | `ProductIsNotDeactivated` | Упаковка не деактивирована. |
| `6052` | `CitizenNumberTooLong` | Длина ПИН гражданина превышает 24 символа. |
| `6100` | `PrescriptionNotFound` | Рецепт не найден. |
| `6101` | `UserNationalIdentityNotFound` | У пользователя не заполнен ПИН. |
| `6102` | `PharmacyLicenseNotFound` | Нет привязки к аптеке или номера лицензии. |
| `6103` | `SalesDeclarationFailed` | Реализация по рецепту не удалась. |
| `6104` | `SalesCancelDeclarationFailed` | Отмена реализации по рецепту не удалась. |
| `6105` | `UserMustBelongPharmacy` | Пользователь должен быть привязан к аптеке. |
| `6106` | `UserMustBelongPharmacyOrHospital` | Пользователь должен быть привязан к аптеке или больнице. |

Полный список с русскими описаниями доступен в коде:
`KnddbResultCodes.Describe(resultCode)` (.NET) / `KnddbResultCodes.describe(resultCode)` (Dart).

### Как обрабатывать в коде

```csharp
try
{
    await client.SalesDeclarationAsync(request);
}
catch (KnddbApiException ex) when (ex.IsBusinessError)
{
    // Ошибка бизнес-логики: HTTP-статус 200, причина — в ResultCode.
    switch (ex.ResultCode)
    {
        case KnddbResultCodes.ProductWithQrCodeNotFound:
            // «Упаковка с таким QR-кодом не найдена в системе»
            break;

        case KnddbResultCodes.ProductWithQrCodeNotSuitableForSale:
            // «Упаковка не подходит для продажи: возможно, она уже продана…»
            break;

        case KnddbResultCodes.DeclarationDoesntBelongToYou:
            // Декларация другой организации
            break;

        default:
            // Готовое русское описание, а если код неизвестен — сообщение сервера
            logger.LogWarning("KNMDB {Code}: {Description}", ex.ResultCode, ex.GetResultDescription());
            break;
    }
}
```

Готовые признаки в исключении:

| Свойство | Что проверяет |
|---|---|
| `IsBusinessError` | Конверт с ненулевым `ResultCode` (пришёл с HTTP 200). |
| `IsProductNotFound` | HTTP 404 **или** код 6022/6039 — удобно для проверки лекарства. |
| `IsProductNotSuitableForSale` | Код 6023 — повторная продажа или непригодная упаковка. |
| `ResultCode` | Код результата; `null`, если конверта в ответе не было. |
| `ResultMessage` | Сообщение сервера (английское). |
| `GetResultDescription()` | Русское описание кода, иначе `ResultMessage`. |

> Если `ResultCode` равен `null`, ответ пришёл **не** от Track and Trace, а от
> уровня ASP.NET Core: проверка модели (400), отсутствие прав (403),
> необработанное исключение (500). В этом случае ориентируйтесь на `StatusCode`
> и `ProblemDetails`.

### Эндпоинт токена

`POST /connect/token` обёрткой не покрыт, но обработчик исключений действует и
там: при внутреннем сбое сервер отвечает **HTTP 200** и конвертом
`{"resultCode": 2, "resultMessage": "…", "actionResult": null}`.

SDK распознаёт этот случай и сообщает настоящую причину вместо невнятного
«сервер не вернул поле `access_token`»:

```text
KnddbAuthenticationException: Сервер KNMDB не выполнил вход (resultCode 2).
Внутренняя ошибка сервера KNMDB. Обратитесь в поддержку.
Ответ сервера: Unexpected error(s) occurred: An error occurred while saving the entity changes.
Вероятная причина — несовместимость запроса с сервером: проверьте заголовок User-Agent и формат дат.
```

---

## Заголовок User-Agent

Сервер KNMDB **записывает значение заголовка `User-Agent` в базу данных** при
входе (`AuthorizationController`, поле `UserAgent`). Если заголовок не передан,
запись завершается ошибкой:

```json
{"resultCode":2,"resultMessage":"Unexpected error(s) occurred: An error occurred while saving the entity changes","actionResult":null}
```

Симптом обманчив: выглядит как сбой сервера, а на самом деле интеграция не
отправила заголовок. Диагностика простая — если «без `User-Agent` не работает,
а с любым значением работает», причина именно в этом.

SDK отправляет заголовок **всегда**, даже если приложение его не задало, —
подставляется `Knddb.TrackAndTrace.SDK/1.0`. Своё значение задавайте в настройках:

```csharp
var options = new KnddbClientOptions
{
    UserAgent = "MyPharmacyApp/2.1 (+https://example.kg; support@example.kg)",
};
```

```dart
final options = KnddbClientOptions(
  userAgent: 'MyPharmacyApp/2.1 (+https://example.kg; support@example.kg)',
);
```

Указывайте название и версию приложения: это помогает администратору KNMDB
отличить интеграции в журналах и при разборе инцидентов.

---

## Аутентификация

### Области доступа (scope): только `api`

> **Не добавляйте `offline_access` — вход перестанет работать.**
>
> Сервер KNMDB регистрирует единственную область: `options.RegisterScopes("api")`
> в конфигурации OpenIddict. Область `offline_access` в OpenIddict разрешена
> **только** при включённом потоке обновления токена (`AllowRefreshTokenFlow()`).
> На сервере KNMDB он не включён, поэтому запрос с этой областью отклоняется
> **целиком**, ещё до проверки логина и пароля:
>
> ```json
> {"error":"invalid_request","error_description":"The 'offline_access' scope is not allowed.","error_uri":"https://documentation.openiddict.com/errors/ID2035"}
> ```
>
> Симптом обманчив: ошибка выглядит как проблема с учётной записью, хотя дело
> в лишней области. Проверено на тестовом контуре.
>
> SDK запрашивает `api`. Если ваша конфигурация сервера отличается, задайте
> область явно: `KnddbClientOptions.Scope` (.NET) / `KnddbClientOptions(scope:)` (Dart).
> Пустое значение означает «не передавать `scope` вовсе».

### `POST /connect/token` — получение токена

Тело запроса: `application/x-www-form-urlencoded`.

| Поле | Тип | Обязательно | Описание |
|---|---|---|---|
| `grant_type` | string | да | **Только `password`** — вход по логину и паролю. Сервер не поддерживает `refresh_token` |
| `username` | string | при `password` | Логин пользователя KNMDB |
| `password` | string | при `password` | Пароль пользователя KNMDB |
| `scope` | string | нет | Область доступа. SDK запрашивает `api` — это единственная зарегистрированная область |

Ответ:

| Поле | Тип | Описание |
|---|---|---|
| `access_token` | string | JWT для заголовка `Authorization: Bearer ...` |
| `token_type` | string | Тип токена, всегда `Bearer` |
| `expires_in` | int | Время жизни токена доступа **в секундах**. Сервер выдаёт 2 часа (7200) |
| `refresh_token` | string | Токен обновления, если сервер его выдал |
| `scope` | string | Выданные области доступа |
| `id_token` | string | Токен идентификации OpenID Connect (SDK не использует) |

Проверка через curl:

```bash
curl -H "content-type: application/x-www-form-urlencoded" \
     -H "user-agent: MyApp/1.0" \
     -d "grant_type=password&username=<логин>&password=<пароль>&scope=api" \
     https://testndbapi.med.kg/connect/token
```

> Заголовок `user-agent` в примере не случаен: без него сервер не может
> сохранить запись о входе — см. [Заголовок User-Agent](#заголовок-user-agent).

### `POST /connect/logout` — выход

SDK очищает токены локально в любом случае, даже если сервер вернул ошибку.

### Автоматическое продление токена

Сервер KNMDB **не поддерживает поток `refresh_token`** (`grant_type=refresh_token`
отвечает `unsupported_grant_type`, поток `AllowRefreshTokenFlow()` в конфигурации
не включён). Токен доступа живёт 2 часа.

Порядок действий SDK:

1. Токен обновляется за `KnddbClientOptions.TokenExpirationMargin` (по умолчанию 30 секунд) до истечения.
2. Если сервер выдал `refresh_token` (например, на конфигурации с включённым
   потоком обновления) — используется он.
3. Если токена обновления нет, SDK **повторно выполняет вход по сохранённым
   учётным данным** — приложение этого не замечает.
4. Если учётные данные не сохранены (`storeCredentials: false`) и токен истёк —
   выбрасывается `KnddbAuthenticationException`, требуется новый вход.
5. При ответе **401** запрос повторяется один раз с принудительно обновлённым токеном.

> **Практический вывод:** для долго работающего приложения сохраняйте учётные
> данные при входе (значение по умолчанию `storeCredentials: true`). Иначе
> каждые 2 часа потребуется повторный ввод пароля оператором.

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

> **Метод не поддерживает постраничную выдачу и возвращает весь справочник.**
> Проверено на тестовом контуре: **11 468 организаций** в одном ответе (несколько
> мегабайт). Метод возвращает все организации республики, а не только ваши.
>
> Что делать при интеграции:
>
> - вызывайте метод **один раз** и кэшируйте результат; для справочника организаций
>   достаточно обновлять его раз в сутки;
> - не вызывайте его перед каждой операцией — используйте кэш по `Code`;
> - на мобильных устройствах учитывайте объём ответа: сохраняйте справочник
>   локально и обновляйте по расписанию.

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
>
> **Всегда задавайте фильтры.** Постраничной выдачи нет, а объёмы большие —
> проверено на тестовом контуре:
>
> | Метод без фильтров | Объём ответа |
> |---|---|
> | `GetTransferListByFilterAsync()` | **15 702** перемещения |
> | `GetMedicineListAsync()` | **3 919** препаратов |
> | `GetAllStakeholdersAsync()` | **11 468** организаций |
>
> Ограничивайте выборку периодом `DeclarationDateFrom` / `DeclarationDateTo`,
> состоянием `CurrentState` и организацией — иначе ответ будет в несколько
> мегабайт, а разбор займёт секунды.

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

Все неуспешные ответы приводятся к `KnddbApiException` — и по HTTP-статусу,
и по коду результата в конверте (см. [Конверт ответа](#конверт-ответа-и-коды-результата)).

| Член | Описание |
|---|---|
| `ResultCode` | **Код результата из конверта.** `null`, если конверта в ответе не было |
| `ResultMessage` | Сообщение сервера из конверта (при ошибке бизнес-логики — на английском) |
| `IsBusinessError` | Конверт с ненулевым `ResultCode` — ошибка бизнес-логики с HTTP 200 |
| `IsProductNotFound` | HTTP 404 **или** код 6022/6039 — упаковка не найдена |
| `IsProductNotSuitableForSale` | Код 6023 — упаковка не подходит для продажи |
| `GetResultDescription()` | Русское описание кода; если код неизвестен — `ResultMessage` |
| `StatusCode` | HTTP-статус ответа. Для ошибок бизнес-логики — **200** |
| `Method`, `RequestUri` | Метод и путь запроса |
| `ResponseBody` | «Сырое» тело ответа |
| `ProblemDetails` | Разобранный ProblemDetails (RFC 7807) |
| `TraceId` | Идентификатор запроса для обращения в поддержку |
| `Errors` | Ошибки валидации: имя поля → текст |
| `IsNotFound` | HTTP 404 |
| `IsForbidden` | HTTP 403 — не хватает роли API |
| `IsBadRequest` | HTTP 400 |
| `IsAuthenticationFailure` | HTTP 401 или 403 |
| `IsConflict` | HTTP 409 |
| `IsTransientFailure` | HTTP 5xx или 408 — имеет смысл повторить |

| Тип исключения | Когда возникает |
|---|---|
| `KnddbApiException` | Неуспешный HTTP-статус **или** ненулевой `resultCode` в конверте |
| `KnddbAuthenticationException` | 401, неверный логин/пароль, отсутствие учётных данных, отклонённый `refresh_token`, ошибка в конверте на `/connect/token` |
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
catch (KnddbApiException ex) when (ex.IsProductNotFound)
{
    // Упаковка не найдена: и HTTP 404, и код 6022 в конверте с HTTP 200
}
catch (KnddbApiException ex) when (ex.IsBusinessError)
{
    // Прочие ошибки бизнес-логики: показываем русское описание
    Console.WriteLine($"{ex.ResultCode}: {ex.GetResultDescription()}");
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
