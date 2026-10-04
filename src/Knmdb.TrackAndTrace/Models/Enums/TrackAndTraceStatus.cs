using System.ComponentModel;

namespace Knmdb.TrackAndTrace.Models.Enums;

/// <summary>
/// Статус обязательности маркировки лекарственного средства в системе Track and Trace.
/// </summary>
/// <remarks>
/// <para>
/// Соответствует серверному перечислению <c>KNDDBModel.Product.TrackAndTraceStatusEnum</c>.
/// Возвращается в свойстве <c>trackAndTraceStatus</c> модели <c>MedicineInfo</c>
/// (метод <c>POST /api/TrackAndTrace/GetMedicineList</c>).
/// </para>
/// <para>По сети передаётся числом; строковые представления также принимаются при чтении.</para>
/// </remarks>
public enum TrackAndTraceStatus
{
    /// <summary>
    /// Маркировка не требуется — препарат не подлежит прослеживаемости.
    /// </summary>
    /// <remarks>Человекочитаемое название: <c>Not Required</c>. Числовое значение: <c>1</c>.</remarks>
    [Description("Not Required")]
    NotTracked = 1,

    /// <summary>
    /// Маркировка обязательна — требуется нанесение этикетки (Data Matrix кода).
    /// </summary>
    /// <remarks>Человекочитаемое название: <c>Required</c>. Числовое значение: <c>2</c>.</remarks>
    [Description("Required")]
    LabelMandatory = 2,

    /// <summary>
    /// Обязательны и маркировка, и прослеживаемость — полный контроль движения упаковки
    /// от производителя или импортёра до конечного потребителя.
    /// </summary>
    /// <remarks>Человекочитаемое название: <c>Label And TraceMandatory</c>. Числовое значение: <c>3</c>.</remarks>
    [Description("Label And TraceMandatory")]
    LabelAndTraceMandatory = 3,
}
