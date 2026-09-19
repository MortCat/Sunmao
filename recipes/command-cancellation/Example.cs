using Sunmao.Wpf.Mvvm;

namespace Sunmao.Recipes;

// UI-thread-owned. The delegate must observe cancellation and complete its cleanup.
internal sealed class CommandExample : IAsyncDisposable
{
    private bool _closing;
    /// <summary>UI-thread command exposed for WPF binding; closing disables further execution.</summary>
    public AsyncRelayCommand Command { get; }

    internal CommandExample(Func<CancellationToken, Task> operation, Action<Exception> onError)
    {
        Command = new AsyncRelayCommand(operation, () => !_closing, onError);
    }

    /// <summary>On the UI thread, disable new work, cancel the current invocation and await its cleanup.</summary>
    public async ValueTask DisposeAsync()
    {
        _closing = true;
        Command.RaiseCanExecuteChanged();
        await Command.CancelAndWaitAsync();
    }
}
