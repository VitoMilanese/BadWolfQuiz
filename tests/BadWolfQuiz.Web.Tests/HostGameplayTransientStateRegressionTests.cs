namespace BadWolfQuiz.Web.Tests;

public sealed class HostGameplayTransientStateRegressionTests
{
    [Fact]
    public void Replaced_question_selection_forms_use_a_delegated_ajax_fallback_before_the_submit_guard()
    {
        var root = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "host-gameplay-transient-state-recovery.js"));
        var assets = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "TagHelpers",
            "HostGameplaySubmitGuardAssetsTagHelper.cs"));

        Assert.Contains(".question-selection-form", script, StringComparison.Ordinal);
        Assert.Contains("event.defaultPrevented", script, StringComparison.Ordinal);
        Assert.Contains("event.stopImmediatePropagation()", script, StringComparison.Ordinal);
        Assert.Contains("new FormData(form)", script, StringComparison.Ordinal);
        Assert.Contains("await fetch(form.action", script, StringComparison.Ordinal);
        Assert.Contains("await window.BadWolfHostGameplay.refresh()", script, StringComparison.Ordinal);
        Assert.Contains("X-Requested-With", script, StringComparison.Ordinal);

        var recoveryIndex = assets.IndexOf(
            "host-gameplay-transient-state-recovery.js?v=1",
            StringComparison.Ordinal);
        var submitGuardIndex = assets.IndexOf(
            "host-gameplay-submit-guard.js?v=5",
            StringComparison.Ordinal);
        var antiforgeryRecoveryIndex = assets.IndexOf(
            "host-question-selection-recovery.js?v=1",
            StringComparison.Ordinal);

        Assert.True(recoveryIndex >= 0);
        Assert.True(submitGuardIndex > recoveryIndex);
        Assert.True(antiforgeryRecoveryIndex > submitGuardIndex);
    }

    [Fact]
    public void Peer_rated_navigation_clears_stale_player_attempt_and_return_transition_state()
    {
        var root = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "host-gameplay-transient-state-recovery.js"));

        Assert.Contains("peer-rated-all-player-active", script, StringComparison.Ordinal);
        Assert.Contains("peer-rated-question-shell-active", script, StringComparison.Ordinal);
        Assert.Contains("peer-rated-returning-to-board", script, StringComparison.Ordinal);
        Assert.Contains("question-answering-player", script, StringComparison.Ordinal);
        Assert.Contains("question-attempted-player", script, StringComparison.Ordinal);
        Assert.Contains("persistentBoard.hidden === false", script, StringComparison.Ordinal);
        Assert.Contains("badwolf:host-gameplay-updated", script, StringComparison.Ordinal);
        Assert.Contains("badwolf:host-shell-mounted", script, StringComparison.Ordinal);
        Assert.Contains("MutationObserver", script, StringComparison.Ordinal);
        Assert.Contains("attributeFilter: [\"class\", \"hidden\"]", script, StringComparison.Ordinal);
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
