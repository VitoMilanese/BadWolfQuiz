using Renci.SshNet;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace BadWolfQuizLogDownloaderWpf;

internal enum RemoteBackupType
{
    AppData,
    FullSite
}

internal sealed record RemoteBackupInfo(
    string FileName,
    string RemotePath,
    RemoteBackupType Type,
    DateTime? BackupTime,
    long SizeBytes)
{
    public string TypeDisplay => Type == RemoteBackupType.AppData ? "App_Data" : "Full site";

    public string BackupTimeDisplay =>
        BackupTime?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? "Unknown";

    public string SizeDisplay => FormatSize(SizeBytes);

    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = (double)Math.Max(0, bytes);
        var unit = 0;

        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0
            ? $"{value:0} {units[unit]}"
            : $"{value:0.##} {units[unit]}";
    }
}

internal sealed class RemoteBackupClient(AppSettings settings)
{
    private static readonly Regex AppDataPattern = new(
        "^badwolfquiz-app-data-(?<date>\\d{8})-(?<time>\\d{6})\\.tar\\.gz$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex FullSitePattern = new(
        "^badwolfquiz-service-folder-(?<date>\\d{8})-(?<time>\\d{6})\\.tar\\.gz$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public async Task<IReadOnlyList<RemoteBackupInfo>> ListAsync(
        CancellationToken cancellationToken)
    {
        EnsureBackupDirectoryConfigured();

        var directory = settings.RemoteBackupDirectory.Trim().TrimEnd('/');
        var command =
            $"test -d {ShellQuote(directory)} || exit 0; " +
            $"find {ShellQuote(directory)} -maxdepth 1 -type f " +
            "-name 'badwolfquiz-*.tar.gz' -printf '%f\\t%s\\n'";

        var output = await ExecuteCommandAsync(command, cancellationToken);
        var items = new List<RemoteBackupInfo>();

        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.TrimEnd('\r').Split('\t');
            if (parts.Length != 2 || !long.TryParse(parts[1], out var size))
            {
                continue;
            }

            if (!TryClassify(parts[0], out var type, out var backupTime))
            {
                continue;
            }

            items.Add(new RemoteBackupInfo(
                parts[0],
                directory + "/" + parts[0],
                type,
                backupTime,
                size));
        }

        return items
            .OrderByDescending(item => item.BackupTime)
            .ThenByDescending(item => item.FileName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task DownloadAsync(
        RemoteBackupInfo backup,
        string localPath,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        progress?.Report($"Downloading {backup.FileName}...");

        try
        {
            await Task.Run(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var client = CreateClient();
                using var cancellationRegistration =
                    cancellationToken.Register(() => SafeDispose(client));

                client.Connect();
                cancellationToken.ThrowIfCancellationRequested();

                using var command = client.CreateCommand(
                    BuildCommand($"test -f {ShellQuote(backup.RemotePath)} && cat -- {ShellQuote(backup.RemotePath)}"));
                using var output = command.OutputStream;
                await using var destination = new FileStream(
                    localPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    81920,
                    useAsync: true);

                var asyncResult = command.BeginExecute();
                try
                {
                    await output.CopyToAsync(destination, cancellationToken);
                    command.EndExecute(asyncResult);
                    cancellationToken.ThrowIfCancellationRequested();

                    if (command.ExitStatus != 0)
                    {
                        throw CreateRemoteCommandException(command);
                    }
                }
                finally
                {
                    if (client.IsConnected)
                    {
                        client.Disconnect();
                    }
                }
            }, cancellationToken);
        }
        catch
        {
            try
            {
                if (File.Exists(localPath))
                {
                    File.Delete(localPath);
                }
            }
            catch
            {
            }

            throw;
        }

        progress?.Report($"Downloaded: {localPath}");
    }

    public Task DeleteAsync(RemoteBackupInfo backup, CancellationToken cancellationToken) =>
        ExecuteCommandAsync(
            $"test -f {ShellQuote(backup.RemotePath)} && rm -f -- {ShellQuote(backup.RemotePath)}",
            cancellationToken);

    public async Task RestoreAsync(
        RemoteBackupInfo backup,
        string targetPath,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            throw new InvalidOperationException("The restore target path is not configured.");
        }

