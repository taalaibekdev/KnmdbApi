namespace Knmdb.TrackAndTrace;

/// <summary>
/// Хранит, от имени какого пользователя выполняются запросы SDK.
/// </summary>
/// <remarks>
/// <para>
/// Значение привязано к контексту асинхронного вызова, поэтому:
/// </para>
/// <list type="bullet">
///   <item><description>в серверном приложении параллельные запросы разных пользователей не мешают друг другу;</description></item>
///   <item><description>в мобильном и desktop-приложении текущий пользователь — один на приложение,
///   пока не выполнено переключение.</description></item>
/// </list>
/// <para>
/// Значение, установленное внутри метода, не «протекает» наружу — это стандартное поведение
/// <see cref="AsyncLocal{T}"/>. Поэтому переключение пользователя выполняется либо методом
/// <c>SignInAsync</c>/<c>UseSessionAsync</c> на самом клиенте, либо через область
/// <see cref="KnddbApiClient.BeginUserScope(string)"/>.
/// </para>
/// </remarks>
internal sealed class KnddbCurrentUser
{
    private readonly AsyncLocal<KnddbSession?> _current = new();

    /// <summary>Текущая сессия или <see langword="null"/>.</summary>
    public KnddbSession? Current
    {
        get => _current.Value;
        set => _current.Value = value;
    }
}

/// <summary>
/// Область выполнения запросов от имени определённого пользователя.
/// </summary>
/// <remarks>
/// <para>
/// Запоминает текущего пользователя, переключает SDK на указанного и возвращает прежнего
/// при освобождении. Поддерживает и <c>await using</c>, и обычный <c>using</c>.
/// </para>
/// <code>
/// await using (client.BeginUserScope("user:42"))
/// {
///     var stock = await client.GetStockInheldListAsync();
/// }
/// </code>
/// </remarks>
public sealed class KnddbUserScope : IDisposable, IAsyncDisposable
{
    private readonly KnddbCurrentUser _current;
    private KnddbSession? _previous;
    private bool _disposed;

    /// <summary>
    /// Создаёт область и запоминает текущего пользователя, чтобы вернуть его при освобождении.
    /// </summary>
    /// <param name="current">Хранилище текущего пользователя.</param>
    /// <param name="session">Сессия, от имени которой выполняются запросы внутри области.</param>
    /// <remarks>
    /// Прежний пользователь запоминается <b>внутри</b> конструктора — до переключения.
    /// </remarks>
    internal KnddbUserScope(KnddbCurrentUser current, KnddbSession session)
    {
        _current = current ?? throw new ArgumentNullException(nameof(current));
        Session = session ?? throw new ArgumentNullException(nameof(session));

        _previous = current.Current;
        current.Current = session;
    }

    /// <summary>Пользователь, от имени которого выполняются запросы внутри области.</summary>
    public KnddbSession Session { get; }

    /// <summary>Ключ сессии области.</summary>
    public string SessionKey => Session.Key;

    /// <summary>Логин пользователя области.</summary>
    public string? UserName => Session.UserName;

    /// <summary>Восстанавливает прежнего пользователя.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _current.Current = _previous;
        _previous = null;
    }

    /// <summary>Восстанавливает прежнего пользователя.</summary>
    /// <returns>Завершённая задача.</returns>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
