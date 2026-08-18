namespace BadWolfQuiz.Web.Tests;

public sealed class FinalQuestionHostFallbackAjaxRegressionTests
{
    [Fact]
    public void Host_bootstrap_guards_first_fallback_click_before_asset_is_ready()
    {
        var bootstrap = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "gameplay-escape-shortcuts.js"));

        Assert.Contains(
            "/js/final-player-fallback-actions.js?v=3",
            bootstrap,
            StringComparison.Ordinal);
        Assert.Contains("pendingFinalFallbackClicks", bootstrap, StringComparison.Ordinal);
        Assert.Contains(
            "window.badWolfFinalPlayerFallbackActionsInitialized",
            bootstrap,
            StringComparison.Ordinal);
        Assert.Contains("replayPendingFinalFallbackClicks", bootstrap, StringComparison.Ordinal);
        Assert.Contains("event.stopImmediatePropagation()", bootstrap, StringComparison.Ordinal);

        var fallbackAssetIndex = bootstrap.IndexOf(
            "/js/final-player-fallback-actions.js?v=3",
            StringComparison.Ordinal);
        var hostTargetIndex = bootstrap.IndexOf(
            "const hostGameplayTarget",
            StringComparison.Ordinal);
        Assert.True(fallbackAssetIndex >= 0);
        Assert.True(hostTargetIndex > fallbackAssetIndex);
    }

    [Fact]
    public void Final_fallback_actions_queue_rapid_clicks_and_update_rows_locally()
    {
        var script = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "final-player-fallback-actions.js"));

        Assert.Contains("SubmitMinimumFinalWager", script, StringComparison.Ordinal);
        Assert.Contains("SubmitEmptyFinalAnswer", script, StringComparison.Ordinal);
        Assert.Contains("/Admin/Games/FinalFallback", script, StringComparison.Ordinal);
        Assert.Contains("event.stopImmediatePropagation()", script, StringComparison.Ordinal);
        Assert.Contains("let fallbackQueue = Promise.resolve()", script, StringComparison.Ordinal);
        Assert.Contains("fallbackQueue = fallbackQueue.then", script, StringComparison.Ordinal);
        Assert.Contains("status.textContent = result.submittedLabel", script, StringComparison.Ordinal);
        Assert.Contains("form.remove()", script, StringComparison.Ordinal);
        Assert.Contains("lockButton?.removeAttribute(\"disabled\")", script, StringComparison.Ordinal);
        Assert.DoesNotContain("suppressedHostRefreshes", script, StringComparison.Ordinal);
        Assert.DoesNotContain("BadWolfHostGameplay.refresh", script, StringComparison.Ordinal);
        Assert.DoesNotContain("window.location.reload()", script, StringComparison.Ordinal);
        Assert.DoesNotContain("window.alert", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Lightweight_endpoint_is_idempotent_and_does_not_broadcast_progress()
    {
        var pageModel = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "Games",
            "FinalFallback.cshtml.cs"));

        Assert.Contains("SubmitMinimumFinalWagerForPlayer", pageModel, StringComparison.Ordinal);
        Assert.Contains("SubmitEmptyFinalAnswerForPlayer", pageModel, StringComparison.Ordinal);
        Assert.Contains("alreadySubmitted", pageModel, StringComparison.Ordinal);
        Assert.Contains("allSubmitted", pageModel, StringComparison.Ordinal);
        Assert.Contains("submittedLabel", pageModel, StringComparison.Ordinal);
        Assert.DoesNotContain("FinalQuestionProgressChanged", pageModel, StringComparison.Ordinal);
        Assert.DoesNotContain("IHubContext", pageModel, StringComparison.Ordinal);
        Assert.DoesNotContain("SendAsync", pageModel, StringComparison.Ordinal);
        Assert.DoesNotContain("gameHistoryStore", pageModel, StringComparison.Ordinal);
        Assert.DoesNotContain("discord", pageModel, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PlayersChanged", pageModel, StringComparison.Ordinal);
        Assert.DoesNotContain("GameStatusChanged", pageModel, StringComparison.Ordinal);
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
