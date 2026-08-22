namespace BadWolfQuiz.Web.Tests;

public sealed class HostNavigationActionGuardRegressionTests
{
    [Fact]
    public void Navigation_guard_assets_are_loaded_for_lobby_and_answer_history()
    {
        var root = FindRepositoryRoot();
        var imports = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "_ViewImports.cshtml"));
        var tagHelper = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "TagHelpers",
            "HostNavigationGuardAssetsTagHelper.cs"));

        Assert.Contains(
            "HostNavigationGuardAssetsTagHelper",
            imports,
            StringComparison.Ordinal);
        Assert.Contains("model is not LobbyModel", tagHelper, StringComparison.Ordinal);
        Assert.Contains("model is not AnswerHistoryModel", tagHelper, StringComparison.Ordinal);
        Assert.Contains(
            "/js/host-navigation-action-guard.js?v=1.22.10",
            tagHelper,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Closed_question_and_review_actions_are_guarded_against_duplicate_navigation()
    {
        var root = FindRepositoryRoot();
        var lobby = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "Games",
            "Lobby.cshtml"));
        var script = ReadGuardScript(root);

        Assert.Contains("data-question-resolved", lobby, StringComparison.Ordinal);
        Assert.Contains("question-review-actions", lobby, StringComparison.Ordinal);
        Assert.Contains(
            "[data-host-gameplay-board] a[data-question-resolved]",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            ".question-review-actions a[href]",
            script,
            StringComparison.Ordinal);
        Assert.Contains("activeNavigation !== null", script, StringComparison.Ordinal);
        Assert.Contains("event.preventDefault();", script, StringComparison.Ordinal);
        Assert.Contains("event.stopImmediatePropagation();", script, StringComparison.Ordinal);
        Assert.Contains("control.disabled = true;", script, StringComparison.Ordinal);
        Assert.Contains("aria-busy", script, StringComparison.Ordinal);
        Assert.Contains("data-navigation-guard-busy", script, StringComparison.Ordinal);
        Assert.Contains(
            "badwolf:host-gameplay-updated",
            script,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Return_to_game_navigation_is_owned_by_the_guard_and_escape_uses_the_same_path()
    {
        var root = FindRepositoryRoot();
        var answerHistory = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "Games",
            "AnswerHistory.cshtml"));
        var script = ReadGuardScript(root);

        Assert.Contains(
            "data-answer-history-back-to-game",
            answerHistory,
            StringComparison.Ordinal);
        Assert.Contains(
            "[data-answer-history-back-to-game]",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "busyApi.navigate(link.href)",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "backToGame.click();",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "document.addEventListener(\"keydown\"",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "event.stopImmediatePropagation();",
            script,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Navigation_guards_restore_controls_after_errors_or_safety_timeout()
    {
        var root = FindRepositoryRoot();
        var script = ReadGuardScript(root);

        Assert.Contains("MutationObserver", script, StringComparison.Ordinal);
        Assert.Contains("game-board-error", script, StringComparison.Ordinal);
        Assert.Contains(
            "window.setTimeout(\n            releaseNavigation,\n            safetyTimeoutMilliseconds);",
            script,
            StringComparison.Ordinal);
        Assert.Contains("restoreLockedControls();", script, StringComparison.Ordinal);
        Assert.Contains("window.BadWolfBusy?.hide?.();", script, StringComparison.Ordinal);
        Assert.Contains("pageshow", script, StringComparison.Ordinal);
    }

    private static string ReadGuardScript(string root) =>
        File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "host-navigation-action-guard.js"))
            .ReplaceLineEndings("\n");

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
