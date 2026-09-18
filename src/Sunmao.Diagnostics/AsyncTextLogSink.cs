using System.Globalization;
using System.Text;
using System.Threading.Channels;
using Sunmao.Core.Logging;

namespace Sunmao.Diagnostics;

/// <summary>
/// Application-scoped, non-blocking diagnostic log sink that writes one text file per local day.
/// </summary>
/// <remarks>
/// <para>
/// Producers only build a bounded entry and call <see cref="ChannelWriter{T}.TryWrite"/>; they never
/// block or throw. Directory creation, file open, daily rotation and flushing belong to one reader
/// task. Under pressure, Debug and Info entries are dropped first (the last <c>criticalReserve</c>
/// slots are kept for Warning and Error); drops are counted and reported in the file once the writer
/// recovers.
/// </para>
/// <para>
/// The destination is chosen once with <see cref="TryConfigure(LogFileDestination)"/>, which can
/// happen after early startup entries have been queued. Diagnostic entries may be dropped under
/// pressure, so do not use this sink for records that must never be lost.
/// </para>
/// <code>
/// await using var log = new AsyncTextLogSink();
/// log.TryConfigure(new LogFileDestination(@"D:\Data\logs", "myapp"));
/// log.Write(LogLevel.Info, "startup", "Ready.");
/// </code>
/// </remarks>
public sealed class AsyncTextLogSink : ILogSink, IAsyncDisposable
{
    private const int DefaultCapacity = 4096;
    private const int DefaultCriticalReserve = 256;
    private const int DefaultMaxTextCharacters = 4096;
    private const int DefaultMaxExceptionCharacters = 8192;
    private const int BatchSize = 64;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(1);

    private readonly Channel<WorkItem> _channel;
    private readonly TaskCompletionSource<LogFileDestination> _destination =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TimeProvider _timeProvider;
    private readonly int _capacity;
    private readonly int _criticalReserve;
    private readonly int _maxTextCharacters;
    private readonly int _maxExceptionCharacters;
    private readonly Task _writerTask;
    private readonly TaskCompletionSource<bool> _disposeCompletion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private long _nextSequence;
    private long _queued;
    private long _accepted;
    private long _droppedDebug;
    private long _droppedInfo;
    private long _droppedWarning;
    private long _droppedError;
    private long _writerFailed;
    private long _rejectedAfterStop;
    private long _unreportedDropped;
    private long _unreportedWriterFailures;
    private long _failedThroughSequence;
    private long _lastFailureTicks;
    private int _accepting = 1;
    private int _destinationState;
    private int _lifecycleState = (int)AsyncLogState.WaitingForDestination;
    private int _disposeStarted;
    private string? _lastFailure;

    /// <summary>Creates the sink and its writer task; entries queue until a destination is configured.</summary>
    /// <param name="timeProvider">Time source for entry timestamps and daily rotation (local time); defaults to the system.</param>
    /// <param name="capacity">Maximum queued entries.</param>
    /// <param name="criticalReserve">Queue slots reserved for Warning and Error entries; must be below <paramref name="capacity"/>.</param>
    /// <param name="maxTextCharacters">Maximum characters kept from a category or message.</param>
    /// <param name="maxExceptionCharacters">Maximum characters kept from an exception message or stack trace.</param>
    /// <exception cref="ArgumentOutOfRangeException">A limit is out of range.</exception>
    public AsyncTextLogSink(
        TimeProvider? timeProvider = null,
        int capacity = DefaultCapacity,
        int criticalReserve = DefaultCriticalReserve,
        int maxTextCharacters = DefaultMaxTextCharacters,
        int maxExceptionCharacters = DefaultMaxExceptionCharacters)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        if (criticalReserve < 0 || criticalReserve >= capacity)
        {
            throw new ArgumentOutOfRangeException(nameof(criticalReserve));
        }

