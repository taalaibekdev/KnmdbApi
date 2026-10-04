using System.ComponentModel;

namespace Knmdb.TrackAndTrace.Models.Enums;

/// <summary>
/// Тип организации-участника оборота лекарственных средств (стейкхолдера).
/// </summary>
/// <remarks>
/// <para>
/// Соответствует серверному перечислению <c>KNDDBModel.TTStakeholder.StakeholderType</c>.
/// Возвращается в свойстве <c>type</c> модели <c>StakeholderInfo</c>
/// (метод <c>GET /api/TrackAndTrace/GetAllStakeholders</c>).
/// </para>
/// <para>По сети передаётся числом; строковые представления также принимаются при чтении.</para>
/// </remarks>
public enum StakeholderType
{
    /// <summary>
    /// Производитель — организация, выпускающая лекарственные средства.
    /// </summary>
    /// <remarks>Числовое значение: <c>1</c>.</remarks>
    [Description("Producer")]
    Producer = 1,

    /// <summary>
    /// Импортёр — организация, ввозящая лекарственные средства на территорию страны.
    /// </summary>
    /// <remarks>Числовое значение: <c>2</c>.</remarks>
    [Description("Importer")]
    Importer = 2,

    /// <summary>
    /// Компания — юридическое лицо общего профиля (дистрибьютор, представительство и т. п.).
    /// </summary>
    /// <remarks>Числовое значение: <c>3</c>.</remarks>
    [Description("Company")]
    Company = 3,

    /// <summary>
    /// Завод-изготовитель.
    /// </summary>
    /// <remarks>Числовое значение: <c>4</c>.</remarks>
    [Description("Manufacturer")]
    Manufacturer = 4,

    /// <summary>
    /// Аптека — организация розничной реализации.
    /// </summary>
    /// <remarks>Числовое значение: <c>5</c>.</remarks>
    [Description("Pharmacy")]
    Pharmacy = 5,

    /// <summary>
    /// Медицинская организация (больница, поликлиника и т. п.).
    /// </summary>
    /// <remarks>Числовое значение: <c>6</c>.</remarks>
    [Description("MedicalOrganization")]
    MedicalOrganization = 6,

    /// <summary>
    /// Склад — организация складского хранения и оптовой отгрузки.
    /// </summary>
    /// <remarks>Числовое значение: <c>7</c>.</remarks>
    [Description("Warehouse")]
    Warehouse = 7,
}
