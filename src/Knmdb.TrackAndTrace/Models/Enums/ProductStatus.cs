using System.ComponentModel;

namespace Knmdb.TrackAndTrace.Models.Enums;

/// <summary>
/// Складской статус упаковки лекарственного средства.
/// </summary>
/// <remarks>
/// <para>
/// Соответствует серверному перечислению <c>KNDDBModel.TTProductBox.StatusEnum</c>.
/// Возвращается в свойстве <c>productStatus</c> модели <c>ProductInquiryResult</c>
/// (методы <c>POST /api/TrackAndTrace/ProductInquiryQRCode</c> и <c>POST /api/TrackAndTrace/ProductInquiryGtinSn</c>).
/// </para>
/// <para>По сети передаётся числом; строковые представления также принимаются при чтении.</para>
/// </remarks>
public enum ProductStatus
{
    /// <summary>
    /// В наличии — упаковка находится на складе текущего держателя и не участвует в перемещении.
    /// </summary>
    /// <remarks>Числовое значение: <c>1</c>.</remarks>
    [Description("InStock")]
    InStock = 1,

    /// <summary>
    /// Выбыла — упаковка покинула склад (продана, экспортирована, деактивирована и т. п.).
    /// </summary>
    /// <remarks>Числовое значение: <c>2</c>.</remarks>
    [Description("StockOut")]
    StockOut = 2,

    /// <summary>
    /// В перемещении — упаковка находится в процессе передачи между стейкхолдерами.
    /// </summary>
    /// <remarks>Числовое значение: <c>3</c>.</remarks>
    [Description("InTransfer")]
    InTransfer = 3,
}