        if (maxTextCharacters <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxTextCharacters));
        }

        if (maxExceptionCharacters <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxExceptionCharacters));
        }

        _timeProvider = timeProvider ?? TimeProvider.System;
        _capacity = capacity;
        _criticalReserve = criticalReserve;
        _maxTextCharacters = maxTextCharacters;
        _maxExceptionCharacters = maxExceptionCharacters;
        _channel = Channel.CreateBounded<WorkItem>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });
        _writerTask = RunWriterAsync();
    }

    /// <summary>Whether the one-time file destination has been selected.</summary>
    public bool IsConfigured => Volatile.Read(ref _destinationState) == 1;

    /// <summary>Current health and pressure counters.</summary>
    public AsyncLogSnapshot Snapshot => new(
        (AsyncLogState)Volatile.Read(ref _lifecycleState),
        IsConfigured,
        Volatile.Read(ref _accepting) != 0,
        Interlocked.Read(ref _queued),
        Interlocked.Read(ref _accepted),
        Interlocked.Read(ref _droppedDebug),
        Interlocked.Read(ref _droppedInfo),
        Interlocked.Read(ref _droppedWarning),
        Interlocked.Read(ref _droppedError),
        Interlocked.Read(ref _writerFailed),
        Interlocked.Read(ref _rejectedAfterStop),
        Volatile.Read(ref _lastFailure),
        Interlocked.Read(ref _lastFailureTicks) == 0
            ? null
            : new DateTimeOffset(Interlocked.Read(ref _lastFailureTicks), TimeSpan.Zero));

    /// <summary>
    /// Selects the destination once. It bypasses the entry queue, so a flood of early entries can
    /// never prevent configuration.
    /// </summary>
    /// <param name="destination">Folder and file prefix.</param>
    /// <returns>False when a destination was already selected or the sink is stopping.</returns>
    public bool TryConfigure(LogFileDestination destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        if (Volatile.Read(ref _accepting) == 0 ||
            Interlocked.CompareExchange(ref _destinationState, 1, 0) != 0)
        {
            return false;
        }

        if (_destination.TrySetResult(destination))
        {
            return true;
        }

        // Disposal may have won between the accepting check and the one-time claim. Do not expose a
        // configured state when the destination task was cancelled.
        Interlocked.CompareExchange(ref _destinationState, 2, 1);
        return false;
    }

    /// <summary>Selects the destination once; see <see cref="TryConfigure(LogFileDestination)"/>.</summary>
    /// <param name="directory">Folder for the daily files.</param>
    /// <param name="filePrefix">File name prefix.</param>
    public bool TryConfigure(string directory, string filePrefix = "app") =>
        TryConfigure(new LogFileDestination(directory, filePrefix));

    /// <inheritdoc />
    public void Write(LogLevel level, string category, string message, Exception? exception = null)
    {
        var queueReserved = false;
        var published = false;
        try
        {
            if (Volatile.Read(ref _accepting) == 0)
            {
                Interlocked.Increment(ref _rejectedAfterStop);
                return;
            }

            if (ShouldDropForPressure(level))
            {
                IncrementDropped(level);
                return;
            }

            var sequence = Interlocked.Increment(ref _nextSequence);
            var entry = new LogEntry(
                sequence,
                _timeProvider.GetLocalNow(),
                level,
                Limit(category, _maxTextCharacters),
                Limit(message, _maxTextCharacters),
                Describe(exception, _maxExceptionCharacters));

            // Reserve before publishing. A fast reader may consume the item immediately after
            // TryWrite succeeds; incrementing afterwards would transiently undercount pressure.
            Interlocked.Increment(ref _queued);
            queueReserved = true;
            if (_channel.Writer.TryWrite(WorkItem.ForEntry(entry)))
            {
                published = true;
                Interlocked.Increment(ref _accepted);
                return;
            }

            Interlocked.Decrement(ref _queued);
            IncrementDropped(level);
        }
        catch (Exception error)
        {
            // The logging boundary must never become the failure that escapes the caller.
            if (queueReserved && !published)
            {
                Interlocked.Decrement(ref _queued);
            }

            Interlocked.Increment(ref _writerFailed);
            SetFailure(error, Volatile.Read(ref _nextSequence));
        }
    }

    /// <summary>
    /// Waits until every entry accepted before this call has been written or has failed.
    /// </summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>Success only when every earlier entry reached the file.</returns>
    public async Task<AsyncLogFlushResult> FlushAsync(CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _disposeStarted) != 0)
        {
            return AsyncLogFlushResult.Failed("The log sink is stopping or stopped.");
        }

        // Without a destination the writer cannot consume a barrier; report it instead of hanging.
        if (!IsConfigured)
        {
            return AsyncLogFlushResult.Failed("The log sink has no configured destination.");
        }

        var targetSequence = Volatile.Read(ref _nextSequence);
        var barrier = WorkItem.ForBarrier(targetSequence);
        var enqueued = false;

        try
        {
            Interlocked.Increment(ref _queued);
            await _channel.Writer.WriteAsync(barrier, cancellationToken).ConfigureAwait(false);
            enqueued = true;
            return await barrier.Completion!.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (!enqueued)
            {
                Interlocked.Decrement(ref _queued);
            }

            return AsyncLogFlushResult.Failed("Log flush was cancelled.");
        }
        catch (ChannelClosedException)
        {
            if (!enqueued)
            {
                Interlocked.Decrement(ref _queued);
            }

            return AsyncLogFlushResult.Failed("The log writer is closed.");
        }
        catch (Exception exception)
        {
            if (!enqueued)
            {
                Interlocked.Decrement(ref _queued);
            }

            SetFailure(exception, targetSequence);
            return AsyncLogFlushResult.Failed("The log flush failed before the barrier was accepted.");
        }
    }

    /// <summary>Stops accepting entries, drains the queue to the file and closes it.</summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.CompareExchange(ref _disposeStarted, 1, 0) == 0)
        {
            try
            {
                Volatile.Write(ref _accepting, 0);
                Volatile.Write(ref _lifecycleState, (int)AsyncLogState.Stopping);
                _destination.TrySetCanceled();
                _channel.Writer.TryComplete();
                await _writerTask.ConfigureAwait(false);
                Volatile.Write(ref _lifecycleState, (int)AsyncLogState.Stopped);
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

    private async Task RunWriterAsync()
    {
        DailyWriter? writer = null;
        LogFileDestination? destination = null;
        var batchCount = 0;

        try
        {
            try
            {
                destination = await _destination.Task.ConfigureAwait(false);
                Volatile.Write(ref _lifecycleState, (int)AsyncLogState.Ready);
            }
            catch (OperationCanceledException)
            {
                // Disposed before configuration: accepted entries are counted as writer failures
                // while the bounded queue is drained.
            }

            await foreach (var item in _channel.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                Interlocked.Decrement(ref _queued);

                if (item.BarrierTarget.HasValue)
                {
                    // A barrier succeeds only when no writer failure was observed at or before its
                    // target. The field stores the first failed sequence (zero means none), so a later
                    // failure cannot hide an earlier one.
                    var firstFailure = Volatile.Read(ref _failedThroughSequence);
                    var succeeded = firstFailure == 0 || firstFailure > item.BarrierTarget.Value;
                    try
                    {
                        writer?.Flush();
                    }
                    catch (Exception exception)
                    {
                        SetFailure(exception, item.BarrierTarget.Value);
                        succeeded = false;
                    }

                    item.Completion!.TrySetResult(
                        succeeded
                            ? AsyncLogFlushResult.Successful()
                            : AsyncLogFlushResult.Failed("One or more entries before the log barrier were not written."));
                    batchCount = 0;
                    continue;
                }

                var entry = item.Entry!;
                if (destination is null)
                {
                    Interlocked.Increment(ref _writerFailed);
                    Interlocked.Increment(ref _unreportedWriterFailures);
                    SetFailure(new InvalidOperationException("No log destination was configured."), entry.Sequence);
                    continue;
                }

                try
                {
                    writer ??= new DailyWriter(destination, () => _timeProvider.GetLocalNow(), SetFailure);
                    if (writer.Write(entry))
                    {
                        var droppedBeforeRecovery = Interlocked.Read(ref _unreportedDropped);
                        var writerFailuresBeforeRecovery = Interlocked.Read(ref _unreportedWriterFailures);
                        if ((droppedBeforeRecovery > 0 || writerFailuresBeforeRecovery > 0) &&
                            !writer.TryWriteRecoverySummary(
                                entry.OccurredAt,
                                droppedBeforeRecovery,
                                writerFailuresBeforeRecovery))
                        {
                            Interlocked.Increment(ref _writerFailed);
                            Interlocked.Increment(ref _unreportedWriterFailures);
                            SetFailure(
                                writer.LastError ?? new IOException("The log recovery summary could not be written."),
                                entry.Sequence);
                            writer.Dispose();
                            writer = null;
                            continue;
                        }

                        if (droppedBeforeRecovery > 0 || writerFailuresBeforeRecovery > 0)
                        {
                            Interlocked.Add(ref _unreportedDropped, -droppedBeforeRecovery);
                            Interlocked.Add(ref _unreportedWriterFailures, -writerFailuresBeforeRecovery);
                        }

                        batchCount++;
                        // Flush sparse traffic before waiting for more work; sustained traffic keeps
                        // the batch bound. No timer is needed.
                        if (batchCount >= BatchSize || !_channel.Reader.TryPeek(out _))
                        {
                            writer.Flush();
                            batchCount = 0;
                        }
                    }
                    else
                    {
                        Interlocked.Increment(ref _writerFailed);
                        Interlocked.Increment(ref _unreportedWriterFailures);
                        SetFailure(writer.LastError ?? new IOException("The log writer could not write the entry."), entry.Sequence);
                    }
                }
                catch (Exception exception)
                {
                    Interlocked.Increment(ref _writerFailed);
                    Interlocked.Increment(ref _unreportedWriterFailures);
                    SetFailure(exception, entry.Sequence);
                    writer?.Dispose();
                    writer = null;
                }
            }

            try
            {
                writer?.Flush();
            }
            catch (Exception exception)
            {
                SetFailure(exception, Volatile.Read(ref _nextSequence));
            }
        }
        catch (Exception exception)
        {
            SetFailure(exception, Volatile.Read(ref _nextSequence));
            DrainAfterWriterFailure(exception);
        }
        finally
        {
            writer?.Dispose();
            DrainUnfinishedBarriers();
        }
    }

    private void DrainAfterWriterFailure(Exception exception)
    {
        while (_channel.Reader.TryRead(out var item))
        {
            Interlocked.Decrement(ref _queued);
            if (item.Entry is not null)
            {
                Interlocked.Increment(ref _writerFailed);
                Interlocked.Increment(ref _unreportedWriterFailures);
                SetFailure(exception, item.Entry.Sequence);
            }
            else
            {
                item.Completion!.TrySetResult(AsyncLogFlushResult.Failed("The log writer stopped before the barrier."));
            }
        }
    }

    private void DrainUnfinishedBarriers()
    {
        while (_channel.Reader.TryRead(out var item))
        {
            Interlocked.Decrement(ref _queued);
            item.Completion?.TrySetResult(AsyncLogFlushResult.Failed("The log writer stopped before the barrier."));
        }
    }

    private bool ShouldDropForPressure(LogLevel level)
    {
        if (level is LogLevel.Warning or LogLevel.Error)
        {
            return false;
        }

        return Interlocked.Read(ref _queued) >= _capacity - _criticalReserve;
    }

    private void IncrementDropped(LogLevel level)
    {
        switch (level)
        {
            case LogLevel.Debug:
                Interlocked.Increment(ref _droppedDebug);
                break;
            case LogLevel.Info:
                Interlocked.Increment(ref _droppedInfo);
                break;
            case LogLevel.Warning:
                Interlocked.Increment(ref _droppedWarning);
                break;
            case LogLevel.Error:
                Interlocked.Increment(ref _droppedError);
                break;
        }

        Interlocked.Increment(ref _unreportedDropped);
    }

    private void SetFailure(Exception exception, long sequence)
    {
        string text;
        try
        {
            text = Limit($"{exception.GetType().FullName}: {exception.Message}", _maxExceptionCharacters);
        }
        catch
        {
            text = "<log writer failure details unavailable>";
        }

        Volatile.Write(ref _lastFailure, text);
        Interlocked.Exchange(ref _lastFailureTicks, _timeProvider.GetUtcNow().UtcTicks);
        Volatile.Write(ref _lifecycleState, (int)AsyncLogState.Degraded);

        if (sequence <= 0)
        {
            sequence = Volatile.Read(ref _nextSequence);
        }

        while (sequence > 0)
        {
            var current = Interlocked.Read(ref _failedThroughSequence);
            if (current != 0 && current <= sequence)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref _failedThroughSequence, sequence, current) == current)
            {
                return;
            }
        }
    }

    private static string Limit(string? value, int maximum)
    {
        var normalized = value?.Replace('\r', ' ').Replace('\n', ' ').Trim() ?? string.Empty;
        return normalized.Length <= maximum
            ? normalized
            : normalized[..maximum] + "…";
    }

    private static ExceptionDescriptor? Describe(Exception? exception, int maximum)
    {
        if (exception is null)
        {
            return null;
        }

        var descriptors = new List<ExceptionDescriptor>(capacity: 3);
        var current = exception;
        for (var depth = 0; current is not null && depth < 3; depth++)
        {
            string type;
            string message;
            string stack;
            Exception? inner;

            try
            {
                type = Limit(current.GetType().FullName, 256);
            }
            catch
            {
                type = "<unknown exception type>";
            }

            try
            {
                message = Limit(current.Message, maximum);
            }
            catch
            {
                message = "<exception message unavailable>";
            }

            try
            {
                stack = Limit(current.StackTrace, maximum);
            }
            catch
            {
                stack = string.Empty;
            }

            try
            {
                inner = current.InnerException;
            }
            catch
            {
                inner = null;
            }

            descriptors.Add(new ExceptionDescriptor(type, message, stack));
            current = inner;
        }

        return descriptors[0] with { Inner = descriptors.Skip(1).ToArray() };
    }

    private sealed record LogEntry(
        long Sequence,
        DateTimeOffset OccurredAt,
        LogLevel Level,
        string Category,
        string Message,
        ExceptionDescriptor? Exception);

    private sealed record ExceptionDescriptor(
        string Type,
        string Message,
        string Stack,
        IReadOnlyList<ExceptionDescriptor>? Inner = null);

    private sealed record WorkItem(
        LogEntry? Entry,
        long? BarrierTarget,
        TaskCompletionSource<AsyncLogFlushResult>? Completion)
    {
        public static WorkItem ForEntry(LogEntry entry) => new(entry, null, null);

        public static WorkItem ForBarrier(long target) => new(
            null,
            target,
            new TaskCompletionSource<AsyncLogFlushResult>(TaskCreationOptions.RunContinuationsAsynchronously));
    }

    private sealed class DailyWriter(
        LogFileDestination destination,
        Func<DateTimeOffset> clock,
        Action<Exception, long> onFailure) : IDisposable
    {
        private StreamWriter? _writer;
        private DateOnly? _date;
        private long _currentSequence;
        private DateTimeOffset _nextRetryAt;

        public Exception? LastError { get; private set; }

        public bool Write(LogEntry entry)
        {
            var date = DateOnly.FromDateTime(entry.OccurredAt.DateTime);
            if (_writer is null || _date != date)
            {
                CloseWriter();
                if (!TryOpen(date, entry.Sequence))
                {
                    return false;
                }
            }

            try
            {
                _writer!.WriteLine(FormatLine(entry));
                _currentSequence = entry.Sequence;
                LastError = null;
                return true;
            }
            catch (Exception exception)
            {
                LastError = exception;
                onFailure(exception, entry.Sequence);
                CloseWriter();
                return false;
            }
        }

        public bool TryWriteRecoverySummary(DateTimeOffset occurredAt, long droppedCount, long writerFailureCount)
        {
            if (_writer is null)
            {
                return false;
            }

            try
            {
                var summary = new StringBuilder()
                    .Append(occurredAt.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture))
                    .Append(" [WARNING] logging | Log writer recovered; ")
                    .Append(droppedCount + writerFailureCount)
                    .Append(" diagnostic message(s) were not persisted (ingress dropped=")
                    .Append(droppedCount)
                    .Append(", writer failed=")
                    .Append(writerFailureCount)
                    .Append("). Original text was not recovered.")
                    .ToString();
                _writer.WriteLine(summary);
                _writer.Flush();
                return true;
            }
            catch (Exception exception)
            {
                LastError = exception;
                onFailure(exception, _currentSequence);
                CloseWriter();
                return false;
            }
        }

        public void Flush() => _writer?.Flush();

        public void Dispose() => CloseWriter();

        private bool TryOpen(DateOnly date, long sequence)
        {
            var now = clock();
            if (now < _nextRetryAt)
            {
                return false;
            }

            var path = destination.FilePath(date);
            try
            {
                Directory.CreateDirectory(destination.Directory);
                var stream = new FileStream(
                    path,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.ReadWrite,
                    bufferSize: 16 * 1024,
                    options: FileOptions.SequentialScan);
                _writer = new StreamWriter(
                    stream,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                    bufferSize: 16 * 1024,
                    leaveOpen: false);
                _date = date;
                _nextRetryAt = DateTimeOffset.MinValue;
                LastError = null;
                return true;
            }
            catch (Exception exception)
            {
                LastError = exception;
                _nextRetryAt = now + RetryDelay;
                onFailure(exception, sequence);
                return false;
            }
        }

        private void CloseWriter()
        {
            try
            {
                _writer?.Dispose();
            }
            catch (Exception exception)
            {
                onFailure(exception, _currentSequence);
            }
            finally
            {
                _writer = null;
                _date = null;
            }
        }

        private static string FormatLine(LogEntry entry)
        {
            var builder = new StringBuilder()
                .Append(entry.OccurredAt.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture))
                .Append(" [")
                .Append(entry.Level.ToString().ToUpperInvariant())
                .Append("] ")
                .Append(entry.Category)
                .Append(" | ")
                .Append(entry.Message);

            AppendException(builder, entry.Exception, indent: "    ");
            return builder.ToString();
        }

        private static void AppendException(StringBuilder builder, ExceptionDescriptor? exception, string indent)
        {
            if (exception is null)
            {
                return;
            }

            builder.AppendLine()
                .Append(indent)
                .Append(exception.Type)
                .Append(": ")
                .Append(exception.Message);

            if (!string.IsNullOrWhiteSpace(exception.Stack))
            {
                builder.AppendLine()
                    .Append(indent)
                    .Append(exception.Stack);
            }

            if (exception.Inner is not null)
            {
                foreach (var inner in exception.Inner)
                {
                    AppendException(builder, inner, indent + "    ");
                }
            }
        }
    }
}

