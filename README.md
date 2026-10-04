# Knmdb.TrackAndTrace

SDK (DI-сервис) для **API Track and Trace** системы **KNMDB / KNDDB**
(Кыргызская национальная база лекарственных средств).

Кроссплатформенная библиотека для **.NET 10**: подключается в **мобильное** (MAUI),
**desktop** (WPF, WinForms, Avalonia), **серверное** (ASP.NET Core) приложение,
а также в консольные утилиты и фоновые службы.

---

## Документация

| Документ | Для чего |
|---|---|
| **README.md** (этот файл) | Установка, контуры, быстрый старт, смена пользователя |
| [docs/SCENARIOS.md](docs/SCENARIOS.md) | **Начните отсюда для интеграции**: пошаговые сценарии «от импортёра до аптеки» с кодом |
| [docs/API-REFERENCE.md](docs/API-REFERENCE.md) | Все 23 метода, каждое поле запросов и ответов, роли API |
| [docs/ENUMS.md](docs/ENUMS.md) | Все перечисления: числа, названия, пояснения |

---

## Возможности

| Возможность | Описание |
|---|---|
| **Все методы API** | 23 метода Track and Trace + вход/выход OAuth 2.0 |
| **Смена пользователя «на ходу»** | `SignInAsync`, `UseSession`, `BeginUserScope` |
| **Многопользовательская работа** | Один клиент обслуживает несколько учётных записей KNMDB |
| **Автопродление токена** | По `refresh_token`, с автоматическим повторным входом |
| **Полная документация** | Русские XML-комментарии для каждого метода, поля и значения enum |
| **AOT / trimming** | Сериализация JSON генерируется на этапе сборки |
| **Минимум зависимостей** | Только `Microsoft.Extensions.*` |

---

## Установка

```bash
dotnet add package Knmdb.TrackAndTrace
```

**Один пакет — всё, что нужно.** Он содержит и клиент API, и подключение к
`IServiceCollection` с `IHttpClientFactory`. Отдельный пакет для DI не требуется:
типов, специфичных только для ASP.NET Core, в SDK нет, поэтому он одинаково работает
в серверных, desktop-, мобильных и консольных приложениях.

Требуется **.NET 10** (`net10.0`).

---

## Контуры: тестовый и боевой

| Контур | Базовый адрес | Когда использовать |
|---|---|---|
| `KnddbEnvironment.Test` | `https://testndbapi.med.kg/` | Разработка, отладка, тесты. **Значение по умолчанию.** |
| `KnddbEnvironment.Production` | `https://ndbapi.med.kg/` | Промышленная эксплуатация |

```csharp
var options = new KnddbClientOptions
{
    Environment = KnddbEnvironment.Test,        // тестовый контур
    // Environment = KnddbEnvironment.Production, // боевой контур
};

// Явные помощники:
var options2 = new KnddbClientOptions().ForEnvironment(KnddbEnvironment.Production);
var options3 = new KnddbClientOptions().ForTest();
var options4 = new KnddbClientOptions().ForProduction();

// Проверки в коде:
Console.WriteLine(options2.EffectiveBaseAddress);   // https://ndbapi.med.kg/
Console.WriteLine(options2.ResolvedEnvironment);    // Production
Console.WriteLine(options.IsProduction);            // false
```

> **Важно.** На боевом контуре все операции реальны и необратимы: созданные декларации
> влияют на фактический оборот лекарственных средств. По умолчанию SDK использует
> **тестовый** контур — так случайная ошибка в конфигурации не затронет промышленные данные.

Адрес можно задать вручную, если используется прокси или локальный стенд:

```csharp
var options = new KnddbClientOptions { BaseAddress = new Uri("http://localhost:5001/") };
```

---

## Быстрый старт

### Консольное, desktop- или мобильное приложение

```csharp
using Knmdb.TrackAndTrace;

using var client = KnddbApiClient.Create(KnddbEnvironment.Test);

// Вход
await client.SignInAsync("ваш_логин", "ваш_пароль");

// Список организаций
var stakeholders = await client.GetAllStakeholdersAsync();
foreach (var s in stakeholders?.Stakeholders ?? [])
{
    Console.WriteLine($"{s.Code}: {s.Name} ({s.Type})");
}
```

