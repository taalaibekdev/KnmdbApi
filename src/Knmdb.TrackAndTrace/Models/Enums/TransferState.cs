using System.ComponentModel;

namespace Knmdb.TrackAndTrace.Models.Enums;

/// <summary>
/// Состояние перемещения (передачи) упаковок между стейкхолдерами.
/// </summary>
/// <remarks>
/// <para>
/// Соответствует серверному перечислению <c>KNDDBModel.TTTransferDeclaration.TransferStates</c>.
/// Возвращается в свойствах <c>currentState</c> моделей <c>GetTransferDeclarationResponse</c>
/// и <c>TransferDeclarationInfo</c>, а также принимается в фильтре
/// <c>GetTransferListByFilterRequest</c>.
/// </para>
/// <para>По сети передаётся числом; строковые представления также принимаются при чтении.</para>
/// </remarks>
public enum TransferState
{
    /// <summary>
    /// Инициировано — отправитель создал декларацию перемещения, получатель ещё не подтвердил приём.
    /// </summary>
    /// <remarks>Числовое значение: <c>1</c>.</remarks>
    [Description("Initiated")]
    Initiated = 1,

    /// <summary>
    /// Принято — получатель подтвердил приём упаковок, право собственности перешло к нему.
    /// </summary>
    /// <remarks>Числовое значение: <c>2</c>.</remarks>
    [Description("Accepted")]
    Accepted = 2,

    /// <summary>
    /// Отменено — перемещение аннулировано отправителем до подтверждения приёма.
    /// </summary>
    /// <remarks>Числовое значение: <c>3</c>.</remarks>
    [Description("Cancelled")]
    Cancelled = 3,
}
