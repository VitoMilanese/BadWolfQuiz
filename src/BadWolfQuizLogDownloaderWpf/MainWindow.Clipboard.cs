using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace BadWolfQuizLogDownloaderWpf;

public partial class MainWindow
{
    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);
        LogList.SelectionMode = SelectionMode.Extended;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        if (e.Key != Key.C ||
            (Keyboard.Modifiers & ModifierKeys.Control) == 0 ||
            !LogList.IsKeyboardFocusWithin ||
            LogList.SelectedItems.Count == 0)
        {
            return;
        }

        var selectedRows = LogList.SelectedItems
            .OfType<LogEntryView>()
            .OrderBy(item => LogList.Items.IndexOf(item))
            .Select(item => item.Text)
            .ToArray();

        if (selectedRows.Length == 0)
        {
            return;
        }

        Clipboard.SetText(string.Join(Environment.NewLine, selectedRows));
        e.Handled = true;
    }
}
