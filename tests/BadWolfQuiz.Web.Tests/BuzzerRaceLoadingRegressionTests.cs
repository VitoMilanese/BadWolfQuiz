namespace BadWolfQuiz.Web.Tests;

public sealed class BuzzerRaceLoadingRegressionTests
{
    [Fact]
    public void WinnerImmediatelyBroadcastsCollectingStateToHostOnly()
    {
        var root = FindRepositoryRoot();
        var hub = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "Hubs", "GameHub.cs"));

        var hostBroadcast = hub.IndexOf(".Group(HostGroupName(claim.Game.PublicCode))", StringComparison.Ordinal);
        var collectionDelay = hub.IndexOf("await Task.Delay(TimeSpan.FromSeconds(1));", StringComparison.Ordinal);
        var finalBroadcast = hub.IndexOf(".Group(GroupName(claim.Game.PublicCode))", collectionDelay, StringComparison.Ordinal);

        Assert.True(hostBroadcast >= 0);
        Assert.True(collectionDelay > hostBroadcast);
        Assert.True(finalBroadcast > collectionDelay);
        Assert.Contains("isBuzzerRaceCollecting: true", hub, StringComparison.Ordinal);
        Assert.Contains("isCollecting = isBuzzerRaceCollecting", hub, StringComparison.Ordinal);
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
    public void CollectingPayloadShowsAnimatedOverlayWithoutPlayerNames()
    {
        var root = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "wwwroot", "js", "buzzer-race-loading.js"));
        var styles = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "wwwroot", "css", "buzzer-race-loading.css"));

        Assert.Contains("event.detail?.isCollecting", script, StringComparison.Ordinal);
        Assert.Contains("event.stopPropagation();", script, StringComparison.Ordinal);
        Assert.Contains("data-buzzer-race-collecting", script, StringComparison.Ordinal);
        Assert.Contains("container.replaceChildren(createCollectingOverlay())", script, StringComparison.Ordinal);
        Assert.DoesNotContain("winnerPlayerName", script, StringComparison.Ordinal);
        Assert.DoesNotContain("latePlayers", script, StringComparison.Ordinal);
        Assert.Contains("buzzer-race-loading-spin", styles, StringComparison.Ordinal);
        Assert.Contains("buzzer-race-loading-spin-reverse", styles, StringComparison.Ordinal);
        Assert.Contains("prefers-reduced-motion: reduce", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void HostCollectingStateRendersBeforeRefreshAndFinalResultReusesOverlay()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "Pages", "Admin", "Games", "Lobby.cshtml"));

        var collectingBranch = markup.IndexOf("if (update.buzzerRace?.isCollecting)", StringComparison.Ordinal);
        var gameplayRefresh = markup.IndexOf("void requestHostGameplayRefresh()", collectingBranch, StringComparison.Ordinal);

        Assert.True(collectingBranch >= 0);
        Assert.True(gameplayRefresh > collectingBranch);
        Assert.Contains("data-buzzer-race-collecting", markup, StringComparison.Ordinal);
        Assert.Contains("delete overlay.dataset.buzzerRaceCollecting", markup, StringComparison.Ordinal);
        Assert.Contains("overlay.replaceChildren(card);", markup, StringComparison.Ordinal);
        Assert.Contains("if (!overlay.isConnected)", markup, StringComparison.Ordinal);
        Assert.Contains("preserveCollectingBuzzerOverlay", markup, StringComparison.Ordinal);
        Assert.Contains("currentTransient && !preserveCollectingBuzzerOverlay", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadingAssetsAreAvailableOnHostLobby()
    {
        var root = FindRepositoryRoot();
        var layout = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "Pages", "Shared", "_Layout.cshtml"));

        Assert.Contains("~/css/buzzer-race-loading.css", layout, StringComparison.Ordinal);
        Assert.Contains("~/js/buzzer-race-loading.js", layout, StringComparison.Ordinal);
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
