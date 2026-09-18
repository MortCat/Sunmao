using System.Globalization;

namespace Sunmao.Diagnostics;

/// <summary>
/// Where <see cref="AsyncTextLogSink"/> writes: one UTF-8 file per local day, named
/// <c>{FilePrefix}-yyyy-MM-dd.log</c>, inside <see cref="Directory"/>. The directory is created on
/// first write.
/// </summary>
public sealed class LogFileDestination
{
    /// <summary>Creates a destination.</summary>
    /// <param name="directory">Folder for the daily files.</param>
    /// <param name="filePrefix">File name prefix; defaults to <c>app</c>.</param>
    /// <exception cref="ArgumentException">The directory is empty or the prefix is not a valid file name part.</exception>
    public LogFileDestination(string directory, string filePrefix = "app")
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException("A log directory is required.", nameof(directory));
        }

        if (string.IsNullOrWhiteSpace(filePrefix) || filePrefix.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("The file prefix must be a non-empty, valid file name part.", nameof(filePrefix));
        }

        Directory = Path.GetFullPath(directory);
        FilePrefix = filePrefix;
    }

    /// <summary>Absolute folder for the daily files.</summary>
    public string Directory { get; }

    /// <summary>File name prefix.</summary>
    public string FilePrefix { get; }

    /// <summary>Full path of the file for <paramref name="date"/>.</summary>
    /// <param name="date">Local calendar date of the entries.</param>
    public string FilePath(DateOnly date) =>
        Path.Combine(Directory, $"{FilePrefix}-{date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.log");
}
