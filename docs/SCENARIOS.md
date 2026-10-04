# Сценарии интеграции: от импортёра до аптеки

Практическое руководство для разработчиков: **что делает каждая организация** в системе
Track and Trace и **какой код** для этого нужен. Все примеры — рабочие, сквозные и построены
в том порядке, в котором происходит реальное движение упаковки лекарства.

- [Как читать этот документ](#как-читать-этот-документ)
- [Роли и жизненный цикл упаковки](#роли-и-жизненный-цикл-упаковки)
- [Шаг 0. Подготовка интеграции](#шаг-0-подготовка-интеграции)
- [Шаг 1. Импортёр: ввод партии в оборот](#шаг-1-импортёр-ввод-партии-в-оборот)
- [Шаг 2. Склад импортёра: постановка на учёт и проверка остатков](#шаг-2-склад-импортёра-постановка-на-учёт-и-проверка-остатков)
- [Шаг 3. Склад → склад (оптовая передача)](#шаг-3-склад--склад-оптовая-передача)
- [Шаг 4. Аптека: приёмка](#шаг-4-аптека-приёмка)
- [Шаг 5. Аптека: продажа покупателю](#шаг-5-аптека-продажа-покупателю)
- [Шаг 6. Аптека: частичная продажа](#шаг-6-аптека-частичная-продажа)
- [Шаг 7. Больница: расход по требованию](#шаг-7-больница-расход-по-требованию)
- [Шаг 8. Выбытие: деактивация и списание](#шаг-8-выбытие-деактивация-и-списание)
- [Возвраты](#возвраты)
- [Отмены и ошибки оператора](#отмены-и-ошибки-оператора)
- [Инвентаризация и сверка остатков](#инвентаризация-и-сверка-остатков)
- [Мобильное приложение покупателя](#мобильное-приложение-покупателя)
- [Синхронизация справочника лекарств](#синхронизация-справочника-лекарств)
- [Чек-лист внедрения](#чек-лист-внедрения)
- [Типичные ошибки при интеграции](#типичные-ошибки-при-интеграции)

---

## Как читать этот документ

- Каждый шаг содержит: **кто** выполняет, **что** происходит в реальности, **какой метод API**
  вызывается, **код** и **что проверить** в ответе.
- Состояние упаковки (`ProductState`) меняется последовательно — это удобная «точка контроля»
  при отладке: если состояние не то, значит пропущен или не отработал предыдущий шаг.
- Полные описания полей и методов: [API-REFERENCE.md](API-REFERENCE.md),
  значения перечислений: [ENUMS.md](ENUMS.md).

---

## Роли и жизненный цикл упаковки

### Движение упаковки

```text
  ┌──────────────┐   1. Импорт / производство
  │  ИМПОРТЁР    │   ImportDeclaration / ProductionDeclaration
  │  (склад)     │   → упаковка: Import (2) или Production (1)
  └──────┬───────┘
         │  2. Складской учёт
         │     StockDeclaration → Stock (13)
         ▼
  ┌──────────────┐   3. Перемещение
  │  ЦЕНТР. СКЛАД│   TransferDeclaration → TransferInitiated (9)
  │  ДИСТРИБЬЮТОР│   TransferAccept      → TransferAccepted (10)
  └──────┬───────┘
         │  4. Перемещение в аптеку
         ▼
  ┌──────────────┐   5. Продажа
  │   АПТЕКА     │   SalesDeclaration → Sales (3)
  │              │   частичная: PartialSales (12)
  └──────┬───────┘
         │  6. Итог жизненного цикла
         ▼
  ┌──────────────┐
  │  ПОТРЕБИТЕЛЬ │   проверка через ProductInquiry (без авторизации)
  └──────────────┘

  В любой момент до продажи: DeactivateDeclaration → Deactivation (4)
```

### Кто какие методы использует

| Организация | Тип в API | Основные методы |
|---|---|---|
| Импортёр | `Importer` (2) | `ImportDeclaration`, `StockDeclaration`, `TransferDeclaration` |
| Производитель | `Producer` (1), `Manufacturer` (4) | `ProductionDeclaration`, `StockDeclaration`, `TransferDeclaration` |
| Склад / дистрибьютор | `Warehouse` (7), `Company` (3) | `TransferDeclaration`, `TransferAccept`, `GetStockInheldList`, `StockDeclaration` |
| Аптека | `Pharmacy` (5) | `TransferAccept`, `SalesDeclaration`, `GetPartialSaleInfo` |
| Больница | `MedicalOrganization` (6) | `TransferAccept`, `SalesDeclaration`, `DeactivateDeclaration` |
| Потребитель | — | `ProductInquiryQRCode` (без авторизации) |

### Смена состояний упаковки

| После операции | `ProductState` | Число |
|---|---|---|
| `ProductionDeclaration` | `Production` | 1 |
| `ImportDeclaration` | `Import` | 2 |
| `StockDeclaration` | `Stock` | 13 |
| `TransferDeclaration` | `TransferInitiated` | 9 |
| `TransferAccept` | `TransferAccepted` | 10 |
| `TransferCancel` | `TransferCancelled` | 11 |
| `TransferReturn` + `TransferAccept` | `ReturnTransferAccepted` | 15 |
| `SalesDeclaration` | `Sales` | 3 |
| `SalesDeclaration` (частичная) | `PartialSales` | 12 |
| `DeactivateDeclaration` | `Deactivation` | 4 |

---

## Шаг 0. Подготовка интеграции

### Что нужно от KNMDB до начала работ

Для начала интеграции достаточно **логина и пароля** учётной записи KNMDB.

| Что | Где взять | Зачем |
|---|---|---|
| Логин и пароль | Администратор KNMDB | `SignInAsync` |
| Код организации (`stakeholderCode`) | `GetAllStakeholdersAsync` | Указывается в декларациях |
| Значение `qrTypeId` | Конфигурация интеграции | Указывается в декларациях импорта/производства |

> **Роли API настраиваются автоматически.** Их не нужно проверять и запрашивать отдельно:
> необходимые роли уже назначены учётной записи через административную панель KNMDB.
>
> Если какой-либо метод возвращает **403**, это повод связаться с
> **администратором департамента лекарственных средств** и уточнить настройку прав
> для вашей учётной записи. Со стороны интеграции делать ничего не нужно.
>
> **`qrTypeId`.** Устаревший метод `GetSupportedQrTypesAsync` исключён из API департамента
> лекарственных средств и помечен в SDK как `[Obsolete]` — он будет удалён, когда окончательно
> исчезнет из API. Значение `qrTypeId` согласуйте с департаментом лекарственных средств
> и задайте его в конфигурации приложения как обычную константу.

> **Несколько организаций.** Одна учётная запись KNMDB привязана к одной организации.
> Если интеграция обслуживает несколько филиалов, заведите отдельную учётную запись на каждый
> и используйте **ключи сессий** (см. ниже) — так операции всегда будут выполняться
> от имени правильной организации.

### Код: подключение и первый запрос

```csharp
using Knmdb.TrackAndTrace;

// Тестовый контур для отладки: https://testndbapi.med.kg/
// Боевой контур: https://ndbapi.med.kg/
using var knddb = KnddbApiClient.Create(KnddbEnvironment.Test);

// Вход по логину и паролю
await knddb.SignInAsync("ваш_логин", "ваш_пароль");

// Код своей организации (один раз, затем кэшируйте у себя)
var stakeholders = await knddb.GetAllStakeholdersAsync();
var myOrg = stakeholders!.Stakeholders!.Single(s => s.Code == 100);   // 100 — код из KNMDB
Console.WriteLine($"Организация: {myOrg.Name}, тип: {myOrg.Type}");
```

### Код: несколько организаций в одной интеграции

Это типовой случай для холдинга: импортёр, центральный склад и несколько аптек.

```csharp
using var knddb = KnddbApiClient.Create(KnddbEnvironment.Production);

// Каждая организация получает собственный ключ сессии.
await knddb.SignInAsync("логин_импортёра", "пароль", sessionKey: "importer");
await knddb.SignInAsync("логин_склада",    "пароль", sessionKey: "warehouse:main");
await knddb.SignInAsync("логин_аптеки_1",  "пароль", sessionKey: "pharmacy:1");
await knddb.SignInAsync("логин_аптеки_2",  "пароль", sessionKey: "pharmacy:2");

// Дальше перед каждой операцией выбираем, от чьего имени работаем.
knddb.UseSession("importer");
var import = await knddb.ImportDeclarationAsync(/* ... */);

knddb.UseSession("warehouse:main");
var stock = await knddb.GetStockInheldListAsync();

// Временно — от имени аптеки, не сбивая остальные сессии:
await using (knddb.BeginUserScope("pharmacy:1"))
{
    await knddb.SalesDeclarationAsync(/* ... */);
}
```

---

## Шаг 1. Импортёр: ввод партии в оборот

### Что происходит

Импортёр получил партию от зарубежного производителя. На упаковках уже нанесены
Data Matrix коды по согласованному типу (`qrTypeId`). Задача — сообщить KNMDB, что эта партия
ввезена, и «привязать» к ней список QR-кодов. После этой операции упаковки становятся
доступны для складского учёта и передачи.

**Обязательные поля:** `Gtin`, `BatchNo`, `ExpirationDate`, `ProductionDate`, `DocumentDate`,
`DocumentNo`, `Price`, `QrTypeId`, `QrCodes`.
**Результат:** упаковки получают состояние `Import` (2).

### Код

```csharp
using Knmdb.TrackAndTrace;
using Knmdb.TrackAndTrace.Models.Requests;

using var knddb = KnddbApiClient.Create(KnddbEnvironment.Test);
await knddb.SignInAsync("ваш_логин", "ваш_пароль");

// QR-коды, считанные со сканера или полученные из вашей системы маркировки.
List<string> qrCodes = 
[
    "010460000000001721SN00000000000000011727010110B-2025-01",
    "010460000000001721SN00000000000000021727010110B-2025-01",
];

var request = new ImportDeclarationRequest
{
    // GTIN должен совпадать с тем, для которого выпущены QR-коды
    Gtin = "04600000000017",

    BatchNo = "B-2025-01",
    ExpirationDate = new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero),   // yyyy-MM-dd
    ProductionDate = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),   // yyyy-MM-dd
    DocumentDate   = new DateTimeOffset(2025, 2, 1, 0, 0, 0, TimeSpan.Zero),   // дата ГТД

    DocumentNo = "ГТД-12345/010225/0001234",

    Price = 120.50m,          // цена за упаковку
    QrTypeId = 1,             // из GetSupportedQrTypesAsync
    QrCodes = qrCodes,

    Description = "Первая поставка 2025 года",

    // Необязательно: склад, на который сразу приходуется партия.
    // Если не указать — сервер использует организацию учётной записи.
    StakeholderCode = 100,
};

try
{
    var result = await knddb.ImportDeclarationAsync(request);

    Console.WriteLine($"Декларация создана: {result!.DeclarationId}");
    Console.WriteLine($"Дата регистрации:    {result.DeclarationDate:yyyy-MM-dd HH:mm}");
    Console.WriteLine($"Организация:         {result.StakeholderCode}");
    Console.WriteLine($"Партия:              {result.BatchNo}, GTIN {result.Gtin}");
    Console.WriteLine($"Годен до:            {result.ExpirationDate:yyyy-MM-dd}");

    // Сохраните declarationId у себя: по нему потом можно сопоставить операцию.
}
catch (KnddbApiException ex) when (ex.IsBadRequest)
{
    Console.WriteLine($"Данные отклонены: {ex.Message}");
    // ex.Errors — построчные ошибки по полям
    foreach (var (field, message) in ex.Errors ?? [])
    {
        Console.WriteLine($"  {field}: {message}");
    }
}
catch (KnddbApiException ex) when (ex.IsForbidden)
{
    Console.WriteLine("Недостаточно прав: обратитесь к администратору департамента лекарственных средств");
}
```

### Что проверить

| Проверка | Как |
|---|---|
| Все QR-коды зарегистрированы | Вызовите `ProductInquiryByQrCodeAsync` для первого и последнего кода |
| Состояние упаковки | В ответе проверки `ProductState` == `Import` |
| Партия верна | `result.BatchNo`, `result.Gtin`, `result.ExpirationDate` совпадают с вашими данными |

### Частые проблемы

| Ошибка | Причина | Решение |
|---|---|---|
| 400 «некорректные данные» | Дата в формате с временем (`2025-01-01T00:00:00`) | SDK уже пишет `yyyy-MM-dd`; проверьте, что не подменяете сериализацию |
| 400 по `qrCodes` | QR-код не выпущен для этого GTIN или уже использован | Сверьте партию и GTIN с заказом кодов |
| 403 | Права на метод не настроены | Обратитесь к администратору департамента лекарственных средств |
| 400 по `qrTypeId` | Тип кода не совпадает с форматом Data Matrix | Сверьте с `GetSupportedQrTypesAsync` |

---

## Шаг 2. Склад импортёра: постановка на учёт и проверка остатков

### Что происходит

Партия физически размещена на складе. Оператор фиксирует это в системе — упаковки получают
состояние `Stock` (13). После этого склад может передавать их дальше и видеть в отчёте остатков.

**Обязательные поля:** `DeclarationDate`, `StakeholderCode`.

### Код: постановка на складской учёт

```csharp
using Knmdb.TrackAndTrace;
using Knmdb.TrackAndTrace.Models.Requests;

using var knddb = KnddbApiClient.Create(KnddbEnvironment.Test);
await knddb.SignInAsync("ваш_логин", "ваш_пароль");

var stock = await knddb.StockDeclarationAsync(new StockDeclarationRequest
{
    DeclarationDate = DateTimeOffset.Now,   // формат yyyy-MM-dd проставит SDK
    StakeholderCode = 100,                  // код склада из GetAllStakeholdersAsync
    Description = "Приёмка партии B-2025-01",

    // Детали можно не заполнять: если Details пуст,
    // сервер поставит на учёт всю партию декларации импорта.
    Details =
    [
        new StockDeclarationDetail
        {
            QrCode = "010460000000001721SN00000000000000011727010110B-2025-01",
            BatchNumber = "B-2025-01",
            ExpirationDate = new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero),
        },
    ],
});

Console.WriteLine($"Складская декларация: {stock!.DeclarationId} от {stock.DeclarationDate:yyyy-MM-dd}");
```

### Код: сводка остатков

```csharp
// Сводка по всем препаратам (GTIN + количество) — для отчёта кладовщика.
var summary = await knddb.GetStockInheldListAsync();

Console.WriteLine($"Позиций: {summary!.StockInheldSummaryList?.Count ?? 0}");
foreach (var item in summary.StockInheldSummaryList ?? [])
{
    Console.WriteLine($"{item.Gtin}  {item.FullBrandName}  —  {item.Amount} уп.");
}
```

### Код: детализация по препарату

```csharp
// Упаковка за упаковкой: серийные номера, партии, сроки, состояния.
var details = await knddb.GetStockInheldListByGtinAsync("04600000000017");

foreach (var box in details!.StockInheldList ?? [])
{
    Console.WriteLine(
        $"SN {box.SerialNumber} | партия {box.BatchNumber} | " +
        $"годен до {box.ExpirationDate:yyyy-MM-dd} | {box.CurrentState} | {box.Price} сом");
}
```

### Что проверить

| Проверка | Как |
|---|---|
| Упаковки на складе | `GetStockInheldListByGtinAsync` возвращает ваши серийные номера |
| Состояние | `StockInheldInfo.CurrentState` == `Stock` |
| Количество | `StockInheldSummaryInfo.Amount` совпадает с числом QR-кодов |

### Отмена ошибочной постановки

```csharp
await knddb.StockDeclarationCancelAsync(declarationId: 555);
```

> Всегда проверяйте `IsSuccess`: сервер может вернуть HTTP 200 и `IsSuccess = false`
> с пояснением в `Message`.

```csharp
var cancel = await knddb.StockDeclarationCancelAsync(declarationId: 555);
if (cancel is { IsSuccess: false })
{
    Console.WriteLine($"Отменить не удалось: {cancel.Message}");
}
```

---

## Шаг 3. Склад → склад (оптовая передача)

### Что происходит

Дистрибьютор (или центральный склад импортёра) передаёт упаковки региональному складу или
сразу аптеке. Операция выполняется **в два действия разными организациями**:

1. **Отправитель** создаёт перемещение — упаковки переходят в `TransferInitiated` (9).
2. **Получатель** подтверждает приём — упаковки переходят в `TransferAccepted` (10) и меняют
   держателя.

Пока приём не подтверждён, упаковки «висят» в перемещении и недоступны для продажи.


### Код: отправитель создаёт перемещение

```csharp
using Knmdb.TrackAndTrace;
using Knmdb.TrackAndTrace.Models.Requests;

using var knddb = KnddbApiClient.Create(KnddbEnvironment.Production);

await knddb.SignInAsync("ваш_логин", "пароль", sessionKey: "warehouse:main");

var transfer = await knddb.TransferDeclarationAsync(new TransferDeclarationRequest
{
    FromStakeholder = 100,   // код отправителя (свой склад)
    ToStakeholder = 205,     // код получателя (аптека или другой склад)

    DeclarationDate = DateTimeOffset.Now,
    DocumentNo = "ТТН-000077",
    DocumentDate = DateTimeOffset.Now,
    Description = "Плановая отгрузка в аптеку №5",

    Details =
    [
        new TransferDeclarationDetail { QrCode = "010460000000001721SN...0001", Price = 140.00m },
        new TransferDeclarationDetail { QrCode = "010460000000001721SN...0002", Price = 140.00m },
    ],
});

Console.WriteLine($"Перемещение №{transfer!.DeclarationId} создано {transfer.DeclarationDate:yyyy-MM-dd}");

// Передайте declarationId получателю: по телефону, в ЭДО, через свой сервис.
```

### Код: получатель подтверждает приём

```csharp
using var knddb = KnddbApiClient.Create(KnddbEnvironment.Production);

// ВАЖНО: вход под учётной записью ПОЛУЧАТЕЛЯ —
// подтвердить перемещение может только организация-получатель.
await knddb.SignInAsync("ваш_логин", "ваш_пароль");

// Найти перемещения, ожидающие приёмки
var pending = await knddb.GetTransferListByFilterAsync(new GetTransferListByFilterRequest
{
    CurrentState = TransferState.Initiated,   // ждёт подтверждения
    TransferType = TransferType.Accept,       // я — получатель
});

foreach (var t in pending!.TransferDeclarationList ?? [])
{
    Console.WriteLine($"№{t.DeclarationId} от {t.FromStakeholder} — {t.TotalCount} уп. ({t.DocumentNo})");
}

// Посмотреть состав перед подтверждением
var composition = await knddb.GetTransferDeclarationAsync(declarationId: 9001);
foreach (var item in composition!.Details ?? [])
{
    Console.WriteLine($"  {item.SerialNumber} | {item.FullBrandName} | {item.Price} сом");
}

// Подтвердить приём
var accepted = await knddb.TransferAcceptAsync(declarationId: 9001);
Console.WriteLine($"Принято: {accepted!.DeclarationId} от {accepted.DeclarationDate:yyyy-MM-dd HH:mm}");
```

### Что проверить

| Проверка | Как |
|---|---|
| Перемещение в ожидании | `GetTransferListByFilterAsync` с `CurrentState = Initiated`, `TransferType = Accept` |
| Состав совпадает с накладной | `GetTransferDeclarationAsync(...).Details` |
| Упаковки у получателя | После приёмки: `GetStockInheldListByGtinAsync` под учётной записью получателя |
| Нет «зависших» перемещений | Периодически проверяйте список `Initiated` — это забытые приёмки |

---

## Шаг 4. Аптека: приёмка

Приёмка описана в [шаге 3](#шаг-3-склад--склад-оптовая-передача) — операция та же
(`TransferAccept`), отличается только роль организации.

**Рекомендация:** сразу при подтверждении приёмки:

1. Проверьте сроки годности и состояние каждой упаковки.
2. Сохраните `declarationId` и список `SerialNumber` в своей учётной системе.
3. Для лекарств с частичным отпуском узнайте остаток:
   `GetPartialSaleInfoAsync(qrCode)`.

---

## Шаг 5. Аптека: продажа покупателю

### Что происходит

Фармацевт сканирует Data Matrix код. Интеграция отправляет декларацию реализации —
упаковка выбывает из оборота (`Sales`, 3). Продажа должна быть подтверждена в системе
в момент отпуска, иначе упаковка останется числиться в наличии.

**Обязательные поля:** `IsPharmacyConsumption` (`true` для аптеки) и `Details`.

### Код

```csharp
using Knmdb.TrackAndTrace;
using Knmdb.TrackAndTrace.Models.Requests;

using var knddb = KnddbApiClient.Create(KnddbEnvironment.Production);
await knddb.SignInAsync("ваш_логин", "ваш_пароль");

var sale = await knddb.SalesDeclarationAsync(new SalesDeclarationRequest
{
    IsPharmacyConsumption = true,     // аптечная (розничная) продажа

    // Необязательные реквизиты — заполняйте, если есть:
    PrescriptionId = "РЕЦ-2025-000431",
    PatientId = "12345678901234",     // ПИН гражданина (для льготного отпуска)

    Details =
    [
        new SalesDeclarationDetail
        {
            QrCode = "010460000000001721SN...0001",
            Price = 165.00m,
            IsPartialSale = false,
        },
    ],
});

Console.WriteLine($"Продажа оформлена: {sale!.DeclarationId} от {sale.DeclarationDate:yyyy-MM-dd HH:mm}");
Console.WriteLine($"Организация: {sale.StakeholderCode}");
```

### Пакетная продажа (несколько упаковок в одном чеке)

```csharp
var scannedCodes = new[] { "код1", "код2", "код3" };

var sale = await knddb.SalesDeclarationAsync(new SalesDeclarationRequest
{
    IsPharmacyConsumption = true,
    Details = scannedCodes
        .Select(code => new SalesDeclarationDetail
        {
            QrCode = code,
            Price = 165.00m,
            IsPartialSale = false,
        })
        .ToList(),
});
```

### Что проверить

| Проверка | Как |
|---|---|
| Продажа прошла | `SalesDeclarationAsync` вернул `DeclarationId` |
| Упаковка выбыла | `ProductInquiryByQrCodeAsync` → `ProductState == Sales`, `IsAvailableForSale == false` |
| Нет в остатках | `GetStockInheldListByGtinAsync` больше не содержит этот серийный номер |

> **Проверка перед продажей.** Если аптека должна продавать только «легальные» упаковки,
> перед оформлением вызывайте `ProductInquiryByQrCodeAsync` и проверяйте
> `IsAvailableForSale`, `IsExpired`, `IsSuspendedOrRecalled`.

---

## Шаг 6. Аптека: частичная продажа

### Что происходит

Препарат отпускается не целой упаковкой (например, продали 10 таблеток из 30).
Упаковка получает состояние `PartialSales` (12), а на ней остаётся неизрасходованный остаток.

**Роль:** `ApiSalesDeclaration`.
**Ключевые поля:** `IsPartialSale = true` и `PartialSaleAmount`.

### Код: частичная продажа

```csharp
using Knmdb.TrackAndTrace;
using Knmdb.TrackAndTrace.Models.Requests;

using var knddb = KnddbApiClient.Create(KnddbEnvironment.Production);
await knddb.SignInAsync("ваш_логин", "ваш_пароль");

var qrCode = "010460000000001721SN...0003";

// 1. Узнаём, сколько уже реализовано из этой упаковки
var partial = await knddb.GetPartialSaleInfoAsync(qrCode);
Console.WriteLine($"Реализовано ранее: {partial!.PartialSaleAmount}");

// 2. Оформляем отпуск части упаковки
var sale = await knddb.SalesDeclarationAsync(new SalesDeclarationRequest
{
    IsPharmacyConsumption = true,
    Details =
    [
        new SalesDeclarationDetail
        {
            QrCode = qrCode,
            Price = 60.00m,            // цена отпущенной части
            IsPartialSale = true,
            PartialSaleAmount = 10,    // количество в этой операции
        },
    ],
});

Console.WriteLine($"Частичный отпуск: {sale!.DeclarationId}");

// 3. Проверяем остаток в упаковке
var inquiry = await knddb.ProductInquiryByQrCodeAsync(qrCode);
Console.WriteLine($"Состояние: {inquiry!.ProductState}");
Console.WriteLine($"Остаток в упаковке: {inquiry.PartialSaleRemainingAmount}");
```

### Код: списание остатка упаковки

Когда остаток больше не будет продаваться, упаковку нужно вывести из оборота
(см. [шаг 8](#шаг-8-выбытие-деактивация-и-списание)) — иначе она останется «в наличии».

```csharp
await knddb.DeactivateDeclarationAsync(new DeactivateDeclarationRequest
{
    ConsumptionType = ConsumptionType.Consumption,   // расход
    Description = "Списание остатка после частичных продаж",
    Details = [new DeactivateDeclarationDetail { QrCode = qrCode }],
});
```

### Отмена частичной продажи

```csharp
// Нужно понять, какая декларация оформила частичную продажу.
var history = await knddb.ProductInquiryByQrCodeAsync(qrCode);
var lastPartial = history!.ProductInquiryHistory!
    .LastOrDefault(h => h.State == ProductState.PartialSales);

if (lastPartial is not null)
{
    await knddb.SalesDeclarationCancelAsync(declarationId: lastPartial.DeclarationNumber);

    // Либо отмена по конкретной упаковке, если декларацию целиком отменять нельзя:
    // await knddb.SalesDeclarationBoxCancelAsync(qrCode);
}
```

---

## Шаг 7. Больница: расход по требованию

### Что происходит

Медицинская организация отпускает препарат по требованию отделения.
Отличие от аптеки — **другие обязательные реквизиты**: `IsPharmacyConsumption = false`,
а также номер требования и отделение.

### Код

```csharp
using Knmdb.TrackAndTrace;
using Knmdb.TrackAndTrace.Models.Requests;

using var knddb = KnddbApiClient.Create(KnddbEnvironment.Production);
await knddb.SignInAsync("ваш_логин", "ваш_пароль");

var consumption = await knddb.SalesDeclarationAsync(new SalesDeclarationRequest
{
    IsPharmacyConsumption = false,          // расход медорганизации, не розница

    RequestNumber = "ТРЕБ-2025-0042",       // номер требования
    DepartmentName = "Кардиологическое отделение",
    BranchDefId = "dep-07",                 // идентификатор подразделения у вас

    PatientId = "12345678901234",           // ПИН пациента
    PrescriptionId = "РЕЦ-2025-000777",

    Details =
    [
        new SalesDeclarationDetail { QrCode = "010460000000001721SN...0010", Price = 0m },
    ],
});

Console.WriteLine($"Расход оформлен: {consumption!.DeclarationId}");
```

> Если препарат не подлежит прослеживаемости (`TrackAndTraceStatus.NotTracked`),
> декларация всё равно оформляется, но требования к маркировке на стороне KNMDB мягче.

---

## Шаг 8. Выбытие: деактивация и списание

### Что происходит

Препарат выводится из оборота без продажи: истёк срок годности, брак, утеря, уничтожение,
инвентаризационная недостача. Обязательно указывается **причина** — тип расходной операции.

**Роль:** `ApiDeactivateDeclaration`.

| Причина | `ConsumptionType` | Число |
|---|---|---|
| Истёк срок годности | `DisposalForDeadline` | 40 |
| Отзыв партии из обращения | `DisposalDueToWithdrawal` | 30 |
| Производственный брак | `ProductionWastage` | 20 |
| Инвентаризация (недостача) | `Revision` | 50 |
| Расход (использование) | `Consumption` | 60 |
| Утеря | `Lost` | 70 |
| Повреждено | `Damaged` | 80 |
| Похищено | `Stolen` | 90 |
| Образец | `Sample` | 100 |

### Код

```csharp
using Knmdb.TrackAndTrace;
using Knmdb.TrackAndTrace.Models.Requests;
using Knmdb.TrackAndTrace.Models.Enums;

using var knddb = KnddbApiClient.Create(KnddbEnvironment.Production);
await knddb.SignInAsync("ваш_логин", "ваш_пароль");

// Списание партии с истёкшим сроком годности
var expired = new[] { "код1", "код2", "код3" };

var result = await knddb.DeactivateDeclarationAsync(new DeactivateDeclarationRequest
{
    ConsumptionType = ConsumptionType.DisposalForDeadline,
    Description = "Списание по акту №АКТ-2025-018 от 01.03.2025",
    Details = expired.Select(c => new DeactivateDeclarationDetail { QrCode = c }).ToList(),
});

Console.WriteLine($"Деактивация: {result!.DeclarationId} от {result.DeclarationDate:yyyy-MM-dd}");
```

### Поиск просроченных упаковок

```csharp
// Пример: найти на своём складе всё, что истекает в ближайшие 30 дней,
// и подготовить акт списания.
var soon = DateTimeOffset.UtcNow.AddDays(30);
var toWriteOff = new List<string>();

var stock = await knddb.GetStockInheldListAsync();
foreach (var summary in stock!.StockInheldSummaryList ?? [])
{
    var boxes = await knddb.GetStockInheldListByGtinAsync(summary.Gtin!);
    foreach (var box in boxes!.StockInheldList ?? [])
    {
        if (box.ExpirationDate <= soon && box.QrCode is not null)
        {
            toWriteOff.Add(box.QrCode);
        }
    }
}

Console.WriteLine($"К списанию: {toWriteOff.Count} упаковок");
```

---

## Возвраты

### Возврат от аптеки поставщику

Возвратное перемещение **не требует указывать отправителя и получателя** — сервер определяет
их по истории движения упаковки.

```csharp
using var knddb = KnddbApiClient.Create(KnddbEnvironment.Production);
await knddb.SignInAsync("ваш_логин", "ваш_пароль");

// 1. Аптека создаёт возврат
var ret = await knddb.TransferReturnAsync(new TransferReturnRequest
{
    DocumentNo = "ВОЗВР-2025-003",
    DocumentDate = DateTimeOffset.Now,
    Description = "Возврат по согласованию от 01.03.2025",
    Details =
    [
        new TransferReturnRequestDetail { QrCode = "010460000000001721SN...0005" },
    ],
});

Console.WriteLine($"Возврат №{ret!.DeclarationId}");

// 2. Поставщик подтверждает приём возврата (под своей учётной записью)
await knddb.SignInAsync("логин_поставщика", "пароль", sessionKey: "supplier");
await knddb.TransferAcceptAsync(declarationId: ret.DeclarationId);
```

### Отмена возврата

```csharp
await knddb.TransferReturnCancelAsync(declarationId: ret.DeclarationId);
```

---

## Отмены и ошибки оператора

Все отмены выполняйте **как можно раньше**: пока упаковка не передана дальше, отмена проходит
без последствий.

| Что отменяем | Метод | Когда возможно |
|---|---|---|
| Ошибочную постановку на склад | `StockDeclarationCancelAsync(declarationId)` | Пока упаковки на складе |
| Декларацию реализации целиком | `SalesDeclarationCancelAsync(declarationId)` | Пока не начаты другие операции |
| Продажу одной упаковки | `SalesDeclarationBoxCancelAsync(qrCode)` | До передачи упаковки |
| Исходящее перемещение | `TransferCancelAsync(declarationId)` | Пока получатель не подтвердил |
| Возвратное перемещение | `TransferReturnCancelAsync(declarationId)` | Пока получатель не подтвердил |

```csharp
// Пример: оператор ошибся и передал не те упаковки
try
{
    await knddb.TransferCancelAsync(declarationId: 9001);
    Console.WriteLine("Перемещение отменено");
}
catch (KnddbApiException ex) when (ex.IsBadRequest || ex.StatusCode == System.Net.HttpStatusCode.Conflict)
{
    // Получатель уже подтвердил приём — нужен обратный возврат
    Console.WriteLine($"Отменить нельзя: {ex.Message}. Оформите возврат.");
    await knddb.TransferReturnAsync(new TransferReturnRequest { /* ... */ });
}
```

### Единая обработка ошибок в интеграции

> **Обязательно к прочтению перед реализацией.** Ошибки бизнес-логики KNMDB
> приходят **с HTTP-статусом 200** в конверте `{ resultCode, resultMessage,
> actionResult }`. Проверка только HTTP-статуса считает такую ошибку успехом
> и возвращает пустой объект. SDK распознаёт эти ошибки сам и выбрасывает
> исключение, поэтому достаточно ловить его — но помните, что `StatusCode`
> в этом случае будет `200`, а причина лежит в `ResultCode`.

```csharp
using Microsoft.Extensions.Logging;

static async Task<T?> SafeAsync<T>(Func<Task<T?>> operation, ILogger logger)
{
    try
    {
        return await operation();
    }
    catch (KnddbAuthenticationException ex)
    {
        // Токен истёк и не обновился — нужен повторный вход
        logger.LogWarning(ex, "Требуется повторный вход в KNMDB");
        return default;
    }
    catch (KnddbApiException ex) when (ex.IsProductNotFound)
    {
        // Упаковка не найдена: HTTP 404 ИЛИ код 6022 в конверте с HTTP 200
        logger.LogInformation("Упаковка не найдена: {Uri}", ex.RequestUri);
        return default;
    }
    catch (KnddbApiException ex) when (ex.IsProductNotSuitableForSale)
    {
        // Код 6023: чаще всего повторная продажа уже проданной упаковки.
        // Это ошибка оператора, а не сбой — сообщаем понятно.
        logger.LogWarning("Упаковка не подходит для продажи: {Description}", ex.GetResultDescription());
        return default;
    }
    catch (KnddbApiException ex) when (ex.IsBusinessError)
    {
        // Любая другая ошибка бизнес-логики: HTTP-статус 200, причина в ResultCode
        logger.LogWarning(
            "KNMDB отклонил операцию. resultCode={ResultCode}; {Description}",
            ex.ResultCode,
            ex.GetResultDescription());
        return default;
    }
    catch (KnddbApiException ex) when (ex.IsForbidden)
    {
        logger.LogError("Недостаточно прав для {Uri}. Обратитесь к администратору департамента лекарственных средств", ex.RequestUri);
        return default;
    }
    catch (KnddbApiException ex) when (ex.IsTransientFailure)
    {
        // 5xx или 408 — можно поставить в очередь на повтор
        logger.LogWarning(ex, "Временная ошибка KNMDB, операция будет повторена");
        throw;
    }
    catch (KnddbApiException ex)
    {
        logger.LogError(ex, "Ошибка KNMDB: {Message}; traceId={TraceId}", ex.Message, ex.TraceId);
        return default;
    }
}
```

### Операторские ошибки, которые нужно показывать понятно

Эти коды приходят с HTTP 200, но означают ошибку оператора. Их не нужно
логировать как сбои — их нужно объяснить пользователю:

| Код | Что случилось | Что показать оператору |
|---|---|---|
| `6022` | Упаковка с таким QR-кодом не найдена | «Упаковка не найдена. Проверьте код и повторите сканирование» |
| `6023` | Упаковка не подходит для продажи | «Упаковка уже продана или не подходит для продажи» |
| `6004` | QR-код уже зарегистрирован | «Эта упаковка уже введена в оборот» |
| `6033` | Упаковка уже объявлена | «Упаковка уже объявлена ранее» |
| `6034` | Декларация другой организации | «Документ принадлежит другой организации» |
| `6039` | Пара «GTIN + серийный номер» не найдена | «Упаковка не найдена. Проверьте GTIN и серийный номер» |
| `6045` | Упаковку нельзя деактивировать | «Упаковка продана — деактивация невозможна» |
| `1` | Ошибка валидации | Показать поля из `ex.Errors` рядом с полями формы |

```csharp
var message = ex.ResultCode switch
{
    KnddbResultCodes.ProductWithQrCodeNotFound =>
        "Упаковка не найдена. Проверьте код и повторите сканирование.",
    KnddbResultCodes.ProductWithQrCodeNotSuitableForSale =>
        "Упаковка уже продана или не подходит для продажи.",
    KnddbResultCodes.SimilarQrCodeInDatabase =>
        "Эта упаковка уже введена в оборот.",
    KnddbResultCodes.ProductAlreadyDeclared =>
        "Упаковка уже объявлена ранее.",
    KnddbResultCodes.DeclarationDoesntBelongToYou =>
        "Документ принадлежит другой организации.",
    KnddbResultCodes.ProductCannotBeDeactivated =>
        "Упаковка продана — деактивация невозможна.",
    _ => ex.GetResultDescription() ?? "Операция отклонена сервером KNMDB.",
};
```

### Если вход не работает

| Симптом | Причина | Решение |
|---|---|---|
| `resultCode 2`, «An error occurred while saving the entity changes» на входе | Не отправлен заголовок `User-Agent`. Сервер записывает его в базу при входе (`AuthorizationController`, поле `UserAgent`) — без заголовка запись падает | SDK отправляет `User-Agent` всегда (`Knddb.TrackAndTrace.SDK/1.0`). Если задаёте свой — убедитесь, что он не пустой |
| `resultCode 2` на методе с датой | Дата передана с временем: `2026-03-15T12:00:00Z` | Сервер принимает только `yyyy-MM-dd`. SDK отбрасывает время автоматически |
| «Сервер не вернул поле `access_token`» | Ошибка на `/connect/token` пришла с HTTP 200 и конвертом | Обновлённые версии SDK сообщают настоящую причину: `Сервер KNMDB не выполнил вход (resultCode …)` |

Проверка «на живом» сервере: если запрос не проходит без `User-Agent`,
но проходит с **любым** его значением — причина именно в заголовке.

---

## Инвентаризация и сверка остатков

Типовая задача склада: сверить данные своей учётной системы с KNMDB.

```csharp
using var knddb = KnddbApiClient.Create(KnddbEnvironment.Production);
await knddb.SignInAsync("ваш_логин", "ваш_пароль");

// Остатки в KNMDB
var summary = await knddb.GetStockInheldListAsync();
var knddbStock = summary!.StockInheldSummaryList!
    .ToDictionary(x => x.Gtin!, x => x.Amount);

// Ваши остатки (из своей базы)
var localStock = new Dictionary<string, decimal>
{
    ["04600000000017"] = 150m,
    ["04600000000024"] = 80m,
};

foreach (var (gtin, amount) in localStock)
{
    var inKnddb = knddbStock.GetValueOrDefault(gtin, 0m);

    if (inKnddb != amount)
    {
        Console.WriteLine($"РАСХОЖДЕНИЕ {gtin}: у вас {amount}, в KNMDB {inKnddb}");
    }
}

// Детализация для уточнения: какие именно серийные номера числятся в KNMDB
foreach (var gtin in localStock.Keys)
{
    var details = await knddb.GetStockInheldListByGtinAsync(gtin);
    var serials = details!.StockInheldList!.Select(b => b.SerialNumber);

    Console.WriteLine($"{gtin}: {string.Join(", ", serials)}");
}
```

> Расхождения чаще всего вызваны **неподтверждёнными перемещениями** (`TransferInitiated`).
> Проверяйте их через `GetTransferListByFilterAsync(CurrentState = Initiated)`.

---

## Мобильное приложение покупателя

Проверка лекарства доступна **без авторизации** — удобно для публичного приложения.

```csharp
using Knmdb.TrackAndTrace;

public sealed class MedicineVerificationService
{
    private readonly KnddbApiClient _knddb;

    public MedicineVerificationService()
        => _knddb = KnddbApiClient.Create(KnddbEnvironment.Production);

    /// <summary>Проверяет упаковку по отсканированному QR-коду.</summary>
    public async Task<VerificationResult> VerifyAsync(string qrCode, CancellationToken ct = default)
    {
        try
        {
            var medicine = await _knddb.ProductInquiryByQrCodeAsync(qrCode, ct);

            if (medicine is null)
            {
                return VerificationResult.NotFound();
            }

            return new VerificationResult
            {
                Found = true,
                ProductName = medicine.ProductName,
                Manufacturer = medicine.ManufacturerName,
                BatchNumber = medicine.BatchNumber,
                ExpirationDate = medicine.ExpirationDate,
                CurrentHolder = medicine.StakeHolderName,
                IsExpired = medicine.IsExpired,
                IsAvailableForSale = medicine.IsAvailableForSale,
                IsSuspendedOrRecalled = medicine.IsSuspendedOrRecalled,
                SuspendReason = medicine.SuspendRecallInfo?.Reason,
                State = medicine.ProductState,
                InstructionDocId = medicine.InstructionForUseDocId,
            };
        }
        catch (KnddbApiException ex) when (ex.IsNotFound)
        {
            return VerificationResult.NotFound();
        }
    }
}
```

### Что показывать покупателю

| Ситуация | Что показать |
|---|---|
| `IsSuspendedOrRecalled` | ⚠️ «Упаковка отозвана/приостановлена. Причина: …» — красный экран |
| `IsExpired` | ⚠️ «Срок годности истёк» — предупреждение |
| `!IsAvailableForSale` и не просрочена | «Упаковка уже выбыла из оборота» — жёлтый экран |
| Всё в порядке | ✅ Данные препарата, производитель, держатель, срок годности |

Дополнительно можно показать историю движения — она уже пришла в ответе:

```csharp
foreach (var step in medicine.ProductInquiryHistory ?? [])
{
    Console.WriteLine($"{step.StateDate:dd.MM.yyyy}  {step.State,-25}  {step.StakeHolder}");
}
```

---

## Синхронизация справочника лекарств

Справочник нужен, чтобы сопоставлять GTIN с торговыми наименованиями и понимать,
какие препараты подлежат маркировке.

```csharp
using var knddb = KnddbApiClient.Create(KnddbEnvironment.Production);
await knddb.SignInAsync("ваш_логин", "ваш_пароль");

// Первая загрузка: полный справочник
DateTimeOffset? lastUpdate = null;
var all = new Dictionary<string, (string Name, TrackAndTraceStatus Status)>();
var hasMore = true;

while (hasMore)
{
    var page = await knddb.GetMedicineListAsync(new GetMedicineListRequest { LastUpdate = lastUpdate });

    if (page?.MedicineList is null || page.MedicineList.Count == 0)
    {
        hasMore = false;
        break;
    }

    foreach (var medicine in page.MedicineList)
    {
        all[medicine.Gtin!] = (medicine.FullBrandName ?? medicine.BrandName ?? "", medicine.TrackAndTraceStatus);
    }

    // Следующая итерация — только изменения после максимальной даты
    lastUpdate = page.MedicineList.Max(m => m.LastUpdate);
    Console.WriteLine($"Загружено {all.Count} препаратов, до {lastUpdate:u}");
}

// Далее — ежедневно/ежечасно передавайте сохранённый lastUpdate.
// Сохраните его в своей базе, чтобы не загружать справочник заново.
```

Проверка перед продажей — подлежит ли препарат маркировке:

```csharp
if (all.TryGetValue(gtin, out var info))
{
    switch (info.Status)
    {
        case TrackAndTraceStatus.NotTracked:
            // Маркировка не требуется
            break;
        case TrackAndTraceStatus.LabelMandatory:
            // Нужна этикетка; прослеживаемость упаковок не обязательна
            break;
        case TrackAndTraceStatus.LabelAndTraceMandatory:
            // Полный контроль: продажа только по зарегистрированному QR-коду
            break;
    }
}
```

---

## Чек-лист внедрения

### Подготовка

- [ ] Получены логин и пароль для каждой организации (роли настраиваются автоматически)
- [ ] Определены коды организаций (`GetAllStakeholdersAsync`) и сохранены в конфигурации
- [ ] Значение `qrTypeId` согласовано с департаментом и задано константой в конфигурации
- [ ] Выбран контур: `Test` для отладки, `Production` для эксплуатации
- [ ] Настроено хранение `access_token`/`refresh_token` (если интеграция многопроцессная)

### Импортёр / производитель

- [ ] `ImportDeclaration` / `ProductionDeclaration` вызывается на каждую партию
- [ ] Обработаны ошибки 400 с разбором `ex.Errors` по полям
- [ ] Сохраняется `declarationId` для последующей сверки

### Склад

- [ ] `StockDeclaration` выполняется после физической приёмки
- [ ] `GetStockInheldList` используется для ежедневного отчёта
- [ ] Реализован контроль сроков годности и подготовка актов списания

### Перемещения

- [ ] Отправитель создаёт `TransferDeclaration` и передаёт `declarationId`
- [ ] Получатель ежедневно проверяет `GetTransferListByFilter(CurrentState = Initiated)`
- [ ] Реализовано подтверждение `TransferAccept` в момент фактической приёмки
- [ ] Есть контроль «зависших» перемещений (старше N дней)

### Аптека / больница

- [ ] `SalesDeclaration` вызывается в момент отпуска
- [ ] Для больниц заполняются `RequestNumber`, `DepartmentName`
- [ ] Реализована частичная продажа (`IsPartialSale`, `PartialSaleAmount`)
- [ ] Списание остатков частично проданных упаковок
- [ ] Есть отмены: `SalesDeclarationCancel`, `SalesDeclarationBoxCancel`

### Эксплуатация

- [ ] Настроено журналирование с `ex.TraceId` для обращений в поддержку
- [ ] Есть обработка `KnddbAuthenticationException` (повторный вход)
- [ ] Есть политика повторов для `IsTransientFailure`
- [ ] При ошибке 403 формируется обращение к администратору департамента лекарственных средств
      (роли настраиваются через административную панель, со стороны интеграции правки не нужны)

---

## Типичные ошибки при интеграции

### Ошибки, найденные при тестировании на боевом контуре

Эти три случая проверены на реальном сервере и чаще всего застают интегратора
врасплох:

| Симптом | Причина | Решение |
|---|---|---|
| Метод «успешен», но данные пустые | Ошибка бизнес-логики пришла с **HTTP 200** в конверте `{ resultCode, resultMessage, actionResult }` | Проверять `resultCode`, а не HTTP-статус. SDK делает это автоматически и выбрасывает `KnddbApiException` |
| Вход падает с `resultCode 2`, «An error occurred while saving the entity changes» | Не отправлен заголовок `User-Agent`: сервер пишет его в базу при входе | SDK отправляет `User-Agent` всегда. Задавайте свой через `UserAgent` в настройках |
| Метод с датой падает с `resultCode 2` | Дата передана с временем: `2026-03-15T12:00:00Z` | Сервер принимает только `yyyy-MM-dd`. SDK отбрасывает время автоматически |
| «Сервер не вернул поле `access_token`» | Ошибка на `/connect/token` пришла с HTTP 200 и конвертом | Обновлённые версии SDK сообщают настоящую причину и код |

### Прочие ошибки

| Симптом | Причина | Решение |
|---|---|---|
| 403 на одном или нескольких методах | Учётной записи не назначены права на эти методы | Обратиться к **администратору департамента лекарственных средств**: роли настраиваются через административную панель KNMDB |
| 403 на всех методах | Учётная запись не активирована или вход выполнен не под той учётной записью | Проверить логин; обратиться к администратору департамента |
| 401 после часа работы | Токен истёк, обновление не удалось | SDK обновляет автоматически; проверьте, что вход выполнен с `storeCredentials: true` |
| 400 «некорректные данные» | Дата с временем или неверный формат | SDK пишет `yyyy-MM-dd`; не переопределяйте сериализацию |
| Повторная продажа той же упаковки не отклоняется | Проверяется только HTTP-статус, а пришёл код `6023` с HTTP 200 | Проверять `ResultCode` / ловить `KnddbApiException` |
| Упаковки не видны в остатках | Перемещение не подтверждено | Проверить `GetTransferListByFilter(CurrentState = Initiated)` |
| `SalesDeclaration` возвращает ошибку | Упаковка не у текущей организации | Операция выполняется от имени держателя; проверьте ключ сессии |
| Часть упаковок не продаётся | Упаковка отозвана или просрочена | Проверить `IsSuspendedOrRecalled`, `IsExpired` |
| Справочник устарел | Не передаётся `LastUpdate` | Сохранять максимальную дату и передавать при следующем запросе |
| Разные операции «перепутаны» между филиалами | Используется одна сессия на всё | Разделить по `sessionKey` на каждую организацию |

---

## См. также

- [../README.md](../README.md) — обзор SDK, установка, быстрый старт
- [API-REFERENCE.md](API-REFERENCE.md) — все методы, поля запросов и ответов
- [ENUMS.md](ENUMS.md) — значения всех перечислений с пояснениями
