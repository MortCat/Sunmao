using Sunmao.Core.Logging;
using Sunmao.Core.Polling;
using Sunmao.Core.Resilience;

namespace Sunmao.Communication;

/// <summary>Connection state reported by <see cref="ManagedConnection"/>.</summary>
public enum ConnectionState
{
    /// <summary>Not started, or stopped.</summary>
    Stopped,

    /// <summary>Not connected; the next attempt is scheduled.</summary>
    Disconnected,

    /// <summary>A connect attempt is running.</summary>
    Connecting,

    /// <summary>Connected.</summary>
    Connected
}

/// <summary>Immutable connection status, safe to read from any thread and to show in a UI.</summary>
/// <param name="State">Current state.</param>
/// <param name="Endpoint">Transport address.</param>
/// <param name="ConsecutiveFailures">Failed attempts since the last successful connect.</param>
/// <param name="LastError">Latest failure message, cleared on connect.</param>
/// <param name="UpdatedAt">Time of the last change.</param>
public sealed record ConnectionSnapshot(
    ConnectionState State,
    string Endpoint,
    int ConsecutiveFailures,
    string? LastError,
    DateTimeOffset UpdatedAt)
{
    /// <summary>True when <see cref="State"/> is <see cref="ConnectionState.Connected"/>.</summary>
    public bool IsConnected => State == ConnectionState.Connected;
}

/// <summary>
/// Keeps a transport connected: a private supervisor poller reconnects it with exponential backoff
/// and logs repeated failures without flooding the log.
/// </summary>
/// <remarks>
/// <para>
/// The first attempt after a drop runs immediately; each failed attempt then waits
/// <c>retryInitialDelay</c>, doubling up to <c>retryMaximumDelay</c>. The first failure is logged at
/// once, repeats are summarized every <c>failureLogInterval</c>, and a recovery is logged once.
/// </para>
/// <para>
/// The connection owns the transport and disposes it. Use <see cref="Transport"/> (or a
/// <see cref="RequestResponseChannel"/> over it) for I/O; I/O failures mark the transport
/// disconnected and the supervisor reconnects it on its next check.
/// </para>
/// </remarks>
public sealed class ManagedConnection : IAsyncDisposable
{
    private readonly ILogSink _log;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _connectTimeout;
    private readonly ExponentialBackoff _backoff;
    private readonly ThrottledFailureLog _failureLog;
    private readonly Supervisor _supervisor;
    private ConnectionSnapshot _snapshot;

    /// <summary>Creates a stopped connection.</summary>
    /// <param name="transport">Transport to supervise; the connection takes ownership.</param>
    /// <param name="logSink">Receives connection failures and recoveries.</param>
    /// <param name="checkInterval">How often the connection is checked; defaults to 200 ms.</param>
    /// <param name="connectTimeout">Limit for one connect attempt; defaults to 3 seconds.</param>
    /// <param name="retryInitialDelay">Delay after the first failed attempt; defaults to 0.5 seconds.</param>
    /// <param name="retryMaximumDelay">Upper bound of the retry delay; defaults to 5 seconds.</param>
    /// <param name="failureLogInterval">Minimum time between failure summaries; defaults to 60 seconds.</param>
    /// <param name="timeProvider">Time source; defaults to the system.</param>
    public ManagedConnection(
        IByteTransport transport,
        ILogSink? logSink = null,
        TimeSpan? checkInterval = null,
        TimeSpan? connectTimeout = null,
        TimeSpan? retryInitialDelay = null,
        TimeSpan? retryMaximumDelay = null,
        TimeSpan? failureLogInterval = null,
        TimeProvider? timeProvider = null)
    {
        Transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _log = logSink ?? NullLogSink.Instance;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _connectTimeout = connectTimeout ?? TimeSpan.FromSeconds(3);
        if (_connectTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(connectTimeout));
        }

