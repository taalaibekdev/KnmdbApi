using System.ComponentModel;

namespace Knmdb.TrackAndTrace.Models.Enums;

/// <summary>
/// Тип расходной операции (тип списания) при деактивации упаковки.
/// </summary>
/// <remarks>
/// <para>
/// Соответствует серверному перечислению <c>KNDDBModel.TTConsumptionDeclaration.ConsumptionTypeEnum</c>.
/// </para>
/// <para>
/// Используется в:
/// <list type="bullet">
///   <item><description>свойстве <c>consumptionType</c> запроса <c>DeactivateDeclarationRequest</c> — тип списания при деактивации;</description></item>
///   <item><description>свойстве <c>consumptionType</c> ответа <c>ProductInquiryResult</c> — тип списания, по которому упаковка выбыла из оборота.</description></item>
/// </list>
/// </para>
/// <para>
/// Обратите внимание: значения идут с шагом 10, а не подряд. Это сделано для возможности
/// добавлять новые типы между существующими без изменения уже сохранённых в базе данных значений.
/// </para>
/// <para>
/// По сети передаётся числом (формат сервера). SDK дополнительно принимает при чтении
/// как имя члена (<c>DisposalForDeadline</c>), так и человекочитаемое название
/// (<c>DISPOSAL FOR DEADLINE</c>) из атрибута <see cref="DescriptionAttribute"/>.
/// </para>
/// </remarks>
public enum ConsumptionType
{
    /// <summary>
    /// Системное поступление — техническая операция ввода остатков в систему.
    /// </summary>
    /// <remarks>Числовое значение: <c>0</c>.</remarks>
    [Description("SYSTEM IN")]
    SystemIn = 0,

    /// <summary>
    /// Системное выбытие — техническая операция вывода остатков из системы.
    /// </summary>
    /// <remarks>Числовое значение: <c>10</c>.</remarks>
    [Description("SYSTEM OUT")]
    SystemOut = 10,

    /// <summary>
    /// Производственные потери — брак и отходы на этапе производства.
    /// </summary>
    /// <remarks>Числовое значение: <c>20</c>.</remarks>
    [Description("PRODUCTION WASTAGE")]
    ProductionWastage = 20,

    /// <summary>
    /// Уничтожение в связи с отзывом партии из обращения.
    /// </summary>
    /// <remarks>Числовое значение: <c>30</c>.</remarks>
    [Description("DISPOSAL DUE TO WITHDRAWAL")]
    DisposalDueToWithdrawal = 30,

    /// <summary>
    /// Уничтожение по истечении срока годности.
    /// </summary>
    /// <remarks>Числовое значение: <c>40</c>.</remarks>
    [Description("DISPOSAL FOR DEADLINE")]
    DisposalForDeadline = 40,

    /// <summary>
    /// Ревизия — корректировка остатков по результатам инвентаризации.
    /// </summary>
    /// <remarks>Числовое значение: <c>50</c>.</remarks>
    [Description("REVISION")]
    Revision = 50,

    /// <summary>
    /// Потребление (расход) — обычное выбытие в процессе использования.
    /// </summary>
    /// <remarks>Числовое значение: <c>60</c>.</remarks>
    [Description("CONSUMPTION")]
    Consumption = 60,

    /// <summary>
    /// Утеряно.
    /// </summary>
    /// <remarks>Числовое значение: <c>70</c>.</remarks>
    [Description("LOST")]
    Lost = 70,

    /// <summary>
    /// Повреждено.
    /// </summary>
    /// <remarks>Числовое значение: <c>80</c>.</remarks>
    [Description("DAMAGED")]
    Damaged = 80,

    /// <summary>
    /// Похищено.
    /// </summary>
    /// <remarks>Числовое значение: <c>90</c>.</remarks>
    [Description("STOLEN")]
    Stolen = 90,

    /// <summary>
    /// Образец — выдача препарата в качестве образца.
    /// </summary>
    /// <remarks>Числовое значение: <c>100</c>.</remarks>
    [Description("SAMPLE")]
    Sample = 100,
}
