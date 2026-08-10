using System.Windows;

namespace BadWolfQuizLogDownloaderWpf;

public partial class App : Application
{
    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);

        if (MainWindow is null)
        {
            return;
        }

        var version = typeof(App).Assembly.GetName().Version;
        MainWindow.Title = version is null
            ? "BadWolfQuiz Log Downloader"
            : $"BadWolfQuiz Log Downloader v{version.ToString(3)}";
    }
}
