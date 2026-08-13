using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace BadWolfQuizLogDownloaderWpf;

public partial class RemoteBackupBrowserWindow : Window
{
    private readonly AppSettings _settings;
    private readonly RemoteBackupClient _backupClient;
    private readonly ObservableCollection<RemoteBackupInfo> _backups = new();
    private IReadOnlyList<RemoteBackupInfo> _allBackups = Array.Empty<RemoteBackupInfo>();
    private CancellationTokenSource? _operationCancellation;
    private bool _busy;

    public RemoteBackupBrowserWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        _backupClient = new RemoteBackupClient(settings);
        DataContext = this;
        Loaded += async (_, _) => await RefreshAsync();
    }

    public ObservableCollection<RemoteBackupInfo> Backups => _backups;

    private RemoteBackupInfo? SelectedBackup => BackupGrid.SelectedItem as RemoteBackupInfo;

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    private void FilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded)
        {
            ApplyFilter();
        }
    }

    private void BackupGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateActionButtons();

    private async void DownloadButton_Click(object sender, RoutedEventArgs e)
    {
        var backup = SelectedBackup;
        if (backup is null || _busy)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Download remote backup",
            FileName = backup.FileName,
            Filter = "Tar GZip archives (*.tar.gz)|*.tar.gz|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        await RunOperationAsync(async token =>
        {
            var progress = new Progress<string>(message => StatusText.Text = message);
            await _backupClient.DownloadAsync(backup, dialog.FileName, progress, token);
            StatusText.Text = $"Downloaded: {dialog.FileName}";
        }, "Downloading backup...", "Download failed");
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        var backup = SelectedBackup;
        if (backup is null || _busy)
        {
            return;
        }

        var confirmation = MessageBox.Show(
            this,
            $"Delete remote backup?\n\n{backup.FileName}\n\nThis cannot be undone.",
            "Delete remote backup",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        var deleted = false;
        await RunOperationAsync(async token =>
        {
            StatusText.Text = $"Deleting {backup.FileName}...";
            await _backupClient.DeleteAsync(backup, token);
            deleted = true;
            StatusText.Text = "Backup deleted.";
        }, "Deleting backup...", "Delete failed");

        if (deleted)
        {
            await RefreshAsync();
        }
    }

    private async void RestoreButton_Click(object sender, RoutedEventArgs e)
    {
        var backup = SelectedBackup;
        if (backup is null || _busy)
        {
            return;
        }

        var targetPath = backup.Type == RemoteBackupType.AppData
            ? _settings.RemoteAppDataPath
            : _settings.RemoteServiceDirectoryPath;

        if (string.IsNullOrWhiteSpace(targetPath))
        {
            MessageBox.Show(this, "The restore target is not configured in appsettings.json.", "Restore configuration", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var targetName = backup.Type == RemoteBackupType.AppData ? "App_Data" : "full site";
        var confirmation = MessageBox.Show(
            this,
            $"Restore {targetName} from this backup?\n\nArchive: {backup.FileName}\nTarget: {targetPath}\n\nThe service will be stopped and the current target directory will be replaced.",
            "Restore remote backup",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        await RunOperationAsync(async token =>
        {
            var serviceClient = new SshLogClient(_settings);
            var progress = new Progress<string>(message => StatusText.Text = message);

            StatusText.Text = "Stopping service before restore...";
            await serviceClient.StopServiceAsync(token);

            try
            {
                await _backupClient.RestoreAsync(backup, targetPath, progress, token);
            }
            catch
            {
                StatusText.Text = "Restore failed. The service remains stopped for safety.";
                throw;
            }

            StatusText.Text = "Starting service after restore...";
            await serviceClient.StartServiceAsync(token);
            StatusText.Text = $"Restore completed from {backup.FileName}.";
        }, "Restoring backup...", "Restore failed");
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (_operationCancellation is null)
        {
            return;
        }

        StatusText.Text = "Cancelling...";
        _operationCancellation.Cancel();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_busy)
        {
            Close();
        }
    }

    private async Task RefreshAsync()
    {
        if (_busy)
        {
            return;
        }

        await RunOperationAsync(async token =>
        {
            StatusText.Text = "Loading remote backups...";
            _allBackups = await _backupClient.ListAsync(token);
            ApplyFilter();
            StatusText.Text = $"Loaded {_allBackups.Count} supported backup(s).";
        }, "Loading remote backups...", "Could not load remote backups");
    }

    private async Task RunOperationAsync(Func<CancellationToken, Task> operation, string startStatus, string errorTitle)
    {
        if (_busy)
        {
            return;
        }

        _operationCancellation = new CancellationTokenSource();
        SetBusy(true);
        StatusText.Text = startStatus;

        try
        {
            await operation(_operationCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Operation cancelled.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, errorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _operationCancellation.Dispose();
            _operationCancellation = null;
            SetBusy(false);
        }
    }

    private void ApplyFilter()
    {
        var filter = (FilterCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All";
        IEnumerable<RemoteBackupInfo> items = _allBackups;

        if (filter == "App_Data")
        {
            items = items.Where(item => item.Type == RemoteBackupType.AppData);
        }
        else if (filter == "Full site")
        {
            items = items.Where(item => item.Type == RemoteBackupType.FullSite);
        }

        _backups.Clear();
        foreach (var item in items)
        {
            _backups.Add(item);
        }

        BackupGrid.SelectedItem = null;
        UpdateActionButtons();
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        BusyProgress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        FilterCombo.IsEnabled = !busy;
        RefreshButton.IsEnabled = !busy;
        BackupGrid.IsEnabled = !busy;
        CancelButton.IsEnabled = busy;
        UpdateActionButtons();
    }

    private void UpdateActionButtons()
    {
        var hasSelection = SelectedBackup is not null;
        DownloadButton.IsEnabled = !_busy && hasSelection;
        RestoreButton.IsEnabled = !_busy && hasSelection;
        DeleteButton.IsEnabled = !_busy && hasSelection;
    }
}
