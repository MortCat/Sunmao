using Sunmao.Core.Logging;

namespace Sunmao.Core.Polling;

/// <summary>
/// Base class for every background task that repeats on an interval.
/// </summary>
/// <remarks>
/// <para>
/// Derive a <c>private sealed</c> class and hold it as a private member of its owner (a Component
/// or Service); the owner starts and stops it from its own lifecycle. Pollers are never public and
/// never appear in a public inheritance chain.
/// </para>
/// <para>
/// The loop has one owner and one awaitable task. A poll can return <see cref="PollingResult.Stop"/>
/// to terminate itself; it must never call <see cref="StopAsync"/> from inside the loop because that
/// would make the loop await itself. An external <see cref="StopAsync"/> cancels and awaits the loop.
/// </para>
/// <para>
/// Iteration time is measured with <see cref="TimeProvider"/> and subtracted from the next delay,
/// so the interval is a period rather than an iteration-plus-delay gap. A changed interval
/// (<see cref="PollingResult.ContinueAfter"/>, <see cref="PollingErrorDecision.RetryAfterDelay"/>,
/// <see cref="SetInterval"/>) stays in effect until changed again.
/// </para>
/// <para>
/// By default the first poll runs as soon as the loop starts. Pass <c>delayFirstPoll: true</c> to
/// wait one interval first; do not delay inside <see cref="PollAsync"/>, because that delay would
/// count as the first iteration's work and the second poll would run immediately after it.
/// </para>
/// </remarks>
public abstract class PollingTaskBase : IAsyncDisposable
{
    private static readonly TimeSpan DefaultInterval = TimeSpan.FromMilliseconds(200);

    private readonly ILogSink _log;
    private readonly TimeProvider _timeProvider;
    private readonly bool _delayFirstPoll;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly TaskCompletionSource<bool> _disposeCompletion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private CancellationTokenSource? _cancellation;
    private Task? _loop;
    private long _intervalTicks;
    private int _disposeStarted;
    private bool _disposed;

    /// <summary>Creates a stopped poller.</summary>
    /// <param name="taskName">Name used as the log category.</param>
    /// <param name="logSink">Receives poll and hook failures; defaults to <see cref="NullLogSink"/>.</param>
    /// <param name="interval">Period between poll starts; defaults to 200 ms.</param>
    /// <param name="timeProvider">Time source for delays and iteration timing; defaults to the system.</param>
    /// <param name="delayFirstPoll">True to wait one interval before the first poll after each start.</param>
    /// <exception cref="ArgumentException"><paramref name="taskName"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="interval"/> is not positive.</exception>
    protected PollingTaskBase(
        string taskName,
        ILogSink? logSink = null,
        TimeSpan? interval = null,
        TimeProvider? timeProvider = null,
        bool delayFirstPoll = false)
    {
        if (string.IsNullOrWhiteSpace(taskName))
        {
            throw new ArgumentException("Task name is required.", nameof(taskName));
        }

        TaskName = taskName;
        _log = logSink ?? NullLogSink.Instance;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _delayFirstPoll = delayFirstPoll;
        SetInterval(interval ?? DefaultInterval);
    }

    /// <summary>Name used as the log category.</summary>
    public string TaskName { get; }

    /// <summary>True while the loop task has not completed.</summary>
    public bool IsRunning => _loop is { IsCompleted: false };

    /// <summary>Current period between poll starts.</summary>
    public TimeSpan Interval => TimeSpan.FromTicks(Interlocked.Read(ref _intervalTicks));

    /// <summary>The log sink supplied to the constructor.</summary>
    protected ILogSink Log => _log;

    /// <summary>The time source supplied to the constructor.</summary>
    protected TimeProvider TimeProvider => _timeProvider;

    /// <summary>
    /// Starts the loop after <see cref="OnStartingAsync"/> has completed. Calling it while running is
    /// harmless. A stopped poller can be started again.
    /// </summary>
    /// <param name="cancellationToken">Cancels waiting for the lifecycle gate and the start hook.</param>
    /// <exception cref="ObjectDisposedException">The poller is disposed.</exception>
    public virtual async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            if (IsRunning)
            {
                return;
            }

