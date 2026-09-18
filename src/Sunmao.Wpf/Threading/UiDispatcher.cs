using System.Windows.Threading;

namespace Sunmao.Wpf.Threading;

/// <summary>
/// The single crossing point from a background thread onto the WPF UI thread.
/// </summary>
/// <remarks>
/// <para>
/// Call <see cref="Initialize"/> once from the WPF application startup. Before that (during early
/// startup and in headless unit tests) every call runs inline, so nothing silently disappears.
/// </para>
/// <para>
/// View models should not need this for normal state refresh: they project backend snapshots on the
/// shared UI polling pulse, which already runs on the UI thread. Keep <see cref="Post"/> and
/// <see cref="Invoke"/> for isolated UI infrastructure.
/// </para>
/// </remarks>
public static class UiDispatcher
{
    private static Dispatcher? _dispatcher;

    /// <summary>Registers the dispatcher that owns the WPF view tree.</summary>
    /// <param name="dispatcher">Usually <c>Application.Current.Dispatcher</c>.</param>
    public static void Initialize(Dispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        Volatile.Write(ref _dispatcher, dispatcher);
    }

    /// <summary>True on the UI thread, or when no dispatcher has been registered.</summary>
    public static bool IsOnUiThread
    {
        get
        {
            var dispatcher = Volatile.Read(ref _dispatcher);
            return dispatcher is null || dispatcher.CheckAccess();
        }
    }

    /// <summary>
    /// Throws when called off the registered UI thread. Succeeds when no dispatcher is registered.
    /// </summary>
    /// <param name="operation">Name used in the exception message.</param>
    /// <exception cref="InvalidOperationException">Called from another thread.</exception>
    public static void VerifyAccess(string operation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        var dispatcher = Volatile.Read(ref _dispatcher);
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            throw new InvalidOperationException($"{operation} must run on the WPF UI dispatcher thread.");
        }
    }

    /// <summary>Queues <paramref name="action"/> on the UI thread and returns immediately.</summary>
    /// <param name="action">Short UI work.</param>
    public static void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var dispatcher = Volatile.Read(ref _dispatcher);
        if (dispatcher is null)
        {
            action();
            return;
        }

        dispatcher.BeginInvoke(action);
    }

    /// <summary>Runs <paramref name="action"/> on the UI thread and waits for it.</summary>
    /// <param name="action">Short UI work.</param>
    public static void Invoke(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var dispatcher = Volatile.Read(ref _dispatcher);
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.Invoke(action);
    }

    /// <summary>Clears the registered dispatcher. For tests only.</summary>
    internal static void Reset() => Volatile.Write(ref _dispatcher, null);
}
