using Renci.SshNet;
using System.IO;

namespace BadWolfQuizLogDownloaderWpf;

internal sealed class SshLogClient(AppSettings settings)
{
    public async Task<string> DownloadLogsAsync(
        string since,
        bool onlyGameplayLookupFailures,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        progress?.Report("Connecting to server...");

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

                progress?.Report("Connected. Reading systemd journal...");

                using var command = client.CreateCommand(
                    BuildJournalCommand(since, onlyGameplayLookupFailures, follow: false));

                var output = command.Execute();
                cancellationToken.ThrowIfCancellationRequested();

                if (command.ExitStatus != 0 &&
                    !(onlyGameplayLookupFailures && command.ExitStatus == 1))
                {
                    throw CreateRemoteCommandException(command);
                }

                progress?.Report("Logs received.");
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

    public async Task StreamLogsAsync(
        string since,
        bool onlyGameplayLookupFailures,
        Func<string, Task> onLine,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        using var client = CreateClient();

        progress?.Report("Connecting live log stream...");
        await Task.Run(client.Connect, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        progress?.Report("Live log stream connected.");

        using var command = client.CreateCommand(
            BuildJournalCommand(since, onlyGameplayLookupFailures, follow: true));
        using var output = command.OutputStream;
        using var reader = new StreamReader(output);

        var asyncResult = command.BeginExecute();

        using var cancellationRegistration = cancellationToken.Register(() =>
        {
            try
            {
                command.CancelAsync();
            }
            catch
            {
            }

            try
            {
                if (client.IsConnected)
                {
                    client.Disconnect();
                }
            }
            catch
            {
            }
        });

        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string? line;

                try
                {
                    line = await reader.ReadLineAsync();
                }
                catch (Exception) when (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException(cancellationToken);
                }

                if (line is null)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (asyncResult.IsCompleted)
                    {
                        break;
                    }

                    await Task.Delay(50, cancellationToken);
                    continue;
                }

                await onLine(line);
            }
        }
        finally
        {
            try
            {
                if (!asyncResult.IsCompleted)
                {
                    command.CancelAsync();
                }
            }
            catch
            {
            }

            try
            {
                if (client.IsConnected)
                {
                    client.Disconnect();
                }
            }
            catch
            {
            }
        }
    }

    public Task<string> GetServiceStatusAsync(CancellationToken cancellationToken) =>
        ExecuteServiceCommandAsync("is-active", allowNonZeroExit: true, cancellationToken);

    public async Task StartServiceAsync(CancellationToken cancellationToken)
    {
        await ExecuteServiceCommandAsync("start", allowNonZeroExit: false, cancellationToken);
    }

    public async Task StopServiceAsync(CancellationToken cancellationToken)
    {
        await ExecuteServiceCommandAsync("stop", allowNonZeroExit: false, cancellationToken);
    }

    public async Task<string> CreateDirectoryBackupAsync(
        string remoteSourcePath,
        string remoteBackupDirectory,
        string fileName,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(remoteSourcePath))
        {
            throw new InvalidOperationException("The remote backup source path is not configured.");
        }

        if (string.IsNullOrWhiteSpace(remoteBackupDirectory))
        {
            throw new InvalidOperationException("The remote backup directory is not configured.");
        }

        progress?.Report("Connecting to server for backup...");

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

                progress?.Report("Preparing remote backup directory...");

                var remoteArchivePath =
                    remoteBackupDirectory.Trim().TrimEnd('/') + "/" + fileName;

                using var command = client.CreateCommand(
                    BuildArchiveCommand(
                        remoteSourcePath,
                        remoteBackupDirectory,
                        remoteArchivePath));

                progress?.Report("Creating backup archive on server...");
                command.Execute();
                cancellationToken.ThrowIfCancellationRequested();

                if (command.ExitStatus != 0)
                {
                    throw CreateRemoteCommandException(command);
                }

                progress?.Report("Backup created on server.");
                return remoteArchivePath;
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

    private async Task<string> ExecuteServiceCommandAsync(
        string action,
        bool allowNonZeroExit,
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

                using var command = client.CreateCommand(BuildServiceCommand(action));
                var output = command.Execute().Trim();
                cancellationToken.ThrowIfCancellationRequested();

                if (command.ExitStatus != 0 && !allowNonZeroExit)
                {
                    throw CreateRemoteCommandException(command);
                }

                if (command.ExitStatus != 0 &&
                    allowNonZeroExit &&
                    string.IsNullOrWhiteSpace(output))
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

    private SshClient CreateClient() =>
        new(settings.Host, settings.Port, settings.Username, settings.Password);

    private string BuildJournalCommand(
        string since,
        bool onlyGameplayLookupFailures,
        bool follow)
    {
        var journalCommand =
            $"journalctl -u {ShellQuote(settings.ServiceName)} " +
            $"--since {ShellQuote(since)} --no-pager -o short-iso";

        if (follow)
        {
            journalCommand += " -f";
        }

        if (onlyGameplayLookupFailures)
        {
            journalCommand +=
                " | grep --line-buffered -F " +
                ShellQuote("Host gameplay session lookup failed");
        }

        return settings.UseSudo
            ? BuildSudoCommand(journalCommand)
            : journalCommand;
    }

    private string BuildServiceCommand(string action)
    {
        var serviceCommand =
            $"systemctl {action} {ShellQuote(settings.ServiceName)}";

        return settings.UseSudo
            ? BuildSudoCommand(serviceCommand)
            : serviceCommand;
    }

    private string BuildArchiveCommand(
        string remoteSourcePath,
        string remoteBackupDirectory,
        string remoteArchivePath)
    {
        var normalizedSourcePath = remoteSourcePath.Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(normalizedSourcePath) || normalizedSourcePath == "/")
        {
            throw new InvalidOperationException(
                "The configured backup source path must point to a directory, not the filesystem root.");
        }

        var normalizedBackupDirectory = remoteBackupDirectory.Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(normalizedBackupDirectory) || normalizedBackupDirectory == "/")
        {
            throw new InvalidOperationException(
                "The configured backup directory must not be the filesystem root.");
        }

        var parent = Path.GetDirectoryName(
                normalizedSourcePath.Replace('/', Path.DirectorySeparatorChar))
            ?.Replace(Path.DirectorySeparatorChar, '/');
        var name = Path.GetFileName(
            normalizedSourcePath.Replace('/', Path.DirectorySeparatorChar));

        if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException(
                $"Invalid remote backup source path: {remoteSourcePath}");
        }

        var archiveCommand =
            $"test -d {ShellQuote(normalizedSourcePath)} && " +
            $"mkdir -p {ShellQuote(normalizedBackupDirectory)} && " +
            $"chmod u+rwx {ShellQuote(normalizedBackupDirectory)} && " +
            $"tar -czf {ShellQuote(remoteArchivePath)} " +
            $"-C {ShellQuote(parent)} {ShellQuote(name)}";

        return settings.UseSudo
            ? BuildSudoCommand(archiveCommand)
            : archiveCommand;
    }

    private string BuildSudoCommand(string command) =>
        $"printf '%s\\n' {ShellQuote(settings.Password)} | " +
        $"sudo -S -p '' sh -c {ShellQuote(command)}";

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
