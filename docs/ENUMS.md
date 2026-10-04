# Перечисления (enum)

Все перечисления API KNMDB Track and Trace: числовые значения, человекочитаемые названия
и пояснения.

- [Формат в JSON](#формат-в-json)
- [StakeholderType — тип организации](#stakeholdertype--тип-организации)
- [ProductState — состояние упаковки](#productstate--состояние-упаковки)
- [ProductStatus — складской статус](#productstatus--складской-статус)
- [TransferState — состояние перемещения](#transferstate--состояние-перемещения)
- [TransferType — роль в перемещении](#transfertype--роль-в-перемещении)
- [ConsumptionType — тип списания](#consumptiontype--тип-списания)
- [TrackAndTraceStatus — обязательность маркировки](#trackandtracestatus--обязательность-маркировки)

---

## Формат в JSON

> **Что важно знать до начала работы.**

| Направление | Формат |
|---|---|
| **Запись (SDK → KNMDB)** | **Число.** Сервер сериализует перечисления стандартными средствами Newtonsoft.Json (строковый конвертер не подключён), и OpenAPI-схема объявляет их как `"type": "integer", "format": "int32"`. |
| **Чтение (KNMDB → SDK)** | **Число или строка.** SDK принимает число, имя члена (`"Pharmacy"`, `"TransferInitiated"`) и человекочитаемое название (`"Label And TraceMandatory"`, `"DISPOSAL FOR DEADLINE"`). |

Терпимое чтение защищает интеграцию, если формат на стороне сервера изменится, и позволяет
читать заранее подготовленные JSON-файлы в человекочитаемом виде.

```csharp
// Запись: в JSON уйдёт 5
var stakeholder = new StakeholderType? { };   // пример типа
var json = JsonSerializer.Serialize(StakeholderType.Pharmacy, KnddbJson.DefaultOptions);
// json == "5"

// Чтение: все три варианта дают одно значение
JsonSerializer.Deserialize<StakeholderType>("5", KnddbJson.DefaultOptions);                      // Pharmacy
JsonSerializer.Deserialize<StakeholderType>("\"Pharmacy\"", KnddbJson.DefaultOptions);           // Pharmacy
JsonSerializer.Deserialize<StakeholderType>("\"medicalOrganization\"", KnddbJson.DefaultOptions); // MedicalOrganization
```

Пользоваться перечислениями в коде нужно как обычно — сравнением с членами:

```csharp
if (medicine.ProductState == ProductState.Sales)
{
    Console.WriteLine("Упаковка продана");
}

switch (stakeholder.Type)
{
    case StakeholderType.Importer:
        // логика импортёра
        break;
    case StakeholderType.Pharmacy:
        // логика аптеки
        break;
}
```

---

## `StakeholderType` — тип организации

Соответствует `KNDDBModel.TTStakeholder.StakeholderType`.
Возвращается в `StakeholderInfo.Type` метода `GetAllStakeholdersAsync`.

| Значение | Число | Название | Описание |
|---|---|---|---|
| `Producer` | 1 | `"Producer"` | **Производитель** — организация, выпускающая лекарственные средства |
| `Importer` | 2 | `"Importer"` | **Импортёр** — организация, ввозящая препараты на территорию страны |
| `Company` | 3 | `"Company"` | **Компания** — юридическое лицо общего профиля (дистрибьютор, представительство) |
| `Manufacturer` | 4 | `"Manufacturer"` | **Завод-изготовитель** |
| `Pharmacy` | 5 | `"Pharmacy"` | **Аптека** — организация розничной реализации |
| `MedicalOrganization` | 6 | `"MedicalOrganization"` | **Медицинская организация** (больница, поликлиника) |
| `Warehouse` | 7 | `"Warehouse"` | **Склад** — складское хранение и оптовая отгрузка |

**Какие методы доступны какому типу:**

| Тип | Типичные методы |
|---|---|
| `Producer`, `Manufacturer` | `ProductionDeclaration`, `StockDeclaration`, `TransferDeclaration` |
| `Importer` | `ImportDeclaration`, `StockDeclaration`, `TransferDeclaration` |
| `Warehouse`, `Company` | `TransferDeclaration`, `TransferAccept`, `GetStockInheldList`, `StockDeclaration` |
| `Pharmacy` | `TransferAccept`, `SalesDeclaration`, `GetPartialSaleInfo` |
| `MedicalOrganization` | `TransferAccept`, `SalesDeclaration`, `DeactivateDeclaration` |

---

## `ProductState` — состояние упаковки

Соответствует `KNDDBModel.TTProductBox.StatesEnum`.
Возвращается в `ProductInquiryResult.ProductState`, `ProductInquiryHistory.State`,
`StockInheldInfo.CurrentState`.

Описывает **последнее событие**, произошедшее с упаковкой, а не складской статус.
Для складского статуса используйте [`ProductStatus`](#productstatus--складской-статус).

| Значение | Число | Название | Описание | Каким методом устанавливается |
|---|---|---|---|---|
| `Production` | 1 | `"Production"` | Выпущена производителем | `ProductionDeclaration` |
| `Import` | 2 | `"Import"` | Ввезена на территорию страны | `ImportDeclaration` |
| `Sales` | 3 | `"Sales"` | Реализована конечному потребителю | `SalesDeclaration` |
| `Deactivation` | 4 | `"Deactivation"` | Выведена из оборота (списана) | `DeactivateDeclaration` |
| `Export` | 5 | `"Export"` | Вывезена за пределы страны | — |
| `SalesReturn` | 6 | `"Sales Return"` | Покупатель вернул упаковку | — |
| `PurchaseConfirmation` | 7 | `"Purchase Confirmation"` | Приёмка упаковки покупателем | — |
| `SalesCancelled` | 8 | `"Sales Cancelled"` | Продажа аннулирована | `SalesDeclarationCancel` |
| `TransferInitiated` | 9 | `"Transfer Initiated"` | Отправитель создал перемещение | `TransferDeclaration` |
| `TransferAccepted` | 10 | `"Transfer Accepted"` | Получатель подтвердил приём | `TransferAccept` |
| `TransferCancelled` | 11 | `"Transfer Cancelled"` | Перемещение отменено отправителем | `TransferCancel` |
| `PartialSales` | 12 | `"Partial Sales"` | Продана частично | `SalesDeclaration` с `IsPartialSale` |
| `Stock` | 13 | `"Stock"` | Поставлена на складской учёт | `StockDeclaration` |
| `ReturnTransferInitiated` | 14 | `"Return Transfer Initiated"` | Создан возврат поставщику | `TransferReturn` |
| `ReturnTransferAccepted` | 15 | `"Return Transfer Accepted"` | Возврат принят | `TransferAccept` |
| `ReturnTransferCancelled` | 16 | `"Return Transfer Cancelled"` | Возврат отменён | `TransferReturnCancel` |
| `PartialSalesCancelled` | 17 | `"Partial Sales Cancelled"` | Частичная продажа отменена | `SalesDeclarationBoxCancel` |
| `DeactivationCancelled` | 18 | `"Deactivation Cancelled"` | Деактивация отменена, упаковка в обороте | — |

**Последовательность «нормального» жизненного цикла:**

```text
Production (1) → Import (2) → Stock (13) → TransferInitiated (9) → TransferAccepted (10) → Sales (3)
```

Любое отклонение от этой цепочки — повод проверить, не пропущен ли шаг интеграции.

---

## `ProductStatus` — складской статус

Соответствует `KNDDBModel.TTProductBox.StatusEnum`.
Возвращается в `ProductInquiryResult.ProductStatus`.

| Значение | Число | Название | Описание |
|---|---|---|---|
| `InStock` | 1 | `"InStock"` | В наличии — на складе текущего держателя, не участвует в перемещении |
| `StockOut` | 2 | `"StockOut"` | Выбыла со склада (продана, экспортирована, деактивирована) |
| `InTransfer` | 3 | `"InTransfer"` | Находится в процессе передачи между организациями |

**Разница с `ProductState`:**

| | `ProductStatus` | `ProductState` |
|---|---|---|
| Что описывает | Складское положение сейчас | Последнее событие |
| Значений | 3 | 18 |
| Типичное использование | Логика доступности к продаже | История и отладка |

---

## `TransferState` — состояние перемещения

Соответствует `KNDDBModel.TTTransferDeclaration.TransferStates`.
Возвращается в `GetTransferDeclarationResponse.CurrentState`, `TransferDeclarationInfo.CurrentState`;
принимается в `GetTransferListByFilterRequest.CurrentState`.

| Значение | Число | Название | Описание |
|---|---|---|---|
| `Initiated` | 1 | `"Initiated"` | Инициировано; получатель ещё **не подтвердил** приём. Упаковки недоступны для продажи |
| `Accepted` | 2 | `"Accepted"` | Принято; право собственности перешло получателю |
| `Cancelled` | 3 | `"Cancelled"` | Отменено отправителем до подтверждения |

**Практическое правило:** все перемещения со значением `Initiated` старше нескольких дней —
это «зависшие» приёмки. Их стоит контролировать отдельным отчётом:

```csharp
var pending = await client.GetTransferListByFilterAsync(new GetTransferListByFilterRequest
{
    CurrentState = TransferState.Initiated,
    TransferType = TransferType.Accept,
});
```

---

## `TransferType` — роль в перемещении

Соответствует `KNDDBModel.TTTransferDeclaration.TransferTypes`.
Возвращается в `TransferDeclarationInfo.TransferType`;
принимается в `GetTransferListByFilterRequest.TransferType`.

| Значение | Число | Название | Описание |
|---|---|---|---|
| `Initiate` | 1 | `"Initiate"` | **Инициатор** — выступает отправителем перемещения |
| `Accept` | 2 | `"Accept"` | **Получатель** — принимает перемещение |

Используется, чтобы отличить входящие перемещения от исходящих при поиске по фильтру.

---

## `ConsumptionType` — тип списания

Соответствует `KNDDBModel.TTConsumptionDeclaration.ConsumptionTypeEnum`.
Принимается в `DeactivateDeclarationRequest.ConsumptionType`;
возвращается в `ProductInquiryResult.ConsumptionType`.

| Значение | Число | Название | Описание | Когда применять |
|---|---|---|---|---|
| `SystemIn` | 0 | `"SYSTEM IN"` | Системное поступление — ввод остатков в систему | Технические операции миграции |
| `SystemOut` | 10 | `"SYSTEM OUT"` | Системное выбытие — вывод остатков из системы | Технические операции миграции |
| `ProductionWastage` | 20 | `"PRODUCTION WASTAGE"` | Производственные потери (брак, отходы) | Брак на производстве |
| `DisposalDueToWithdrawal` | 30 | `"DISPOSAL DUE TO WITHDRAWAL"` | Уничтожение в связи с отзывом партии | Отзыв партии из обращения |
| `DisposalForDeadline` | 40 | `"DISPOSAL FOR DEADLINE"` | Уничтожение по истечении срока годности | **Самый частый случай на складе** |
| `Revision` | 50 | `"REVISION"` | Ревизия — корректировка по инвентаризации | Инвентаризационная недостача |
| `Consumption` | 60 | `"CONSUMPTION"` | Потребление (расход) | Использование в медорганизации, списание остатка |
| `Lost` | 70 | `"LOST"` | Утеряно | Утеря при транспортировке |
| `Damaged` | 80 | `"DAMAGED"` | Повреждено | Повреждение упаковки |
| `Stolen` | 90 | `"STOLEN"` | Похищено | Хищение |
| `Sample` | 100 | `"SAMPLE"` | Образец | Выдача препарата как образца |

> **Почему значения с шагом 10.** Шкала оставлена с промежутками, чтобы добавлять новые типы
> между существующими без изменения уже сохранённых в базе данных значений. Не полагайтесь
> на «последовательность» чисел — сравнивайте с членами перечисления.

```csharp
// Отображение причины для акта списания
static string GetWriteOffReason(ConsumptionType type) => type switch
{
    ConsumptionType.DisposalForDeadline => "Истёк срок годности",
    ConsumptionType.DisposalDueToWithdrawal => "Отзыв партии из обращения",
    ConsumptionType.ProductionWastage => "Производственный брак",
    ConsumptionType.Revision => "Инвентаризационная недостача",
    ConsumptionType.Consumption => "Расход",
    ConsumptionType.Lost => "Утеря",
    ConsumptionType.Damaged => "Повреждение",
    ConsumptionType.Stolen => "Хищение",
    ConsumptionType.Sample => "Образец",
    ConsumptionType.SystemIn => "Системное поступление",
    ConsumptionType.SystemOut => "Системное выбытие",
    _ => type.ToString(),
};
```

---

## `TrackAndTraceStatus` — обязательность маркировки

Соответствует `KNDDBModel.Product.TrackAndTraceStatusEnum`.
Возвращается в `MedicineInfo.TrackAndTraceStatus` метода `GetMedicineListAsync`.

| Значение | Число | Название | Описание |
|---|---|---|---|
| `NotTracked` | 1 | `"Not Required"` | Маркировка **не требуется** — препарат не подлежит прослеживаемости |
| `LabelMandatory` | 2 | `"Required"` | Маркировка **обязательна** — нужно нанесение этикетки (Data Matrix) |
| `LabelAndTraceMandatory` | 3 | `"Label And TraceMandatory"` | Обязательны **и маркировка, и прослеживаемость** — полный контроль движения упаковки |

**Как использовать при интеграции:**

```csharp
switch (medicine.TrackAndTraceStatus)
{
    case TrackAndTraceStatus.NotTracked:
        // Продажа оформляется без проверки QR-кода
        break;

    case TrackAndTraceStatus.LabelMandatory:
        // Нужна этикетка; обязательной регистрации каждой упаковки нет
        break;

    case TrackAndTraceStatus.LabelAndTraceMandatory:
        // Полный контроль: продажа только по зарегистрированному QR-коду
        // Перед отпуском проверяйте IsAvailableForSale
        break;
}
```

---

## См. также

- [README.md](README.md) — обзор SDK, установка, быстрый старт
- [SCENARIOS.md](SCENARIOS.md) — сценарии «от импортёра до аптеки» с кодом по шагам
- [API-REFERENCE.md](API-REFERENCE.md) — все методы и поля