Если у приложения уже есть свой `HttpClient`:

```csharp
var options = new KnddbClientOptions { Environment = KnddbEnvironment.Production };
using var http = options.CreateHttpClient();
using var client = new KnddbApiClient(http, options);
```

### Проверка лекарства без авторизации

```csharp
using var client = KnddbApiClient.Create(KnddbEnvironment.Test);

var medicine = await client.ProductInquiryByQrCodeAsync("010460000000001721SN...");
Console.WriteLine($"{medicine?.ProductName}, срок годности: {medicine?.ExpirationDate:d}");
Console.WriteLine($"Просрочено: {medicine?.IsExpired}, доступно к продаже: {medicine?.IsAvailableForSale}");
```

### Дальнейшие шаги

- Импорт партии → [SCENARIOS.md, шаг 1](docs/SCENARIOS.md#шаг-1-импортёр-ввод-партии-в-оборот)
- Передача на склад и в аптеку → [SCENARIOS.md, шаги 2–4](docs/SCENARIOS.md#шаг-2-склад-импортёра-постановка-на-учёт-и-проверка-остатков)
- Продажа в аптеке → [SCENARIOS.md, шаг 5](docs/SCENARIOS.md#шаг-5-аптека-продажа-покупателю)

---

## Подключение через Dependency Injection

### ASP.NET Core

```csharp
builder.Services.AddKnddbTrackAndTrace(options =>
{
    options.Environment = KnddbEnvironment.Production;
    options.UserAgent = "MyPharmacyApp/1.0";
});
```

Настройки из `appsettings.json`:

```json
{
  "Knddb": {
    "Environment": "Production",
    "Timeout": "00:02:00",
    "UserAgent": "MyPharmacyApp/1.0"
  }
}
```

```csharp
builder.Services.AddKnddbTrackAndTrace(builder.Configuration);
```

Использование в контроллере:

```csharp
[ApiController]
[Route("api/stock")]
public sealed class StockController(KnddbApiClient knddb) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
        => Ok(await knddb.GetStockInheldListAsync(ct));
}
```

### Мобильное приложение (MAUI)

```csharp
builder.Services.AddKnddbTrackAndTrace(options =>
{
    options.Environment = KnddbEnvironment.Production;
});
```

В приложении один активный пользователь — достаточно вызвать `SignInAsync` при входе.
Для нескольких рабочих мест используйте `UseSession` / `BeginUserScope`.

### Серверное приложение с несколькими учётными записями KNMDB

Один экземпляр SDK может работать от имени разных организаций. Выполните вход для каждой
учётной записи на старте приложения, а в обработке запроса выбирайте нужную:

```csharp
// Program.cs — вход для каждой учётной записи
var knddb = app.Services.GetRequiredService<KnddbApiClient>();
await knddb.SignInAsync(branchALogin, branchAPassword, sessionKey: "branch:a");
await knddb.SignInAsync(branchBLogin, branchBPassword, sessionKey: "branch:b");

// Контроллер
[HttpGet("stock")]
public async Task<IActionResult> Get(KnddbApiClient knddb, KnddbRequestUser user)
{
    user.UseFromHeader(HttpContext);   // ключ сессии из заголовка X-Knddb-Session
    return Ok(await knddb.GetStockInheldListAsync());
}
```

---

## Смена пользователя «на ходу»

```csharp
using var client = KnddbApiClient.Create(KnddbEnvironment.Test);

// Два пользователя в одном клиенте
await client.SignInAsync("pharmacist", "secret", sessionKey: "user:42");
await client.SignInAsync("warehouse", "secret", sessionKey: "user:77");

// Переключиться без обращения к сети
client.UseSession("user:42");
var stock1 = await client.GetStockInheldListAsync();

// Асинхронное переключение (при необходимости выполнит вход)
await client.UseSessionAsync("user:77");
var stock2 = await client.GetStockInheldListAsync();

// Временно выполнить операцию от имени другого пользователя
await using (client.BeginUserScope("user:42"))
{
    var data = await client.GetStockInheldListAsync();
}   // здесь снова действует user:77

// Проверки
Console.WriteLine(client.CurrentSessionKey);   // user:77
Console.WriteLine(client.CurrentUserName);     // warehouse
Console.WriteLine(client.IsAuthenticated);     // True

// Выход одного пользователя — остальные сессии сохраняются
await client.SignOutAsync("user:42");
```

Каждая сессия хранит собственные токены, поэтому переключение пользователя не требует
повторного запроса токена и не влияет на других пользователей.

---

## Аутентификация

SDK работает с конечными точками OAuth 2.0 сервера KNMDB.

```csharp
await client.SignInAsync("логин", "пароль");                            // grant_type=password
await client.SignInAsync("логин", "пароль", storeCredentials: false);   // не хранить пароль в памяти
```

Проверка доступа через curl:

```bash
curl -H "content-type: application/x-www-form-urlencoded" \
     -d "grant_type=password&username=<логин>&password=<пароль>" \
     https://testndbapi.med.kg/connect/token
```

**Автоматическое продление:**

1. Токен обновляется за `TokenExpirationMargin` (по умолчанию 30 секунд) до истечения.
2. Если `refresh_token` отклонён — SDK автоматически повторяет вход по сохранённым учётным данным.
3. При ответе **401** запрос повторяется один раз с принудительно обновлённым токеном.

Описание полей запроса и ответа токена: [API-REFERENCE.md, Аутентификация](docs/API-REFERENCE.md#аутентификация).

---

## Методы API — краткий обзор

Все методы — на `KnddbApiClient`. Полное описание с каждым полем —
в [API-REFERENCE.md](docs/API-REFERENCE.md).

### Справочники

| Метод | HTTP |
|---|---|
| `GetAllStakeholdersAsync()` | `GET .../GetAllStakeholders` |
| `GetSupportedQrTypesAsync()` — **устаревший** | `GET .../GetSupportedQRTypes` |
| `GetMedicineListAsync(request?)` | `POST .../GetMedicineList` |

### Проверка лекарства (без авторизации)

| Метод | HTTP |
|---|---|
| `ProductInquiryByQrCodeAsync(qrCode)` | `POST .../ProductInquiryQRCode` |
| `ProductInquiryByGtinSnAsync(gtin, serialNumber)` | `POST .../ProductInquiryGtinSn` |

### Ввод в оборот

| Метод | HTTP |
|---|---|
| `ImportDeclarationAsync(request)` | `POST .../ImportDeclaration` |
| `ProductionDeclarationAsync(request)` | `POST .../ProductionDeclaration` |

### Перемещение между организациями

| Метод | HTTP |
|---|---|
| `TransferDeclarationAsync(request)` | `POST .../TransferDeclaration` |
| `TransferAcceptAsync(declarationId)` | `POST .../TransferAccept` |
| `TransferCancelAsync(declarationId)` | `POST .../TransferCancel` |
| `TransferReturnAsync(request)` | `POST .../TransferReturn` |
| `TransferReturnCancelAsync(declarationId)` | `POST .../TransferReturnCancel` |
| `GetTransferDeclarationAsync(declarationId)` | `GET .../GetTransferDeclaration` |
| `GetTransferListByFilterAsync(request?)` | `POST .../GetTransferListByFilter` |

### Складской учёт

| Метод | HTTP |
|---|---|
| `StockDeclarationAsync(request)` | `POST .../StockDeclaration` |
| `StockDeclarationCancelAsync(declarationId)` | `POST .../StockDeclarationCancel` |
| `GetStockInheldListAsync()` | `POST .../GetStockInheldList` |
| `GetStockInheldListByGtinAsync(gtin)` | `POST .../GetStockInheldListByGtin` |
| `GetPartialSaleInfoAsync(qrCode)` | `POST .../GetPartialSaleInfo` |

### Реализация

| Метод | HTTP |
|---|---|
| `SalesDeclarationAsync(request)` | `POST .../SalesDeclaration` |
| `SalesDeclarationCancelAsync(declarationId)` | `POST .../SalesDeclarationCancel` |
| `SalesDeclarationBoxCancelAsync(qrCode)` | `POST .../SalesDeclarationBoxCancel` |

### Выбытие

| Метод | HTTP |
|---|---|
| `DeactivateDeclarationAsync(request)` | `POST .../DeactivateDeclaration` |

### Устаревшее (deprecated)

| Метод | Причина | Что делать вместо него |
|---|---|---|
| `GetSupportedQrTypesAsync()` | Метод `GET /api/TrackAndTrace/GetSupportedQRTypes` исключён из API департамента лекарственных средств | Значение `qrTypeId` согласовать с департаментом и задать константой в конфигурации |

Метод и связанные с ним типы `GetSupportedQRTypesResponse`, `QRTypeInfo` помечены
`[Obsolete]` с диагностическим кодом **KNMDB0001** и будут удалены из SDK, когда окончательно
исчезнут из API. Потребители получат предупреждение компилятора (не ошибку).

Подробнее: [API-REFERENCE.md, Устаревшее](docs/API-REFERENCE.md#устаревшее-deprecated).

---

## Права доступа

**Роли API настраиваются автоматически** — администратором департамента лекарственных средств
через административную панель KNMDB. Интеграции достаточно получить логин и пароль:
запрашивать или проверять роли не нужно.

Если метод вернул **403 Forbidden**, обратитесь к **администратору департамента
лекарственных средств** и уточните настройку прав для вашей учётной записи. Со стороны
интеграции правки не требуются.

---

## Обработка ошибок

```csharp
try
{
    var medicine = await client.ProductInquiryByQrCodeAsync(qrCode);
}
catch (KnddbAuthenticationException ex)      // 401 или ошибка входа
{
    await client.SignInAsync(login, password);
}
catch (KnddbApiException ex) when (ex.IsNotFound)
{
    Console.WriteLine("Упаковка не найдена");
}
catch (KnddbApiException ex) when (ex.IsForbidden)
{
    Console.WriteLine("Нет роли для этого метода — обратитесь к администратору KNMDB");
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

| Тип исключения | Когда возникает |
|---|---|
| `KnddbApiException` | Любой неуспешный HTTP-статус |
| `KnddbAuthenticationException` | 401, неверный логин/пароль, отсутствие учётных данных |
| `KnddbConfigurationException` | Настройки некорректны, сессия не найдена |

Полезные свойства: `IsNotFound` (404), `IsForbidden` (403), `IsBadRequest` (400),
`IsTransientFailure` (5xx/408), `TraceId`, `Errors`, `ProblemDetails`.

Полный список: [API-REFERENCE.md, Исключения](docs/API-REFERENCE.md#исключения).

---

## Кроссплатформенность

- Целевая платформа — `net10.0` (единый TFM для сервера, desktop и мобильных).
- `System.Text.Json` с генерацией кода на этапе сборки — совместимо с AOT и trimming
  (важно для iOS/MAUI).
- Нет `#if WINDOWS` и привязок к реестру, файловой системе или особенностям платформы.
- Зависимости — только `Microsoft.Extensions.*`.
- Транспорт — `HttpClient`/`SocketsHttpHandler`; можно подставить собственный
  `HttpMessageHandler` (сертификаты, прокси, тестовые заглушки).

---

## Сборка и упаковка

```bash
dotnet build Knmdb.TrackAndTrace.slnx -c Release
dotnet test tests/Knmdb.TrackAndTrace.Tests
dotnet pack src/Knmdb.TrackAndTrace -c Release -o ./artifacts
```

| Проект | Назначение |
|---|---|
| `src/Knmdb.TrackAndTrace` | SDK целиком: клиент, модели, сериализация и подключение к DI |
| `tests/Knmdb.TrackAndTrace.Tests` | Модульные тесты |

Исходная OpenAPI-схема API вложена в пакет как ресурс
`Knmdb.TrackAndTrace.OpenApi.knddb-track-and-trace-v1.json`.

## Лицензия

MIT.
