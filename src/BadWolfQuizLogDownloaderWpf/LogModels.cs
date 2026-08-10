using System.Windows.Media;

namespace BadWolfQuizLogDownloaderWpf;

internal enum LogLevel
{
    Trace,
    Debug,
    Information,
    Warning,
    Error,
    Critical,
    Other
}

internal sealed record LogEntry(LogLevel Level, string Text);

internal sealed class LogEntryView
{
    public required LogLevel Level { get; init; }
    public required string Text { get; init; }
    public required Brush Brush { get; init; }
}
