using System.Windows.Input;

namespace Sunmao.Wpf.Mvvm;

/// <summary>
/// <see cref="ICommand"/> for asynchronous UI actions that blocks re-entry while running and supports
/// owner-driven cancellation.
/// </summary>
/// <remarks>
/// <para>
/// While an invocation runs, <see cref="CanExecute"/> returns false and further executions are
/// ignored, so a double click cannot start long work twice.
/// </para>
/// <para>
/// <see cref="Execute"/> is <c>async void</c> because <see cref="ICommand"/> requires it; nothing
/// escapes it. Failures go to the <c>onError</c> callback, and cancellation through the command's
/// token is treated as a normal end.
/// </para>
/// <para>
/// When a page is deactivated, its owner calls <see cref="CancelAndWaitAsync"/> to cancel the current
/// invocation and wait for its cleanup without blocking the UI thread.
/// </para>
/// </remarks>
public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<CancellationToken, Task> _executeAsync;
    private readonly Func<bool>? _canExecute;
    private readonly AsyncCommandCore _core;

    /// <summary>Creates the command.</summary>
    /// <param name="executeAsync">Work to run; observe the token.</param>
    /// <param name="canExecute">Optional availability predicate.</param>
    /// <param name="onError">Receives failures, including failing <see cref="CanExecuteChanged"/> handlers.</param>
    public AsyncRelayCommand(
        Func<CancellationToken, Task> executeAsync,
        Func<bool>? canExecute = null,
        Action<Exception>? onError = null)
    {
        _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
        _canExecute = canExecute;
        _core = new AsyncCommandCore(onError, () => CanExecuteChanged?.Invoke(this, EventArgs.Empty));
    }

    /// <inheritdoc />
    public event EventHandler? CanExecuteChanged;

    /// <summary>True while an invocation is running.</summary>
    public bool IsExecuting => _core.IsExecuting;

    /// <inheritdoc />
    public bool CanExecute(object? parameter) =>
        !_core.IsExecuting && (_canExecute?.Invoke() ?? true);

    /// <inheritdoc />
    public async void Execute(object? parameter) => await ExecuteAsync().ConfigureAwait(true);

    /// <summary>Runs once; ignored while another invocation is active or when unavailable.</summary>
    /// <returns>A task that completes when this invocation, or the ignored call, is finished.</returns>
    public Task ExecuteAsync() =>
        _canExecute?.Invoke() ?? true
            ? _core.ExecuteAsync(_executeAsync)
            : Task.CompletedTask;

    /// <summary>Requests cancellation of the current invocation, if any.</summary>
    public void Cancel() => _core.Cancel();

    /// <summary>
    /// Cancels the invocation current at call time and waits until its cleanup, including the final
    /// <see cref="CanExecuteChanged"/> notification, has finished. Must not be awaited by the running
    /// invocation itself.
    /// </summary>
    public Task CancelAndWaitAsync() => _core.CancelAndWaitAsync();

    /// <summary>Asks bound controls to re-evaluate <see cref="CanExecute"/>.</summary>
    public void RaiseCanExecuteChanged() => _core.RaiseCanExecuteChanged();
}

/// <summary>Typed variant of <see cref="AsyncRelayCommand"/>.</summary>
/// <typeparam name="T">Parameter type. A parameter of another type makes the command unavailable.</typeparam>
public sealed class AsyncRelayCommand<T> : ICommand
{
    private readonly Func<T, CancellationToken, Task> _executeAsync;
    private readonly Func<T, bool>? _canExecute;
    private readonly AsyncCommandCore _core;

    /// <summary>Creates the command.</summary>
    /// <param name="executeAsync">Work to run; observe the token.</param>
    /// <param name="canExecute">Optional availability predicate.</param>
    /// <param name="onError">Receives failures, including failing <see cref="CanExecuteChanged"/> handlers.</param>
    public AsyncRelayCommand(
        Func<T, CancellationToken, Task> executeAsync,
        Func<T, bool>? canExecute = null,
        Action<Exception>? onError = null)
    {
        _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
        _canExecute = canExecute;
        _core = new AsyncCommandCore(onError, () => CanExecuteChanged?.Invoke(this, EventArgs.Empty));
    }

