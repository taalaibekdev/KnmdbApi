using System.ComponentModel;

namespace Knmdb.TrackAndTrace.Models.Enums;

/// <summary>
/// Состояние (историческое событие) упаковки лекарственного средства.
/// </summary>
/// <remarks>
/// <para>
/// Соответствует серверному перечислению <c>KNDDBModel.TTProductBox.StatesEnum</c>.
/// </para>
/// <para>
/// Возвращается в свойствах:
/// <list type="bullet">
///   <item><description><c>currentState</c> — модель <c>StockInheldInfo</c> (остатки на складе);</description></item>
///   <item><description><c>productState</c> — модель <c>ProductInquiryResult</c> (текущее состояние упаковки);</description></item>
///   <item><description><c>state</c> — модель <c>ProductInquiryHistory</c> (история перемещений упаковки).</description></item>
/// </list>
/// </para>
/// <para>
/// Значение описывает <b>последнее событие</b>, которое произошло с упаковкой, а не складской статус.
/// Для складского статуса используйте <see cref="ProductStatus"/>.
/// </para>
/// <para>
/// По сети передаётся числом. SDK дополнительно принимает строковые представления:
/// как имя члена (<c>TransferInitiated</c>), так и человекочитаемое описание
/// (<c>Transfer Initiated</c>) — оно указано в атрибуте <see cref="DescriptionAttribute"/>.
/// </para>
/// </remarks>
public enum ProductState
{
    /// <summary>
    /// Производство — упаковка выпущена производителем.
    /// </summary>
    /// <remarks>Числовое значение: <c>1</c>.</remarks>
    [Description("Production")]
    Production = 1,

    /// <summary>
    /// Импорт — упаковка ввезена на территорию страны.
    /// </summary>
    /// <remarks>Числовое значение: <c>2</c>.</remarks>
    [Description("Import")]
    Import = 2,

    /// <summary>
    /// Продажа — упаковка реализована конечному потребителю.
    /// </summary>
    /// <remarks>Числовое значение: <c>3</c>.</remarks>
    [Description("Sales")]
    Sales = 3,

    /// <summary>
    /// Деактивация — упаковка выведена из оборота (списана).
    /// </summary>
    /// <remarks>Числовое значение: <c>4</c>.</remarks>
    [Description("Deactivation")]
    Deactivation = 4,

    /// <summary>
    /// Экспорт — упаковка вывезена за пределы страны.
    /// </summary>
    /// <remarks>Числовое значение: <c>5</c>.</remarks>
    [Description("Export")]
    Export = 5,

    /// <summary>
    /// Возврат продажи — покупатель вернул ранее проданную упаковку.
    /// </summary>
    /// <remarks>Числовое значение: <c>6</c>.</remarks>
    [Description("Sales Return")]
    SalesReturn = 6,

    /// <summary>
    /// Подтверждение закупки — приёмка упаковки покупателем.
    /// </summary>
    /// <remarks>Числовое значение: <c>7</c>.</remarks>
    [Description("Purchase Confirmation")]
    PurchaseConfirmation = 7,

    /// <summary>
    /// Продажа отменена — операция продажи аннулирована.
    /// </summary>
    /// <remarks>Числовое значение: <c>8</c>.</remarks>
    [Description("Sales Cancelled")]
    SalesCancelled = 8,

    /// <summary>
    /// Перемещение инициировано — отправитель создал передачу.
    /// </summary>
    /// <remarks>Числовое значение: <c>9</c>.</remarks>
    [Description("Transfer Initiated")]
    TransferInitiated = 9,

    /// <summary>
    /// Перемещение принято — получатель подтвердил приём упаковки.
    /// </summary>
    /// <remarks>Числовое значение: <c>10</c>.</remarks>
    [Description("Transfer Accepted")]
    TransferAccepted = 10,

    /// <summary>
    /// Перемещение отменено — передача аннулирована отправителем.
    /// </summary>
    /// <remarks>Числовое значение: <c>11</c>.</remarks>
    [Description("Transfer Cancelled")]
    TransferCancelled = 11,

    /// <summary>
    /// Частичная продажа — упаковка продана частично.
    /// </summary>
    /// <remarks>Числовое значение: <c>12</c>.</remarks>
    [Description("Partial Sales")]
    PartialSales = 12,

    /// <summary>
    /// Склад — упаковка поставлена на складской учёт.
    /// </summary>
    /// <remarks>Числовое значение: <c>13</c>.</remarks>
    [Description("Stock")]
    Stock = 13,

    /// <summary>
    /// Возвратное перемещение инициировано — создан возврат упаковки поставщику.
    /// </summary>
    /// <remarks>Числовое значение: <c>14</c>.</remarks>
    [Description("Return Transfer Initiated")]
    ReturnTransferInitiated = 14,

    /// <summary>
    /// Возвратное перемещение принято.
    /// </summary>
    /// <remarks>Числовое значение: <c>15</c>.</remarks>
    [Description("Return Transfer Accepted")]
    ReturnTransferAccepted = 15,

    /// <summary>
    /// Возвратное перемещение отменено.
    /// </summary>
    /// <remarks>Числовое значение: <c>16</c>.</remarks>
    [Description("Return Transfer Cancelled")]
    ReturnTransferCancelled = 16,

    /// <summary>
    /// Частичная продажа отменена.
    /// </summary>
    /// <remarks>Числовое значение: <c>17</c>.</remarks>
    [Description("Partial Sales Cancelled")]
    PartialSalesCancelled = 17,

    /// <summary>
    /// Деактивация отменена — упаковка возвращена в оборот.
    /// </summary>
    /// <remarks>Числовое значение: <c>18</c>.</remarks>
    [Description("Deactivation Cancelled")]
    DeactivationCancelled = 18,
}
