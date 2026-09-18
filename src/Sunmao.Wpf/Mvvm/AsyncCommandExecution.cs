namespace Sunmao.Wpf.Mvvm;

/// <summary>
/// One invocation of an asynchronous command: its cancellation source and its completion signal.
/// </summary>
/// <remarks>
/// Cancellation and completion can race. State 0 = active, 1 = cancellation callback in flight,
/// 2 = cancellation finished, 3 = completion requested. The 1 → 3 transition hands the final dispose
/// to the cancelling caller, so completion never disposes the source while <c>Cancel</c> runs.
/// </remarks>
internal sealed class AsyncCommandExecution
{
    private readonly CancellationTokenSource _cancellation = new();
    private readonly TaskCompletionSource _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private int _state;

    public CancellationToken Token => _cancellation.Token;

    public Task Completion => _completion.Task;

    public void RequestCancellation()
    {
        if (Interlocked.CompareExchange(ref _state, 1, 0) != 0)
        {
            return;
        }

        try
        {
            _cancellation.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Completion won the final race and disposed the source; cancellation is satisfied.
            return;
        }
        finally
        {
            if (Interlocked.CompareExchange(ref _state, 2, 1) == 3)
            {
                FinishCompletion();
            }
        }
    }

    public void Complete()
    {
        while (true)
        {
            switch (Volatile.Read(ref _state))
            {
                case 0:
                    if (Interlocked.CompareExchange(ref _state, 3, 0) == 0)
                    {
                        FinishCompletion();
                        return;
                    }

                    continue;
                case 1:
                    if (Interlocked.CompareExchange(ref _state, 3, 1) == 1)
                    {
                        // RequestCancellation's finally block disposes and signals.
                        return;
                    }

                    continue;
                case 2:
                    if (Interlocked.CompareExchange(ref _state, 3, 2) == 2)
                    {
                        FinishCompletion();
                        return;
                    }

                    continue;
                default:
                    return;
            }
        }
    }

    private void FinishCompletion()
    {
        _cancellation.Dispose();
        _completion.TrySetResult();
    }
}