    /// <inheritdoc />
    public event EventHandler? CanExecuteChanged;

    /// <summary>True while an invocation is running.</summary>
    public bool IsExecuting => _core.IsExecuting;

    /// <inheritdoc />
    public bool CanExecute(object? parameter) =>
        CommandParameter.TryConvert<T>(parameter, out var typed) &&
        !_core.IsExecuting &&
        (_canExecute?.Invoke(typed) ?? true);

    /// <inheritdoc />
    public async void Execute(object? parameter)
    {
        if (CommandParameter.TryConvert<T>(parameter, out var typed))
        {
            await ExecuteAsync(typed).ConfigureAwait(true);
        }
    }

    /// <summary>Runs once with <paramref name="parameter"/>; ignored while another invocation is active or when unavailable.</summary>
    /// <param name="parameter">Command parameter.</param>
    public Task ExecuteAsync(T parameter) =>
        _canExecute?.Invoke(parameter) ?? true
            ? _core.ExecuteAsync(token => _executeAsync(parameter, token))
            : Task.CompletedTask;

    /// <summary>Requests cancellation of the current invocation, if any.</summary>
    public void Cancel() => _core.Cancel();

    /// <summary>Cancels the current invocation and waits for its cleanup; see <see cref="AsyncRelayCommand.CancelAndWaitAsync"/>.</summary>
    public Task CancelAndWaitAsync() => _core.CancelAndWaitAsync();

    /// <summary>Asks bound controls to re-evaluate <see cref="CanExecute"/>.</summary>
    public void RaiseCanExecuteChanged() => _core.RaiseCanExecuteChanged();
}

/// <summary>Execution state shared by both asynchronous command types.</summary>
internal sealed class AsyncCommandCore(Action<Exception>? onError, Action raiseCanExecuteChanged)
{
    private AsyncCommandExecution? _execution;

    public bool IsExecuting => Volatile.Read(ref _execution) is not null;

    public Task ExecuteAsync(Func<CancellationToken, Task> body)
    {
        var execution = new AsyncCommandExecution();
        if (Interlocked.CompareExchange(ref _execution, execution, null) is not null)
        {
            execution.Complete();
            return Task.CompletedTask;
        }

        RaiseCanExecuteChanged();
        return RunAsync(execution, body);
    }

    public void Cancel()
    {
        var execution = Volatile.Read(ref _execution);
        if (execution is not null)
        {
            RequestCancellation(execution);
        }
    }

    public async Task CancelAndWaitAsync()
    {
        var execution = Volatile.Read(ref _execution);
        if (execution is null)
        {
            return;
        }

        RequestCancellation(execution);
        await execution.Completion.ConfigureAwait(false);
    }

    public void RaiseCanExecuteChanged()
    {
        try
        {
            raiseCanExecuteChanged();
        }
        catch (Exception exception)
        {
            ReportError(exception);
        }
    }

    private async Task RunAsync(AsyncCommandExecution execution, Func<CancellationToken, Task> body)
    {
        try
        {
            await body(execution.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (execution.Token.IsCancellationRequested)
        {
            // Expected: the owner or operator cancelled.
        }
        catch (Exception exception)
        {
            // Must not escape: an unhandled exception from async void terminates the process.
            ReportError(exception);
        }
        finally
        {
            Interlocked.CompareExchange(ref _execution, null, execution);

            // The owner's drain includes this final notification, so a page may release its binding
            // resources as soon as CancelAndWaitAsync completes.
            RaiseCanExecuteChanged();
            execution.Complete();
        }
    }

    private void RequestCancellation(AsyncCommandExecution execution)
    {
        try
        {
            execution.RequestCancellation();
        }
        catch (Exception exception)
        {
            // A user cancellation callback can throw. Report it, but still let the drain finish.
            ReportError(exception);
        }
    }

    private void ReportError(Exception exception)
    {
        try
        {
            onError?.Invoke(exception);
        }
        catch
        {
            // The error handler must not turn a handled failure into a process-terminating one.
        }
    }
}
