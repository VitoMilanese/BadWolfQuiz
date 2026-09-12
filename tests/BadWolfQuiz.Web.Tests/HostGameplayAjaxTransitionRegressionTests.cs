namespace BadWolfQuiz.Web.Tests;

public sealed class HostGameplayAjaxTransitionRegressionTests
{
    [Fact]
    public void Host_multiple_choice_no_answer_uses_ajax_host_refresh()
    {
        var root = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "host-multiple-choice-no-reload.js"));
        var assets = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "TagHelpers",
            "HostMultipleChoiceAssetsTagHelper.cs"));

        Assert.Contains("host-multiple-choice-panel", script, StringComparison.Ordinal);
        Assert.Contains("ResolveQuestion", script, StringComparison.Ordinal);
        Assert.Contains("event.preventDefault()", script, StringComparison.Ordinal);
        Assert.Contains("new FormData(form)", script, StringComparison.Ordinal);
        Assert.Contains("await fetch(form.action", script, StringComparison.Ordinal);
        Assert.Contains("await window.BadWolfHostGameplay.refresh()", script, StringComparison.Ordinal);
        Assert.DoesNotContain("window.location.reload", script, StringComparison.Ordinal);
        Assert.Contains("host-multiple-choice-no-reload.js?v=1", assets, StringComparison.Ordinal);
    }

    [Fact]
    public void Peer_rated_return_to_board_disables_legacy_full_reload_fallback_before_polish_runs()
    {
        var root = FindRepositoryRoot();
        var guard = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "peer-rated-return-no-reload.js"));
        var assets = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "TagHelpers",
            "PeerRatedAllPlayerAssetsTagHelper.cs"));
        var runtime = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "peer-rated-all-player-question.js"));

        Assert.Contains(
            "window.badWolfPeerRatedReturnFetchWrapped = true",
            guard,
            StringComparison.Ordinal);
        Assert.Contains("const originalFetch = window.fetch.bind(window)", guard, StringComparison.Ordinal);
        Assert.Contains("setReturningToBoard(true)", guard, StringComparison.Ordinal);
        Assert.DoesNotContain("window.location.reload", guard, StringComparison.Ordinal);
        Assert.Contains(
            "if (handler === \"ReturnToBoard\")",
            runtime,
            StringComparison.Ordinal);
        Assert.Contains(
            "await window.BadWolfHostGameplay?.refresh?.()",
            runtime,
            StringComparison.Ordinal);

        var guardIndex = assets.IndexOf(
            "peer-rated-return-no-reload.js?v=2",
            StringComparison.Ordinal);
        var polishIndex = assets.IndexOf(
            "peer-rated-all-player-polish.js?v=3",
            StringComparison.Ordinal);
        Assert.True(guardIndex >= 0);
        Assert.True(polishIndex > guardIndex);
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
