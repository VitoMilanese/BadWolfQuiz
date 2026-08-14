namespace BadWolfQuiz.Web.Tests;

public sealed class QuickTimerControlsTests
{
    [Fact]
    public void Host_lobby_exposes_quick_timer_adjustments()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "Games",
            "Lobby.cshtml"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "css",
            "site.css"));

        Assert.Contains("game-timer-quick-actions", markup);
        Assert.Contains("asp-page-handler=\"AddQuestionTimerTime\"", markup);
        Assert.Contains("new[] { 10, 15, 20, 30 }", markup);
        Assert.Contains("host-game-timer:hover .game-timer-quick-actions", css);
        Assert.Contains("host-game-timer:focus-within .game-timer-quick-actions", css);
        Assert.Contains("host-game-timer.is-quick-actions-pinned", css);
        Assert.Contains("top: 100%", css);
        Assert.Contains("padding-top: 6px", css);
        Assert.Contains("display: contents", css);
        Assert.DoesNotContain(".host-game-timer {\n    position: relative;", css);
        Assert.Contains("event.target.matches(\".game-timer-adjust\")", markup);
        Assert.Contains("quickTimerPinnedStorageKey", markup);
        Assert.Contains("sessionStorage.getItem(quickTimerPinnedStorageKey)", markup);
        Assert.Contains("sessionStorage.setItem(quickTimerPinnedStorageKey, \"true\")", markup);
        Assert.Contains("sessionStorage.removeItem(quickTimerPinnedStorageKey)", markup);
        Assert.Contains("setQuickTimerControlsPinned(true)", markup);
        Assert.Contains("setQuickTimerControlsPinned(false)", markup);
        Assert.Contains("is-quick-actions-pinned", markup);
        Assert.Contains("document.addEventListener(\"pointerdown\"", markup);
        Assert.DoesNotContain("document.addEventListener(\"pointermove\"", markup);
        Assert.Contains("pointer-events: auto", css);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src")) &&
                Directory.Exists(Path.Combine(directory.FullName, "tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
