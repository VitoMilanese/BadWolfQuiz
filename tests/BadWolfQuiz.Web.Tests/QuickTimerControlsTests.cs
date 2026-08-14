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
        Assert.Contains("top: 100%", css);
        Assert.Contains("padding-top: 6px", css);
        Assert.Contains("display: contents", css);
        Assert.DoesNotContain(".host-game-timer {\n    position: relative;", css);
        Assert.Contains("event.target.matches(\".game-timer-adjust\")", markup);
        Assert.Contains("pointer-events: auto", css);
        Assert.DoesNotContain("is-quick-actions-pinned", markup);
        Assert.DoesNotContain("is-quick-actions-pinned", css);
        Assert.DoesNotContain("quickTimerPinnedStorageKey", markup);
        Assert.Contains("disableSubmitter = true", markup);
        Assert.Contains("!isQuickAdjustment", markup);
        Assert.Contains("submitter?.matches(\":hover\")", markup);
        Assert.Contains("submitter?.blur();", markup);

        var handler = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "Games",
            "Lobby.cshtml.cs"));
        var addHandlerStart = handler.IndexOf(
            "OnPostAddQuestionTimerTimeAsync",
            StringComparison.Ordinal);
        var addHandlerEnd = handler.IndexOf(
            "OnPostAdvanceRoundAsync",
            addHandlerStart,
            StringComparison.Ordinal);
        var addHandler = handler[addHandlerStart..addHandlerEnd];
        Assert.Contains("if (IsAjaxRequest())", addHandler);
        Assert.Contains("return new JsonResult(new { success = true });", addHandler);
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
