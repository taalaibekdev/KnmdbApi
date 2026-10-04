using System.ComponentModel;

namespace Knmdb.TrackAndTrace.Models.Enums;

/// <summary>
/// Роль стейкхолдера в конкретной декларации перемещения: кто он — отправитель или получатель.
/// </summary>
/// <remarks>
/// <para>
/// Соответствует серверному перечислению <c>KNDDBModel.TTTransferDeclaration.TransferTypes</c>.
/// Возвращается в свойстве <c>transferType</c> модели <c>TransferDeclarationInfo</c>
/// и принимается в фильтре <c>GetTransferListByFilterRequest</c>.
/// </para>
/// <para>По сети передаётся числом; строковые представления также принимаются при чтении.</para>
/// </remarks>
public enum TransferType
{
    /// <summary>
    /// Инициатор — стейкхолдер выступает отправителем перемещения.
    /// </summary>
    /// <remarks>Числовое значение: <c>1</c>.</remarks>
    [Description("Initiate")]
    Initiate = 1,

    /// <summary>
    /// Получатель — стейкхолдер принимает перемещение.
    /// </summary>
    /// <remarks>Числовое значение: <c>2</c>.</remarks>
    [Description("Accept")]
    Accept = 2,
}
