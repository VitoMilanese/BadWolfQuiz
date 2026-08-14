using System.Windows;

namespace BadWolfQuizLogDownloaderWpf;

public partial class MainWindow
{
    private async void BackupAppDataButton_Click(object sender, RoutedEventArgs e)
    {
        if (_settings is null)
        {
            return;
        }

        await CreateBackupAsync("app-data", _settings.RemoteAppDataPath, "App_Data");
    }

    private async void BackupServiceFolderButton_Click(object sender, RoutedEventArgs e)
    {
        if (_settings is null)
        {
            return;
        }

        await CreateBackupAsync("service-folder", _settings.RemoteServiceDirectoryPath, "service folder");
    }

    private async void BrowseBackupsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_settings is null || _operationCancellation is not null || _liveCancellation is not null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_settings.RemoteBackupDirectory))
        {
            MessageBox.Show(this, "The remote backup directory is not configured in appsettings.json.", "Backup configuration", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var browser = new RemoteBackupBrowserWindow(_settings) { Owner = this };
        browser.ShowDialog();
        await RefreshServiceStateAsync();
    }

    private async Task CreateBackupAsync(string backupType, string remotePath, string displayName)
    {
        if (_settings is null || _operationCancellation is not null || _liveCancellation is not null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(remotePath))
        {
            MessageBox.Show(this, $"The remote {displayName} path is not configured in appsettings.json.", "Backup configuration", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(_settings.RemoteBackupDirectory))
        {
            MessageBox.Show(this, "The remote backup directory is not configured in appsettings.json.", "Backup configuration", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _operationCancellation = new CancellationTokenSource();
        SetBusy(true);
        SetBackupControlsEnabled(false);

        var fileName = $"badwolfquiz-{backupType}-{DateTime.Now:yyyyMMdd-HHmmss}.tar.gz";

        try
        {
            var progress = new Progress<string>(message => StatusText.Text = message);
            var client = new SshLogClient(_settings);
            var remoteBackupPath = await client.CreateDirectoryBackupAsync(remotePath, _settings.RemoteBackupDirectory, fileName, progress, _operationCancellation.Token);
            StatusText.Text = $"Backup saved: {remoteBackupPath}";
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Backup cancelled.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Backup failed.";
            MessageBox.Show(this, ex.Message, "Backup error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _operationCancellation?.Dispose();
            _operationCancellation = null;
            SetBusy(false);
            SetBackupControlsEnabled(true);
        }
    }

    private void SetBackupControlsEnabled(bool enabled)
    {
        BackupAppDataButton.IsEnabled = enabled;
        BackupServiceFolderButton.IsEnabled = enabled;
        BrowseBackupsButton.IsEnabled = enabled;
    }
}