        var normalizedTarget = targetPath.Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(normalizedTarget) || normalizedTarget == "/")
        {
            throw new InvalidOperationException("The restore target must be a directory below the filesystem root.");
        }

        var parent = GetRemoteParent(normalizedTarget);
        var name = GetRemoteName(normalizedTarget);

        progress?.Report($"Validating {backup.FileName}...");

        var restoreCommand =
            "set -e; " +
            $"test -f {ShellQuote(backup.RemotePath)}; " +
            "tmp=$(mktemp /tmp/badwolfquiz-restore-XXXXXX.tar.gz); " +
            "trap 'rm -f \"$tmp\"' EXIT; " +
            $"cp -- {ShellQuote(backup.RemotePath)} \"$tmp\"; " +
            "tar -tzf \"$tmp\" >/dev/null; " +
            $"tar -tzf \"$tmp\" | while IFS= read -r entry; do case \"$entry\" in {ShellQuote(name)}|{ShellQuote(name + "/")}*) ;; *) echo 'Archive contains unexpected paths.' >&2; exit 1 ;; esac; done; " +
            $"rm -rf -- {ShellQuote(normalizedTarget)}; " +
            $"mkdir -p -- {ShellQuote(parent)}; " +
            $"tar -xzf \"$tmp\" -C {ShellQuote(parent)}";

        progress?.Report($"Restoring {backup.TypeDisplay} backup...");
        await ExecuteCommandAsync(restoreCommand, cancellationToken);
        progress?.Report("Restore completed.");
    }

    private async Task<string> ExecuteCommandAsync(
        string commandText,
        CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var client = CreateClient();
            using var cancellationRegistration =
                cancellationToken.Register(() => SafeDispose(client));

            try
            {
                client.Connect();
                cancellationToken.ThrowIfCancellationRequested();

                using var command = client.CreateCommand(BuildCommand(commandText));
                var output = command.Execute();
                cancellationToken.ThrowIfCancellationRequested();

                if (command.ExitStatus != 0)
                {
                    throw CreateRemoteCommandException(command);
                }

                return output;
            }
            catch (Exception) when (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }
            finally
            {
                if (client.IsConnected)
                {
                    client.Disconnect();
                }
            }
        }, cancellationToken);
    }

    private static bool TryClassify(
        string fileName,
        out RemoteBackupType type,
        out DateTime? backupTime)
    {
        var appDataMatch = AppDataPattern.Match(fileName);
        if (appDataMatch.Success)
        {
            type = RemoteBackupType.AppData;
            backupTime = ParseBackupTime(appDataMatch);
            return true;
        }

        var fullSiteMatch = FullSitePattern.Match(fileName);
        if (fullSiteMatch.Success)
        {
            type = RemoteBackupType.FullSite;
            backupTime = ParseBackupTime(fullSiteMatch);
            return true;
        }

        type = default;
        backupTime = null;
        return false;
    }

    private static DateTime? ParseBackupTime(Match match) =>
        DateTime.TryParseExact(
            match.Groups["date"].Value + match.Groups["time"].Value,
            "yyyyMMddHHmmss",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var value)
            ? value
            : null;

    private void EnsureBackupDirectoryConfigured()
    {
        if (string.IsNullOrWhiteSpace(settings.RemoteBackupDirectory))
        {
            throw new InvalidOperationException("The remote backup directory is not configured.");
        }
    }

    private SshClient CreateClient() =>
        new(settings.Host, settings.Port, settings.Username, settings.Password);

    private string BuildCommand(string command) =>
        settings.UseSudo
            ? $"printf '%s\\n' {ShellQuote(settings.Password)} | sudo -S -p '' sh -c {ShellQuote(command)}"
            : command;

    private static string GetRemoteParent(string path)
    {
        var separator = path.LastIndexOf('/');
        return separator <= 0 ? "/" : path[..separator];
    }

    private static string GetRemoteName(string path)
    {
        var separator = path.LastIndexOf('/');
        return separator < 0 ? path : path[(separator + 1)..];
    }

    private static InvalidOperationException CreateRemoteCommandException(SshCommand command) =>
        new(
            string.IsNullOrWhiteSpace(command.Error)
                ? $"Remote command failed with exit code {command.ExitStatus}."
                : command.Error.Trim());

    private static string ShellQuote(string value) =>
        "'" + value.Replace("'", "'\"'\"'") + "'";

    private static void SafeDispose(IDisposable disposable)
    {
        try
        {
            disposable.Dispose();
        }
        catch
        {
        }
    }
}
