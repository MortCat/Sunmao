namespace Sunmao.Core.Logging;

/// <summary>Severity of a diagnostic entry.</summary>
public enum LogLevel
{
    /// <summary>Detail that is only useful while investigating a problem.</summary>
    Debug,

    /// <summary>Normal operation worth recording, such as a recovery or a completed stage.</summary>
    Info,

    /// <summary>Something failed or degraded but the owner keeps working.</summary>
    Warning,

    /// <summary>An operation failed and needs attention.</summary>
    Error
}

/// <summary>
/// Destination for diagnostic output.
/// </summary>
/// <remarks>
/// Deliberately minimal so that base classes such as <c>PollingTaskBase</c> never decide the log
/// format or write files themselves. Implementations must be thread-safe, must not block the
/// caller on I/O and must never throw. <c>Sunmao.Diagnostics.AsyncTextLogSink</c> is the file
/// implementation.
/// </remarks>
public interface ILogSink
{
    /// <summary>Records one entry. Must be thread-safe, non-blocking and must not throw.</summary>
    /// <param name="level">Severity of the entry.</param>
    /// <param name="category">Short owner name, for example a component or task name.</param>
    /// <param name="message">Human-readable message.</param>
    /// <param name="exception">Optional exception whose details are recorded with the message.</param>
    void Write(LogLevel level, string category, string message, Exception? exception = null);
}

/// <summary>Discards every entry. Default when no sink is supplied.</summary>
public sealed class NullLogSink : ILogSink
{
    /// <summary>Shared instance.</summary>
    public static NullLogSink Instance { get; } = new();

    /// <inheritdoc />
    public void Write(LogLevel level, string category, string message, Exception? exception = null)
    {
    }
}
