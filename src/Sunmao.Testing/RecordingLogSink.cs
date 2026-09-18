using Sunmao.Core.Logging;

namespace Sunmao.Testing;

/// <summary>One entry captured by <see cref="RecordingLogSink"/>.</summary>
/// <param name="Level">Severity.</param>
/// <param name="Category">Log category.</param>
/// <param name="Message">Message text.</param>
/// <param name="Exception">Attached exception, if any.</param>
public sealed record LogEntry(LogLevel Level, string Category, string Message, Exception? Exception);

/// <summary>Thread-safe <see cref="ILogSink"/> that keeps every entry in memory for assertions.</summary>
public sealed class RecordingLogSink : ILogSink
{
    private readonly object _gate = new();
    private readonly List<LogEntry> _entries = [];

    /// <summary>Snapshot of every entry written so far, in order.</summary>
    public IReadOnlyList<LogEntry> Entries
    {
        get
        {
            lock (_gate)
            {
                return _entries.ToArray();
            }
        }
    }

    /// <summary>Snapshot of every message written so far, in order.</summary>
    public IReadOnlyList<string> Messages => Entries.Select(entry => entry.Message).ToArray();

    /// <inheritdoc />
    public void Write(LogLevel level, string category, string message, Exception? exception = null)
    {
        lock (_gate)
        {
            _entries.Add(new LogEntry(level, category, message, exception));
        }
    }
}