/// <summary>Lifecycle state of an <see cref="AsyncTextLogSink"/>.</summary>
public enum AsyncLogState
{
    /// <summary>Entries are queued; no destination has been configured yet.</summary>
    WaitingForDestination,

    /// <summary>Writing normally.</summary>
    Ready,

    /// <summary>At least one write failed; see <see cref="AsyncLogSnapshot.LastFailure"/>.</summary>
    Degraded,

    /// <summary>Disposal is draining the queue.</summary>
    Stopping,

    /// <summary>Disposed.</summary>
    Stopped
}

/// <summary>Health and pressure counters of an <see cref="AsyncTextLogSink"/>.</summary>
/// <param name="State">Lifecycle state.</param>
/// <param name="IsConfigured">Whether a destination has been selected.</param>
/// <param name="IsAccepting">Whether new entries are accepted.</param>
/// <param name="Queued">Entries and barriers currently queued.</param>
/// <param name="Accepted">Entries accepted into the queue since construction.</param>
/// <param name="DroppedDebug">Debug entries dropped under pressure.</param>
/// <param name="DroppedInfo">Info entries dropped under pressure.</param>
/// <param name="DroppedWarning">Warning entries dropped because the queue was full.</param>
/// <param name="DroppedError">Error entries dropped because the queue was full.</param>
/// <param name="WriterFailed">Entries or operations the writer failed to persist.</param>
/// <param name="RejectedAfterStop">Entries written after disposal began.</param>
/// <param name="LastFailure">Description of the latest failure.</param>
/// <param name="LastFailureAt">Time of the latest failure.</param>
public sealed record AsyncLogSnapshot(
    AsyncLogState State,
    bool IsConfigured,
    bool IsAccepting,
    long Queued,
    long Accepted,
    long DroppedDebug,
    long DroppedInfo,
    long DroppedWarning,
    long DroppedError,
    long WriterFailed,
    long RejectedAfterStop,
    string? LastFailure,
    DateTimeOffset? LastFailureAt);

/// <summary>Outcome of <see cref="AsyncTextLogSink.FlushAsync"/>.</summary>
/// <param name="Succeeded">True when every earlier entry reached the file.</param>
/// <param name="Failure">Reason when <paramref name="Succeeded"/> is false.</param>
public sealed record AsyncLogFlushResult(bool Succeeded, string? Failure)
{
    /// <summary>A successful result.</summary>
    public static AsyncLogFlushResult Successful() => new(true, null);

    /// <summary>A failed result with a reason.</summary>
    /// <param name="failure">Why the flush did not succeed.</param>
    public static AsyncLogFlushResult Failed(string failure) => new(false, failure);
}
