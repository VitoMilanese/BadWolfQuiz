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
                    throw new InvalidOperationException(
                        string.IsNullOrWhiteSpace(command.Error)
                            ? $"Remote command failed with exit code {command.ExitStatus}."
                            : command.Error.Trim());
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

    private string BuildSudoCommand(string journalCommand) =>
        $"printf '%s\\n' {ShellQuote(settings.Password)} | " +
        $"sudo -S -p '' sh -c {ShellQuote(journalCommand)}";

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