        _backoff = new ExponentialBackoff(retryInitialDelay, retryMaximumDelay, timeProvider: _timeProvider);
        _failureLog = new ThrottledFailureLog(_log, "connection", failureLogInterval, _timeProvider);
        _supervisor = new Supervisor(this, checkInterval ?? TimeSpan.FromMilliseconds(200));
        _snapshot = new ConnectionSnapshot(ConnectionState.Stopped, transport.Endpoint, 0, null, _timeProvider.GetUtcNow());
    }

    /// <summary>The supervised transport.</summary>
    public IByteTransport Transport { get; }

    /// <summary>Current status.</summary>
    public ConnectionSnapshot Snapshot => Volatile.Read(ref _snapshot);

    /// <summary>Raised on the supervisor thread when the status changes. Handlers must be short.</summary>
    public event EventHandler<ConnectionSnapshot>? StateChanged;

    /// <summary>Starts supervising; the first connect attempt runs immediately.</summary>
    /// <param name="cancellationToken">Cancels the start.</param>
    public Task StartAsync(CancellationToken cancellationToken = default) => _supervisor.StartAsync(cancellationToken);

    /// <summary>Stops supervising and disconnects the transport.</summary>
    public async Task StopAsync()
    {
        await _supervisor.StopAsync().ConfigureAwait(false);
        await Transport.DisconnectAsync().ConfigureAwait(false);
        _backoff.Reset();
        Publish(ConnectionState.Stopped, null);
    }

    /// <summary>Stops supervising and disposes the transport.</summary>
    public async ValueTask DisposeAsync()
    {
        await _supervisor.DisposeAsync().ConfigureAwait(false);
        await Transport.DisposeAsync().ConfigureAwait(false);
        Publish(ConnectionState.Stopped, null);
    }

    private async ValueTask<PollingResult> SuperviseAsync(CancellationToken cancellationToken)
    {
        var endpoint = Transport.Endpoint;
        if (Transport.IsConnected)
        {
            if (Snapshot.State != ConnectionState.Connected)
            {
                _backoff.Reset();
                _failureLog.RecordRecovery($"{endpoint} connected");
                Publish(ConnectionState.Connected, null);
            }

            return PollingResult.Continue;
        }

        if (Snapshot.State == ConnectionState.Connected)
        {
            // A drop is retried immediately; backoff only starts after an attempt fails.
            _failureLog.RecordFailure($"{endpoint} connection lost");
            Publish(ConnectionState.Disconnected, "Connection lost.");
        }

        if (!_backoff.IsAttemptDue)
        {
            return PollingResult.Continue;
        }

        Publish(ConnectionState.Connecting, Snapshot.LastError);
        Exception? failure = null;
        using (var timeout = new CancellationTokenSource(_connectTimeout, _timeProvider))
        using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token))
        {
            try
            {
                await Transport.ConnectAsync(linked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                failure = new TimeoutException($"Connecting to {endpoint} took longer than {_connectTimeout.TotalSeconds:0.#} s.");
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        }

        if (Transport.IsConnected)
        {
            _backoff.Reset();
            _failureLog.RecordRecovery($"{endpoint} connected");
            Publish(ConnectionState.Connected, null);
            return PollingResult.Continue;
        }

        _backoff.RecordFailure();
        _failureLog.RecordFailure($"{endpoint} is not connected; retrying with backoff up to {_backoff.MaximumDelay.TotalSeconds:0.#} s", failure);
        Publish(ConnectionState.Disconnected, failure?.Message ?? "Connect attempt did not connect.");
        return PollingResult.Continue;
    }

    private void Publish(ConnectionState state, string? error)
    {
        var current = Snapshot;
        var failures = state == ConnectionState.Connected ? 0 : _backoff.Failures;
        if (current.State == state && current.LastError == error && current.ConsecutiveFailures == failures)
        {
            return;
        }

        var next = new ConnectionSnapshot(state, Transport.Endpoint, failures, error, _timeProvider.GetUtcNow());
        Volatile.Write(ref _snapshot, next);
        try
        {
            StateChanged?.Invoke(this, next);
        }
        catch (Exception exception)
        {
            _log.Write(LogLevel.Warning, "connection", "A connection state handler failed.", exception);
        }
    }

    /// <summary>Private supervisor loop; the connection starts and stops it.</summary>
    private sealed class Supervisor(ManagedConnection owner, TimeSpan interval)
        : PollingTaskBase("connection-supervisor", owner._log, interval, owner._timeProvider)
    {
        protected override ValueTask<PollingResult> PollAsync(CancellationToken cancellationToken) =>
            owner.SuperviseAsync(cancellationToken);
    }
}
