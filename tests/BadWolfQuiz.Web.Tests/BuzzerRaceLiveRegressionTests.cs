namespace BadWolfQuiz.Web.Tests;

public sealed class BuzzerRaceLiveRegressionTests
{
    [Fact]
    public void EveryAcceptedPressBroadcastsLiveRaceToHostBeforeLatePressReturns()
    {
        var root = FindRepositoryRoot();
        var hub = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "Hubs", "GameHub.cs"));

        var hostBroadcast = hub.IndexOf(".Group(HostGroupName(claim.Game.PublicCode))", StringComparison.Ordinal);
        var latePressReturn = hub.IndexOf("if (!claim.IsWinner)", hostBroadcast, StringComparison.Ordinal);
        var collectionDelay = hub.IndexOf("await Task.Delay(TimeSpan.FromSeconds(1));", latePressReturn, StringComparison.Ordinal);
        var finalBroadcast = hub.IndexOf(".Group(GroupName(claim.Game.PublicCode))", collectionDelay, StringComparison.Ordinal);

        Assert.True(hostBroadcast >= 0);
        Assert.True(latePressReturn > hostBroadcast);
        Assert.True(collectionDelay > latePressReturn);
        Assert.True(finalBroadcast > collectionDelay);
        Assert.Contains("isBuzzerRaceCollecting: true", hub, StringComparison.Ordinal);
    }

    [Fact]
    public void Host_tools_can_cancel_current_buzzer_claim_and_reopen_it_live()
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
        var pageModel = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "Games",
            "Lobby.cshtml.cs"));
        var registry = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Services",
            "GameSessionRegistry.cs"));
        var ukrainian = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Resources",
            "Localization",
            "SharedResource.uk.resx"));

        Assert.Contains("asp-page-handler=\"CancelBuzzerClaim\"", markup);
        Assert.Contains("data-cancel-buzzer-claim-form", markup);
        Assert.Contains(
            "QuestionBuzzerStatus.Claimed ? \"hidden\" : null",
            markup);
        Assert.Contains(
            "cancelBuzzerClaimForm.hidden = update.status !== \"claimed\";",
            markup);
        Assert.Contains(
            "cancelBuzzerClaimForm?.addEventListener(\"submit\", async event =>",
            markup);
        Assert.Contains("event.preventDefault();", markup);
        Assert.Contains(
            "await submitGameControl(\n                        cancelBuzzerClaimForm,",
            markup);
        Assert.Contains(
            ".closest(\"details\")\n                        ?.removeAttribute(\"open\")",
            markup);
        Assert.Contains(
            "OnPostCancelBuzzerClaimAsync(",
            pageModel);
        Assert.Contains(
            "CancelCurrentQuestionBuzzerClaim(game.PublicCode)",
            pageModel);
        Assert.Contains("await BroadcastBuzzerAsync(game", pageModel);
        Assert.Contains("await BroadcastTimerAsync(game", pageModel);
        Assert.Contains(
            "if (IsAjaxRequest())",
            pageModel);
        Assert.Contains(
            "return new JsonResult(new",
            pageModel);
        Assert.Contains(
            "return BadRequest(new",
            pageModel);
        Assert.Contains(
            "public RuntimeQuestion? CancelCurrentQuestionBuzzerClaim(",
            registry);
        Assert.Contains("game.BuzzerRace = null;", registry);
        Assert.Contains(
            "<value>Скасувати натискання буззера</value>",
            ukrainian);
    }

    [Fact]
    public void ExistingOneSecondLatePressWindowRemainsUnchanged()
    {
        var root = FindRepositoryRoot();
        var registry = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "Services", "GameSessionRegistry.cs"));
        var hub = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "Hubs", "GameHub.cs"));

        Assert.Contains("BuzzerRaceWindow = TimeSpan.FromSeconds(1)", registry, StringComparison.Ordinal);
        Assert.Contains("await Task.Delay(TimeSpan.FromSeconds(1));", hub, StringComparison.Ordinal);
    }

    [Fact]
    public void HostOverlayShowsWinnerAndAddsLatePlayersAsTheyArrive()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "Pages", "Admin", "Games", "Lobby.cshtml"));

        Assert.Contains("race.winnerPlayerName", markup, StringComparison.Ordinal);
        Assert.Contains("previousLatePlayers", markup, StringComparison.Ordinal);
        Assert.Contains("const latePlayerMap = new Map(previousLatePlayers)", markup, StringComparison.Ordinal);
        Assert.Contains("item.dataset.buzzerRacePlayerId = latePlayer.playerId", markup, StringComparison.Ordinal);
        Assert.Contains("!previousLatePlayers.has(latePlayer.playerId)", markup, StringComparison.Ordinal);
        Assert.Contains("left.delayMilliseconds - right.delayMilliseconds", markup, StringComparison.Ordinal);
        Assert.Contains("latePlayer.delayMilliseconds.toString()", markup, StringComparison.Ordinal);
        Assert.Contains("buzzer-race-live-entry-new", markup, StringComparison.Ordinal);
        Assert.Contains("sourceQuestionId: update.sourceQuestionId", markup, StringComparison.Ordinal);
        Assert.Contains("if (update.buzzerRace?.isCollecting)", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void LiveRaceSurvivesRefreshAndNeverExtendsPastThreeSeconds()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "Pages", "Admin", "Games", "Lobby.cshtml"));

        Assert.Contains("const buzzerOverlayLifetimeMilliseconds = 3000", markup, StringComparison.Ordinal);
        Assert.Contains("overlay.dataset.buzzerRaceShownAt = Date.now().toString()", markup, StringComparison.Ordinal);
        Assert.Contains("buzzerOverlayLifetimeMilliseconds - elapsed", markup, StringComparison.Ordinal);
        Assert.Contains("data-buzzer-race-collecting", markup, StringComparison.Ordinal);
        Assert.Contains("preserveCollectingBuzzerOverlay", markup, StringComparison.Ordinal);
        Assert.Contains("buzzerRaceFinalized", markup, StringComparison.Ordinal);
        Assert.Contains("delete overlay.dataset.buzzerRaceCollecting", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void LiveRaceUsesArrivalAnimationWithReducedMotionFallback()
    {
        var root = FindRepositoryRoot();
        var layout = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "Pages", "Shared", "_Layout.cshtml"));
        var styles = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "wwwroot", "css", "buzzer-race-live.css"));

        Assert.Contains("~/css/buzzer-race-live.css", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("buzzer-race-loading", layout, StringComparison.Ordinal);
        Assert.Contains("buzzer-race-live-arrival", styles, StringComparison.Ordinal);
        Assert.Contains("prefers-reduced-motion: reduce", styles, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(root, "src", "BadWolfQuiz.Web", "wwwroot", "js", "buzzer-race-loading.js")));
        Assert.False(File.Exists(Path.Combine(root, "src", "BadWolfQuiz.Web", "wwwroot", "css", "buzzer-race-loading.css")));
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
