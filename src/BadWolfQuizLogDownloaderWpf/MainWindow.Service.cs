using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace BadWolfQuizLogDownloaderWpf;

public partial class MainWindow
{
    private bool _serviceOperationInProgress;

    private async void MainWindow_ServiceLoaded(object sender, RoutedEventArgs e)
    {
        await Dispatcher.Yield(DispatcherPriority.ContextIdle);
        await RefreshServiceStateAsync();
    }

    private async void RefreshServiceButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshServiceStateAsync();
    }

    private async void StartServiceButton_Click(object sender, RoutedEventArgs e)
    {
        await RunServiceActionAsync(
            (client, cancellationToken) => client.StartServiceAsync(cancellationToken),
            "Starting service...");
    }

    private async void StopServiceButton_Click(object sender, RoutedEventArgs e)
    {
        await RunServiceActionAsync(
            (client, cancellationToken) => client.StopServiceAsync(cancellationToken),
            "Stopping service...");
    }

    private async Task RunServiceActionAsync(
        Func<SshLogClient, CancellationToken, Task> action,
        string progressMessage)
    {
        if (_settings is null || _serviceOperationInProgress)
        {
            return;
        }

        _serviceOperationInProgress = true;
        UpdateServiceButtons(ServiceStatusText.Text);
        StatusText.Text = progressMessage;

        var client = new SshLogClient(_settings);

        try
        {
            await action(client, CancellationToken.None);
        }
        catch (Exception ex)
        {
            await AppendServiceDiagnosticAsync($"command-error message={SanitizeDiagnosticValue(ex.Message)}");
            StatusText.Text = "Service command failed.";
            MessageBox.Show(
                this,
                ex.Message,
                "Service error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            try
            {
                await RefreshServiceStateCoreAsync(client);
            }
            catch (Exception ex)
            {
                await AppendServiceDiagnosticAsync($"status-error message={SanitizeDiagnosticValue(ex.Message)}");
                ServiceStatusText.Text = "Unavailable";
                StatusText.Text = "Could not read service status after the service command.";
                MessageBox.Show(
                    this,
                    ex.Message,
                    "Service status error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                _serviceOperationInProgress = false;
                UpdateServiceButtons(ServiceStatusText.Text);
            }
        }
    }

    private async Task RefreshServiceStateAsync()
    {
        if (_settings is null || _serviceOperationInProgress)
        {
            return;
        }

        _serviceOperationInProgress = true;
        UpdateServiceButtons(ServiceStatusText.Text);
        ServiceStatusText.Text = "Checking...";

        try
        {
            ServiceNameText.Text = _settings.ServiceName;
            var client = new SshLogClient(_settings);
            await RefreshServiceStateCoreAsync(client);
        }
        catch (Exception ex)
        {
            await AppendServiceDiagnosticAsync($"status-error message={SanitizeDiagnosticValue(ex.Message)}");
            ServiceStatusText.Text = "Unavailable";
            StatusText.Text = "Could not read service status.";
            MessageBox.Show(
                this,
                ex.Message,
                "Service status error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            _serviceOperationInProgress = false;
            UpdateServiceButtons(ServiceStatusText.Text);
        }
    }

    private async Task RefreshServiceStateCoreAsync(SshLogClient client)
    {
        var state = await client.GetServiceStatusAsync(CancellationToken.None);
        var normalizedState = NormalizeServiceState(state);

        ServiceStatusText.Text = normalizedState;
        StatusText.Text = $"Service {_settings!.ServiceName}: {normalizedState}.";
        await AppendServiceDiagnosticAsync(
            $"status raw={SanitizeDiagnosticValue(state)} normalized={SanitizeDiagnosticValue(normalizedState)}");
    }

    private async Task AppendServiceDiagnosticAsync(string message)
    {
        try
        {
            var directory = GetOutputDirectory();
            Directory.CreateDirectory(directory);

            var path = Path.Combine(directory, "service-diagnostics.log");
            var line = $"{DateTimeOffset.Now:O} service={_settings?.ServiceName ?? "unknown"} {message}{Environment.NewLine}";

            await File.AppendAllTextAsync(path, line, new UTF8Encoding(false));
        }
        catch
        {
        }
    }

    private static string SanitizeDiagnosticValue(string value) =>
        value
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();

    private void UpdateServiceButtons(string state)
    {
        RefreshServiceButton.IsEnabled = !_serviceOperationInProgress && _settings is not null;

        if (_serviceOperationInProgress || _settings is null)
        {
            StartServiceButton.IsEnabled = false;
            StopServiceButton.IsEnabled = false;
            return;
        }

        var normalized = state.Trim().ToLowerInvariant();
        StartServiceButton.IsEnabled = normalized is not "active" and not "checking...";
        StopServiceButton.IsEnabled = normalized == "active";
    }

    private static string NormalizeServiceState(string state)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            return "Unknown";
        }

        return state.Trim().ToLowerInvariant() switch
        {
            "active" => "Active",
            "inactive" => "Inactive",
            "failed" => "Failed",
            "activating" => "Activating",
            "deactivating" => "Deactivating",
            "reloading" => "Reloading",
            _ => state.Trim()
        };
    }
}
