using System.Text;
using System.Text.RegularExpressions;
using System.IO;

namespace BadWolfQuizLogDownloaderWpf;

internal static class LogParser
{
    private static readonly Regex LogLevelRegex = new(
        @"\b(?<level>trce|dbug|info|warn|fail|crit):",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static IReadOnlyList<LogEntry> Parse(
        string logs,
        CancellationToken cancellationToken)
    {
        using var reader = new StringReader(logs);

        var entries = new List<LogEntry>();
        StringBuilder? current = null;
        var currentLevel = LogLevel.Other;
        var lineNumber = 0;

        while (reader.ReadLine() is { } line)
        {
            if ((lineNumber++ & 1023) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var detectedLevel = DetectLevel(line);

            if (detectedLevel is not null)
            {
                if (current is not null)
                {
                    entries.Add(new LogEntry(currentLevel, current.ToString()));
                }

                current = new StringBuilder(line);
                currentLevel = detectedLevel.Value;
                continue;
            }

            if (current is null)
            {
                current = new StringBuilder(line);
                currentLevel = LogLevel.Other;
            }
            else
            {
                current.AppendLine();
                current.Append(line);
            }
        }

        if (current is not null)
        {
            entries.Add(new LogEntry(currentLevel, current.ToString()));
        }

        return entries;
    }

    public static LogLevel? DetectLevel(string line)
    {
        var match = LogLevelRegex.Match(line);
        if (!match.Success)
        {
            return null;
        }

        return match.Groups["level"].Value.ToLowerInvariant() switch
        {
            "trce" => LogLevel.Trace,
            "dbug" => LogLevel.Debug,
            "info" => LogLevel.Information,
            "warn" => LogLevel.Warning,
            "fail" => LogLevel.Error,
            "crit" => LogLevel.Critical,
            _ => LogLevel.Other
        };
    }
}
