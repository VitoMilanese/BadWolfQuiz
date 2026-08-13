using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.IO;

namespace BadWolfQuizLogDownloaderWpf;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<LogEntryView> _pageItems = new();
    private readonly List<string> _livePendingLines = new();
    private readonly DispatcherTimer _liveFlushTimer = new();

    private AppSettings? _settings;
    private CancellationTokenSource? _operationCancellation;
    private CancellationTokenSource? _liveCancellation;
    private StreamWriter? _liveWriter;

    private IReadOnlyList<LogEntry> _allEntries = Array.Empty<LogEntry>();
    private IReadOnlyList<LogEntry> _filteredEntries = Array.Empty<LogEntry>();
    private int _currentPage;
    private int _pageSize = 250;
    private bool _restoringTheme;

    public MainWindow()
    {
        InitializeComponent();

        var iconPath = Path.Combine(AppContext.BaseDirectory, "logger.ico");
        if (File.Exists(iconPath))
        {
            using var iconStream = File.OpenRead(iconPath);
            Icon = BitmapFrame.Create(
                iconStream,
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);
        }

        LogList.ItemsSource = _pageItems;

        _liveFlushTimer.Interval = TimeSpan.FromMilliseconds(250);
        _liveFlushTimer.Tick += (_, _) => FlushLiveLines();

        Loaded += (_, _) =>
        {
            LoadSettings();
            RestoreTheme();
        };
        Closing += MainWindow_Closing;
    }

    private int PageCount =>
        _filteredEntries.Count == 0
            ? 0
            : (int)Math.Ceiling(_filteredEntries.Count / (double)_pageSize);

    private void LoadSettings()
    {
        try
        {
            _settings = AppSettings.Load();
            StatusText.Text =
                $"Ready. Server: {_settings.Username}@{_settings.Host}:{_settings.Port}";
        }
        catch (Exception ex)
        {
            DownloadButton.IsEnabled = false;
            StartLiveButton.IsEnabled = false;
            StatusText.Text = "Configuration error.";
            MessageBox.Show(this, ex.Message, "Configuration error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void DownloadButton_Click(object sender, RoutedEventArgs e)
    {
        if (_settings is null || _operationCancellation is not null || _liveCancellation is not null)
        {
            return;
        }

        _operationCancellation = new CancellationTokenSource();
        SetBusy(true);

        try
        {
            var progress = new Progress<string>(message => StatusText.Text = message);
            var client = new SshLogClient(_settings);

            var logs = await client.DownloadLogsAsync(
                GetJournalSince(GetComboText(PeriodCombo, "24 hours")),
                GameplayOnlyCheck.IsChecked == true,
                progress,
                _operationCancellation.Token);

            var normalized = NormalizeLineEndings(logs);

            var directory = GetOutputDirectory();
            Directory.CreateDirectory(directory);

            var suffix = GameplayOnlyCheck.IsChecked == true ? "-gameplay-errors" : "";
            var path = Path.Combine(
                directory,
                $"badwolfquiz-{DateTime.Now:yyyyMMdd-HHmmss}{suffix}.log");

            await File.WriteAllTextAsync(
                path,
                normalized,
                new UTF8Encoding(false),
                _operationCancellation.Token);

            await LoadLogsAsync(normalized, $"Saved: {path}", _operationCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Operation cancelled.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Download failed.";
            MessageBox.Show(this, ex.Message, "SSH error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _operationCancellation?.Dispose();
            _operationCancellation = null;
            SetBusy(false);
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Cancelling...";
        _operationCancellation?.Cancel();
    }

    private async void OpenFileButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open BadWolfQuiz log",
            Filter = "Log files (*.log;*.txt)|*.log;*.txt|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        _operationCancellation = new CancellationTokenSource();
        SetBusy(true);

        try
        {
            StatusText.Text = "Reading log file...";
            var text = await File.ReadAllTextAsync(
                dialog.FileName,
                _operationCancellation.Token);

            await LoadLogsAsync(
                NormalizeLineEndings(text),
                $"Loaded: {dialog.FileName}",
                _operationCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Operation cancelled.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "File error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _operationCancellation?.Dispose();
            _operationCancellation = null;
            SetBusy(false);
        }
    }

    private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var path = GetOutputDirectory();
        Directory.CreateDirectory(path);

        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }

    private async void StartLiveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_settings is null || _liveCancellation is not null || _operationCancellation is not null)
        {
            return;
        }

        _liveCancellation = new CancellationTokenSource();
        SetLiveState(true);

        _allEntries = Array.Empty<LogEntry>();
        _filteredEntries = Array.Empty<LogEntry>();
        _pageItems.Clear();
        UpdatePaging();

        try
        {
            if (SaveLiveCheck.IsChecked == true)
            {
                var directory = GetOutputDirectory();
                Directory.CreateDirectory(directory);

                var path = Path.Combine(
                    directory,
                    $"badwolfquiz-live-{DateTime.Now:yyyyMMdd-HHmmss}.log");

                _liveWriter = new StreamWriter(
                    path,
                    append: false,
                    new UTF8Encoding(false))
                {
                    AutoFlush = true
                };

                StatusText.Text = $"Starting live stream. Saving to: {path}";
            }
            else
            {
                StatusText.Text = "Starting live stream...";
            }

            _liveFlushTimer.Start();

            var client = new SshLogClient(_settings);
            var progress = new Progress<string>(message => StatusText.Text = message);

            await client.StreamLogsAsync(
                GetJournalSince(GetComboText(PeriodCombo, "1 hour")),
                GameplayOnlyCheck.IsChecked == true,
                async line =>
                {
                    if (_liveWriter is not null)
                    {
                        await _liveWriter.WriteLineAsync(line);
                    }

                    lock (_livePendingLines)
                    {
                        _livePendingLines.Add(line);
                    }
                },
                progress,
                _liveCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Live stream stopped.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Live stream failed.";
            MessageBox.Show(this, ex.Message, "SSH live stream error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _liveFlushTimer.Stop();
            FlushLiveLines();

            if (_liveWriter is not null)
            {
                await _liveWriter.DisposeAsync();
                _liveWriter = null;
            }

            _liveCancellation?.Dispose();
            _liveCancellation = null;
            SetLiveState(false);
        }
    }

    private void StopLiveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_liveCancellation is null)
        {
            return;
        }

        StatusText.Text = "Stopping live stream...";
        StopLiveButton.IsEnabled = false;
        _liveCancellation.Cancel();
    }

    private void FlushLiveLines()
    {
        string[] lines;

        lock (_livePendingLines)
        {
            if (_livePendingLines.Count == 0)
            {
                return;
            }

            lines = _livePendingLines.ToArray();
            _livePendingLines.Clear();
        }

        var parsed = LogParser.Parse(
            string.Join(Environment.NewLine, lines),
            CancellationToken.None);

        var updated = _allEntries.ToList();

        if (updated.Count > 0 &&
            parsed.Count > 0 &&
            parsed[0].Level == LogLevel.Other)
        {
            var last = updated[^1];
            updated[^1] = last with
            {
                Text = last.Text + Environment.NewLine + parsed[0].Text
            };
            parsed = parsed.Skip(1).ToArray();
        }

        updated.AddRange(parsed);

        const int maxLiveEntries = 10000;
        if (updated.Count > maxLiveEntries)
        {
            updated.RemoveRange(0, updated.Count - maxLiveEntries);
        }

        _allEntries = updated;
        ApplyFilters();
    }

    private async Task LoadLogsAsync(
        string logs,
        string completedStatus,
        CancellationToken cancellationToken)
    {
        StatusText.Text = "Parsing log entries...";

        _allEntries = await Task.Run(
            () => LogParser.Parse(logs, cancellationToken),
            cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        ApplyFilters();
        StatusText.Text = $"{completedStatus} Entries: {_allEntries.Count:n0}.";
    }

    private void FilterChanged(object sender, RoutedEventArgs e)
    {
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        if (!IsLoaded)
        {
            return;
        }

        _filteredEntries = _allEntries
            .Where(entry => IsLevelVisible(entry.Level))
            .Reverse()
            .ToArray();

        _currentPage = 0;
        RenderCurrentPage();
    }

    private void RenderCurrentPage()
    {
        _pageItems.Clear();

        var start = _currentPage * _pageSize;

        foreach (var entry in _filteredEntries.Skip(start).Take(_pageSize))
        {
            _pageItems.Add(new LogEntryView
            {
                Level = entry.Level,
                Text = entry.Text,
                Brush = GetLevelBrush(entry.Level)
            });
        }

        UpdatePaging();

        if (_pageItems.Count > 0)
        {
            LogList.ScrollIntoView(_pageItems[0]);
        }
    }

    private void UpdatePaging()
    {
        var pageCount = PageCount;
        PageLabel.Text = pageCount == 0
            ? $"Page 0 / 0 · {_filteredEntries.Count:n0} entries"
            : $"Page {_currentPage + 1:n0} / {pageCount:n0} · {_filteredEntries.Count:n0} entries";
    }

    private void FirstPage_Click(object sender, RoutedEventArgs e) => GoToPage(0);
    private void PreviousPage_Click(object sender, RoutedEventArgs e) => GoToPage(_currentPage - 1);
    private void NextPage_Click(object sender, RoutedEventArgs e) => GoToPage(_currentPage + 1);
    private void LastPage_Click(object sender, RoutedEventArgs e) => GoToPage(Math.Max(0, PageCount - 1));

    private void GoToPage(int page)
    {
        _currentPage = PageCount == 0
            ? 0
            : Math.Clamp(page, 0, PageCount - 1);

        RenderCurrentPage();
    }

    private void PageSizeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        if (int.TryParse(GetComboText(PageSizeCombo, "250"), out var value))
        {
            _pageSize = value;
            _currentPage = 0;
            RenderCurrentPage();
        }
    }

    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        var themeName = GetComboText(ThemeCombo, "Light");
        ApplyTheme(themeName);

        if (!_restoringTheme)
        {
            UserPreferences.SaveTheme(themeName);
        }

        RenderCurrentPage();
    }

    private void RestoreTheme()
    {
        var savedTheme = UserPreferences.LoadTheme();
        if (string.IsNullOrWhiteSpace(savedTheme))
        {
            ApplyTheme(GetComboText(ThemeCombo, "Light"));
            return;
        }

        var item = ThemeCombo.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(x => string.Equals(
                x.Content?.ToString(),
                savedTheme,
                StringComparison.Ordinal));

        if (item is null)
        {
            ApplyTheme(GetComboText(ThemeCombo, "Light"));
            return;
        }

        _restoringTheme = true;
        try
        {
            ThemeCombo.SelectedItem = item;
            ApplyTheme(savedTheme);
        }
        finally
        {
            _restoringTheme = false;
        }
    }

    private void ApplyTheme(string name)
    {
        var palette = ThemePalette.For(name);

        SetBrush("AppBackgroundBrush", palette.Background);
        SetBrush("PanelBrush", palette.Panel);
        SetBrush("ViewerBrush", palette.Viewer);
        SetBrush("AppForegroundBrush", palette.Foreground);
        SetBrush("HeaderForegroundBrush", palette.HeaderForeground);
        SetBrush("AccentForegroundBrush", palette.AccentForeground);
        SetBrush("ThemeComboBackgroundBrush", palette.ThemeComboBackground);
        SetBrush("ThemeComboForegroundBrush", palette.ThemeComboForeground);
        SetBrush("AccentBrush", palette.Accent);
        SetThemeDecoration(name, palette);
        SetBrush("TraceBrush", palette.Trace);
        SetBrush("DebugBrush", palette.Debug);
        SetBrush("InfoBrush", palette.Info);
        SetBrush("WarningBrush", palette.Warning);
        SetBrush("ErrorBrush", palette.Error);
        SetBrush("CriticalBrush", palette.Critical);

        var helper = new PaletteHelper();
        var theme = helper.GetTheme();
        theme.SetBaseTheme(palette.IsDark ? BaseTheme.Dark : BaseTheme.Light);
        theme.SetPrimaryColor(palette.Accent);
        theme.SetSecondaryColor(palette.Secondary);
        helper.SetTheme(theme);
    }

    private static void SetBrush(string key, Color color)
    {
        Application.Current.Resources[key] = new SolidColorBrush(color);
    }

    private static void SetThemeDecoration(string name, ThemePalette palette)
    {
        Brush brush = name switch
        {
            "Italian" => CreateStripedBrush(
                ("#009246", 0.00), ("#009246", 0.333),
                ("#FFFFFF", 0.333), ("#FFFFFF", 0.666),
                ("#CE2B37", 0.666), ("#CE2B37", 1.00)),

            "LGBTQ+" => CreateStripedBrush(
                ("#E40303", 0.000), ("#E40303", 0.166),
                ("#FF8C00", 0.166), ("#FF8C00", 0.333),
                ("#FFED00", 0.333), ("#FFED00", 0.500),
                ("#008026", 0.500), ("#008026", 0.666),
                ("#004DFF", 0.666), ("#004DFF", 0.833),
                ("#750787", 0.833), ("#750787", 1.000)),

            _ => new SolidColorBrush(palette.Secondary)
        };

        Application.Current.Resources["ThemeDecorationBrush"] = brush;
    }

    private static Brush CreateStripedBrush(params (string Color, double Offset)[] stops)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0.5),
            EndPoint = new Point(1, 0.5)
        };

        foreach (var (color, offset) in stops)
        {
            brush.GradientStops.Add(new GradientStop(
                (Color)ColorConverter.ConvertFromString(color),
                offset));
        }

        brush.Freeze();
        return brush;
    }

    private Brush GetLevelBrush(LogLevel level)
    {
        var key = level switch
        {
            LogLevel.Trace => "TraceBrush",
            LogLevel.Debug => "DebugBrush",
            LogLevel.Information => "InfoBrush",
            LogLevel.Warning => "WarningBrush",
            LogLevel.Error => "ErrorBrush",
            LogLevel.Critical => "CriticalBrush",
            _ => "AppForegroundBrush"
        };

        return (Brush)Application.Current.Resources[key];
    }

    private bool IsLevelVisible(LogLevel level) => level switch
    {
        LogLevel.Trace => TraceCheck.IsChecked == true,
        LogLevel.Debug => DebugCheck.IsChecked == true,
        LogLevel.Information => InfoCheck.IsChecked == true,
        LogLevel.Warning => WarningCheck.IsChecked == true,
        LogLevel.Error => ErrorCheck.IsChecked == true,
        LogLevel.Critical => CriticalCheck.IsChecked == true,
        _ => OtherCheck.IsChecked == true
    };

    private void SetBusy(bool busy)
    {
        BusyProgress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        CancelButton.IsEnabled = busy;
        DownloadButton.IsEnabled = !busy && _settings is not null;
        OpenFileButton.IsEnabled = !busy;
        OpenFolderButton.IsEnabled = !busy;
        StartLiveButton.IsEnabled = !busy && _settings is not null && _liveCancellation is null;
        PeriodCombo.IsEnabled = !busy;
        GameplayOnlyCheck.IsEnabled = !busy;
        ThemeCombo.IsEnabled = !busy;
    }

    private void SetLiveState(bool live)
    {
        BusyProgress.Visibility = live ? Visibility.Visible : Visibility.Collapsed;
        StartLiveButton.IsEnabled = !live && _settings is not null;
        StopLiveButton.IsEnabled = live;
        DownloadButton.IsEnabled = !live && _settings is not null;
        OpenFileButton.IsEnabled = !live;
        OpenFolderButton.IsEnabled = !live;
        PeriodCombo.IsEnabled = !live;
        GameplayOnlyCheck.IsEnabled = !live;
        SaveLiveCheck.IsEnabled = !live;
        ThemeCombo.IsEnabled = true;
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_operationCancellation is null && _liveCancellation is null)
        {
            return;
        }

        var result = MessageBox.Show(
            this,
            "An operation is still running. Cancel it and close the application?",
            "Operation in progress",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
        {
            e.Cancel = true;
            return;
        }

        _operationCancellation?.Cancel();
        _liveCancellation?.Cancel();
    }

    private string GetOutputDirectory()
    {
        if (_settings is null)
        {
            return Path.Combine(AppContext.BaseDirectory, "Logs");
        }

        return Path.IsPathRooted(_settings.OutputDirectory)
            ? _settings.OutputDirectory
            : Path.Combine(AppContext.BaseDirectory, _settings.OutputDirectory);
    }

    private static string NormalizeLineEndings(string value) =>
        value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Replace("\n", Environment.NewLine, StringComparison.Ordinal);

    private static string GetJournalSince(string period) => period switch
    {
        "1 hour" => "1 hour ago",
        "6 hours" => "6 hours ago",
        "24 hours" => "24 hours ago",
        "3 days" => "3 days ago",
        "7 days" => "7 days ago",
        _ => "24 hours ago"
    };

    private static string GetComboText(ComboBox comboBox, string fallback) =>
        (comboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? fallback;

    private sealed record ThemePalette(
        Color Background,
        Color Panel,
        Color Viewer,
        Color Foreground,
        Color HeaderForeground,
        Color AccentForeground,
        Color ThemeComboBackground,
        Color ThemeComboForeground,
        Color Accent,
        Color Secondary,
        Color Trace,
        Color Debug,
        Color Info,
        Color Warning,
        Color Error,
        Color Critical,
        bool IsDark)
    {
        public static ThemePalette For(string name) => name switch
        {
            "Dark" => P("#1C1C1E", "#26262A", "#121214", "#ECECEC", "#101010", "#101010", "#EAF3FF", "#101010", "#5AA0FF", "#FFC107",
                "#78787D", "#9696A0", "#64AAFF", "#EBB946", "#F05A5A", "#FF3C3C", true),

            "Matrix" => P("#000000", "#001906", "#000000", "#00FF46", "#000000", "#001A08", "#001A08", "#00FF46", "#00FF46", "#A8FF00",
                "#006E23", "#00A032", "#00FF46", "#AAFF00", "#FF5050", "#FF0000", true),

            "Obsidian" => P("#111017", "#1F1C2A", "#0B0A0F", "#E6E1F0", "#FFFFFF", "#120B20", "#EDE5FF", "#181120", "#9664FF", "#C79CFF",
                "#5F5A6E", "#827896", "#A578FF", "#E6AA46", "#E65572", "#FF2D50", true),

            "Ukrainian" => P("#0057B8", "#1469C3", "#003E82", "#FFFFFF", "#002B5C", "#002B5C", "#003E82", "#FFFFFF", "#FFD700", "#0057B8",
                "#B9D2EB", "#D2E1F0", "#FFFFFF", "#FFE146", "#FF9650", "#FF5F5F", true),

            "UPA" => P("#191919", "#370A0A", "#0C0C0C", "#FFFFFF", "#FFFFFF", "#FFFFFF", "#240000", "#FFFFFF", "#C80000", "#101010",
                "#828282", "#B4B4B4", "#F5F5F5", "#FFB446", "#FF4646", "#D20000", true),

            "Italian" => P("#F5F5F2", "#FFFFFF", "#EEEEEA", "#CE2B37", "#FFFFFF", "#FFFFFF", "#FFFFFF", "#CE2B37", "#009246", "#CE2B37",
                "#6E6E6E", "#5F5F5F", "#009246", "#CD9100", "#CE2B37", "#A01423", false),

            "Warm Parchment" => P("#EFE0BE", "#F6EACD", "#E2CDA5", "#48321E", "#FFFFFF", "#FFFFFF", "#FFF4D8", "#48321E", "#985D28", "#B8864B",
                "#7D6955", "#6E5A46", "#4B5F87", "#A06914", "#A53723", "#7D1914", false),

            "Mint Fog" => P("#E2F2EC", "#EFF9F5", "#D3E8E0", "#23413A", "#FFFFFF", "#102F28", "#F2FFFA", "#23413A", "#37967D", "#78C7B0",
                "#69877D", "#55786E", "#2D7D6E", "#BE912D", "#B94B50", "#912D37", false),

            "LGBTQ+" => P("#FAF7FC", "#FFFFFF", "#F2ECF7", "#26202D", "#FFFFFF", "#FFFFFF", "#FFFFFF", "#3A164F", "#9650C8", "#0087C8",
                "#78737D", "#69646E", "#0087C8", "#EBAA00", "#DC3C5F", "#962878", false),

            _ => P("#F5F5F5", "#FFFFFF", "#FFFFFF", "#202124", "#FFFFFF", "#FFFFFF", "#FFFFFF", "#202124", "#1565C0", "#FFC107",
                "#666666", "#657786", "#1565C0", "#9A6700", "#C62828", "#8E0000", false)
        };

        private static ThemePalette P(
            string background,
            string panel,
            string viewer,
            string foreground,
            string headerForeground,
            string accentForeground,
            string themeComboBackground,
            string themeComboForeground,
            string accent,
            string secondary,
            string trace,
            string debug,
            string info,
            string warning,
            string error,
            string critical,
            bool dark) =>
            new(
                C(background), C(panel), C(viewer), C(foreground), C(headerForeground),
                C(accentForeground), C(themeComboBackground), C(themeComboForeground),
                C(accent), C(secondary), C(trace), C(debug),
                C(info), C(warning), C(error), C(critical), dark);

        private static Color C(string value) =>
            (Color)ColorConverter.ConvertFromString(value);
    }
}
