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
    public void Lightweight_endpoint_is_idempotent_presence_independent_and_notifies_target_player()
    {
        var pageModel = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "Games",
            "FinalFallback.cshtml.cs"));
        var bridge = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "player-final-fallback-refresh.js"));
        var tagHelper = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "BadWolfQuiz.Web",
            "TagHelpers",
            "PlayerFinalQuestionStageAssetsTagHelper.cs"));

        Assert.Contains("game.Session.SubmitFinalWager(", pageModel, StringComparison.Ordinal);
        Assert.Contains("FinalQuestion.MinimumWager", pageModel, StringComparison.Ordinal);
        Assert.Contains(
            "game.Session.SubmitFinalAnswer(runtimePlayerId, \"-\")",
            pageModel,
            StringComparison.Ordinal);
        Assert.Contains("game.MarkPersistenceChanged();", pageModel, StringComparison.Ordinal);
        Assert.Contains("alreadySubmitted", pageModel, StringComparison.Ordinal);
        Assert.Contains("submissionChanged", pageModel, StringComparison.Ordinal);
        Assert.Contains("allSubmitted", pageModel, StringComparison.Ordinal);
        Assert.Contains("submittedLabel", pageModel, StringComparison.Ordinal);
        Assert.Contains("IHubContext<GameHub> gameHub", pageModel, StringComparison.Ordinal);
        Assert.Contains(".Group(GameHub.GroupName(game.PublicCode))", pageModel, StringComparison.Ordinal);
        Assert.Contains("\"FinalQuestionPlayerFallbackChanged\"", pageModel, StringComparison.Ordinal);
        Assert.Contains("new { playerId = runtimePlayerId.Value }", pageModel, StringComparison.Ordinal);
        Assert.DoesNotContain("SubmitMinimumFinalWagerForPlayer", pageModel, StringComparison.Ordinal);
        Assert.DoesNotContain("SubmitEmptyFinalAnswerForPlayer", pageModel, StringComparison.Ordinal);
        Assert.DoesNotContain("EnsurePlayerInactive", pageModel, StringComparison.Ordinal);
        Assert.DoesNotContain("\"FinalQuestionProgressChanged\"", pageModel, StringComparison.Ordinal);
        Assert.DoesNotContain("PlayersChanged", pageModel, StringComparison.Ordinal);
        Assert.DoesNotContain("GameStatusChanged", pageModel, StringComparison.Ordinal);
        Assert.DoesNotContain("gameHistoryStore", pageModel, StringComparison.Ordinal);
        Assert.DoesNotContain("discord", pageModel, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("FinalQuestionPlayerFallbackChanged", bridge, StringComparison.Ordinal);
        Assert.Contains("finalquestionstatechanged", bridge, StringComparison.Ordinal);
        Assert.Contains(".player-lobby[data-player-id]", bridge, StringComparison.Ordinal);
        Assert.Contains("targetPlayerId === playerId", bridge, StringComparison.Ordinal);
        Assert.Contains("handler(update);", bridge, StringComparison.Ordinal);
        Assert.DoesNotContain("window.location.reload()", bridge, StringComparison.Ordinal);
        Assert.Contains(
            "/js/player-final-fallback-refresh.js?v=1",
            tagHelper,
            StringComparison.Ordinal);
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