            CleanupCompletedLoop();
            await OnStartingAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            var cancellation = new CancellationTokenSource();
            _cancellation = cancellation;
            _loop = Task.Run(
                () => RunAsync(cancellation.Token),
                CancellationToken.None);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    /// <summary>
    /// Cancels the loop and waits for it, including <see cref="OnStoppingAsync"/>, to finish. Calling
    /// it more than once is harmless. Never call it from inside <see cref="PollAsync"/>.
    /// </summary>
    public virtual async Task StopAsync()
    {
        await _lifecycleGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await StopLockedAsync().ConfigureAwait(false);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    /// <summary>Stops the loop and prevents further starts. Concurrent callers share one disposal.</summary>
    public virtual async ValueTask DisposeAsync()
    {
        if (Interlocked.CompareExchange(ref _disposeStarted, 1, 0) == 0)
        {
            try
            {
                await _lifecycleGate.WaitAsync().ConfigureAwait(false);
                try
                {
                    _disposed = true;
                    await StopLockedAsync().ConfigureAwait(false);
                }
                finally
                {
                    _lifecycleGate.Release();
                }

                _lifecycleGate.Dispose();
                _disposeCompletion.TrySetResult(true);
            }
            catch (Exception exception)
            {
                _disposeCompletion.TrySetException(exception);
                throw;
            }
        }
        else
        {
            await _disposeCompletion.Task.ConfigureAwait(false);
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// One bounded iteration. Pass the token to every awaited call. Return
    /// <see cref="PollingResult.Stop"/> to end the loop without a deadlock.
    /// </summary>
    /// <param name="cancellationToken">Cancelled when the owner stops or disposes the poller.</param>
    protected abstract ValueTask<PollingResult> PollAsync(CancellationToken cancellationToken);

    /// <summary>Runs before the loop is created. An exception prevents the start.</summary>
    /// <param name="cancellationToken">Token passed to <see cref="StartAsync"/>.</param>
    protected virtual Task OnStartingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Runs once after the loop exits, whether it stopped normally, by cancellation or by error.
    /// Exceptions are logged and do not fault the loop task.
    /// </summary>
    /// <param name="cancellationToken">
    /// Cancelled when the owner stopped the loop (<see cref="StopAsync"/> or <see cref="DisposeAsync"/>);
    /// not cancelled when a poll returned Stop or the error policy stopped it. Use this to tell an
    /// external stop from a self-stop, for example to keep a Faulted status after a self-stop.
    /// </param>
    protected virtual Task OnStoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Called when <see cref="PollAsync"/> throws. The exception has already been logged. Return a
    /// retry delay for an explicit backoff (it stays in effect until changed), or Stop to end the loop.
    /// </summary>
    /// <param name="exception">The exception thrown by the poll.</param>
    /// <param name="cancellationToken">The loop's cancellation token.</param>
    protected virtual ValueTask<PollingErrorDecision> OnPollingErrorAsync(
        Exception exception,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(PollingErrorDecision.Continue);

    /// <summary>Changes the period; the new value stays in effect until changed again.</summary>
    /// <param name="interval">New period; must be positive.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="interval"/> is not positive.</exception>
    protected void SetInterval(TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(interval),
                interval,
                "Polling interval must be greater than zero.");
        }

        Interlocked.Exchange(ref _intervalTicks, interval.Ticks);
    }

    private async Task StopLockedAsync()
    {
        var cancellation = _cancellation;
        var loop = _loop;

        if (cancellation is null || loop is null)
        {
            CleanupCompletedLoop();
            return;
        }

        try
        {
            // Signal cancellation synchronously before awaiting the loop. This makes the stop boundary
            // deterministic and stops a poll that awaits an external task from winning a race with an
            // asynchronous cancellation dispatch.
            cancellation.Cancel();
            await loop.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected: the loop observed the cancellation request.
        }
        finally
        {
            if (ReferenceEquals(_cancellation, cancellation))
            {
                _cancellation = null;
            }

            if (ReferenceEquals(_loop, loop))
            {
                _loop = null;
            }

            cancellation.Dispose();
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_delayFirstPoll && !await DelayAsync(Interval, cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                var startedAt = _timeProvider.GetTimestamp();
                PollingResult result;

                try
                {
                    result = await PollAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    _log.Write(LogLevel.Error, TaskName, "Polling iteration failed.", exception);

                    PollingErrorDecision errorDecision;
                    try
                    {
                        errorDecision = await OnPollingErrorAsync(exception, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception handlerException)
                    {
                        _log.Write(
                            LogLevel.Error,
                            TaskName,
                            "Polling error handler failed; continuing with the normal cadence.",
                            handlerException);
                        errorDecision = PollingErrorDecision.Continue;
                    }

                    if (errorDecision.Decision == PollingDecision.Stop)
                    {
                        break;
                    }

                    if (errorDecision.RetryAfter.HasValue)
                    {
                        SetInterval(errorDecision.RetryAfter.Value);
                    }

                    await DelayUntilNextIterationAsync(startedAt, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (result.NextInterval.HasValue)
                {
                    SetInterval(result.NextInterval.Value);
                }

                if (result.Decision == PollingDecision.Stop)
                {
                    break;
                }

                await DelayUntilNextIterationAsync(startedAt, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            try
            {
                await OnStoppingAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Shutdown cancellation is expected.
            }
            catch (Exception exception)
            {
                _log.Write(LogLevel.Error, TaskName, "Polling shutdown hook failed.", exception);
            }
        }
    }

    private Task DelayUntilNextIterationAsync(long startedAt, CancellationToken cancellationToken)
    {
        var remaining = Interval - _timeProvider.GetElapsedTime(startedAt);
        return DelayAsync(remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining, cancellationToken);
    }

    /// <summary>Returns false when the delay was cancelled by the loop's token.</summary>
    private async Task<bool> DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, _timeProvider, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected during StopAsync or disposal.
            return false;
        }
    }

    private void CleanupCompletedLoop()
    {
        if (_loop is null || !_loop.IsCompleted)
        {
            return;
        }

        _loop = null;
        _cancellation?.Dispose();
        _cancellation = null;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(GetType().Name);
        }
    }
}
