namespace BadWolfQuiz.Web.Tests;

public sealed class AchievementUnlockNotificationRegressionTests
{
    [Fact]
    public void Unlock_notifications_are_registered_persisted_and_sent_over_the_existing_game_hub()
    {
        var root = FindRepositoryRoot();
        string Read(params string[] parts) => File.ReadAllText(Path.Combine([root, .. parts]));

        var program = Read("src", "BadWolfQuiz.Web", "Program.cs");
        var service = Read(
            "src",
            "BadWolfQuiz.Web",
            "Services",
            "AchievementUnlockNotificationBackgroundService.cs");
        var runtime = Read(
            "src",
            "BadWolfQuiz.Web",
            "Services",
            "PlayerAchievementRuntimeState.Expansion.cs");

        Assert.Contains(
            "AddHostedService<AchievementUnlockNotificationBackgroundService>()",
            program,
            StringComparison.Ordinal);
        Assert.Contains("UnlockPlayerAsync(", service, StringComparison.Ordinal);
        Assert.Contains("TryUnlockCustomAsync(", service, StringComparison.Ordinal);
        Assert.Contains("SaveChangesAsync(cancellationToken)", service, StringComparison.Ordinal);
        Assert.Contains("SendAsync(\"AchievementUnlocked\"", service, StringComparison.Ordinal);
        Assert.Contains("GameHub.GroupName(game.PublicCode)", service, StringComparison.Ordinal);
        Assert.Contains("HostCustomAchievementService.BuildCode", service, StringComparison.Ordinal);
        Assert.Contains("achievement.ArtworkUrl", service, StringComparison.Ordinal);
        Assert.Contains("BackfillLiveUnlockSourcesAsync", service, StringComparison.Ordinal);
        Assert.Contains("SourceGameSessionId", service, StringComparison.Ordinal);

        Assert.Contains("TryMarkAchievementUnlockNotified", runtime, StringComparison.Ordinal);
        Assert.Contains("GetNotifiedAchievementCodes", runtime, StringComparison.Ordinal);
        Assert.Contains("NotifiedUnlocks", runtime, StringComparison.Ordinal);
        Assert.Contains("snapshot.NotifiedUnlocks", runtime, StringComparison.Ordinal);
    }

    [Fact]
    public void Player_and_host_clients_queue_and_deduplicate_unlock_animations()
    {
        var root = FindRepositoryRoot();
        string Read(params string[] parts) => File.ReadAllText(Path.Combine([root, .. parts]));

        var tagHelper = Read(
            "src",
            "BadWolfQuiz.Web",
            "TagHelpers",
            "AchievementUnlockNotificationAssetsTagHelper.cs");
        var script = Read(
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "achievement-unlock-notifications.js");
        var styles = Read(
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "css",
            "achievement-unlock-notifications.css");
        var imports = Read(
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "_ViewImports.cshtml");

        Assert.Contains("/Player/Lobby", tagHelper, StringComparison.Ordinal);
        Assert.Contains("/Admin/Games/Lobby", tagHelper, StringComparison.Ordinal);
        Assert.Contains("/Admin/Games/RoundIntro", tagHelper, StringComparison.Ordinal);
        Assert.Contains("/Admin/Games/RunningRoundIntro", tagHelper, StringComparison.Ordinal);
        Assert.Contains("/Admin/Games/FinalQuestionTransition", tagHelper, StringComparison.Ordinal);
        Assert.Contains("achievement-unlock-notifications.css?v=3", tagHelper, StringComparison.Ordinal);
        Assert.Contains("achievement-unlock-notifications.js?v=3", tagHelper, StringComparison.Ordinal);
        Assert.Contains(
            "@addTagHelper BadWolfQuiz.Web.TagHelpers.AchievementUnlockNotificationAssetsTagHelper, BadWolfQuiz.Web",
            imports,
            StringComparison.Ordinal);

        Assert.Contains("new signalR.HubConnectionBuilder()", script, StringComparison.Ordinal);
        Assert.Contains("connection.on(\"AchievementUnlocked\"", script, StringComparison.Ordinal);
        Assert.Contains("connection.invoke(\"JoinSession\", gameCode)", script, StringComparison.Ordinal);
        Assert.Contains("connection.invoke(\"RegisterHostSession\", gameCode)", script, StringComparison.Ordinal);
        Assert.Contains("const playerQueue = []", script, StringComparison.Ordinal);
        Assert.Contains("const hostQueues = new Map()", script, StringComparison.Ordinal);
        Assert.Contains(".scoreboard-player[data-player-id]", script, StringComparison.Ordinal);
        Assert.Contains("sessionStorage", script, StringComparison.Ordinal);
        Assert.Contains("stack.replaceChildren(card)", script, StringComparison.Ordinal);
        Assert.Contains("document.body.appendChild(burst)", script, StringComparison.Ordinal);
        Assert.Contains("syncHostBurstToCard", script, StringComparison.Ordinal);
        Assert.Contains("BadWolfAchievementUnlockNotifications", script, StringComparison.Ordinal);
        Assert.Contains("await wait(7200)", script, StringComparison.Ordinal);
        Assert.Contains("await wait(2400)", script, StringComparison.Ordinal);

        Assert.Contains(".achievement-unlock-player-card", styles, StringComparison.Ordinal);
        Assert.Contains(".achievement-unlock-host-burst", styles, StringComparison.Ordinal);
        Assert.Contains("z-index: 3600;", styles, StringComparison.Ordinal);
        Assert.Contains("prefers-reduced-motion: reduce", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void Live_evaluation_covers_safe_mid_game_conditions_and_defers_completion_only_conditions()
    {
        var root = FindRepositoryRoot();
        var service = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Services",
            "AchievementUnlockNotificationBackgroundService.cs"));

        Assert.Contains("PlayerAchievementMetric.CorrectAnswers", service, StringComparison.Ordinal);
        Assert.Contains("PlayerAchievementMetric.BestCorrectStreak", service, StringComparison.Ordinal);
        Assert.Contains("PlayerAchievementMetric.TaggedAnswers", service, StringComparison.Ordinal);
        Assert.Contains("PlayerAchievementMetric.AudioQuestionAnswers", service, StringComparison.Ordinal);
        Assert.Contains("PlayerAchievementMetric.VideoQuestionAnswers", service, StringComparison.Ordinal);
        Assert.Contains("GetPendingAchievementCodes", service, StringComparison.Ordinal);
        Assert.Contains("\"FirstPick\"", service, StringComparison.Ordinal);
        Assert.Contains("\"SecondRoundFirstPick\"", service, StringComparison.Ordinal);
        Assert.Contains("\"AllInCorrect\"", service, StringComparison.Ordinal);
        Assert.Contains("\"AllInWrong\"", service, StringComparison.Ordinal);
        Assert.Contains("\"DoubleReward\"", service, StringComparison.Ordinal);
        Assert.Contains("\"HalfReward\"", service, StringComparison.Ordinal);
        Assert.Contains("\"FourCluesTwoClues\"", service, StringComparison.Ordinal);
        Assert.Contains("\"EveryCategoryAttempt\"", service, StringComparison.Ordinal);
        Assert.Contains("\"EveryCategoryCorrect\"", service, StringComparison.Ordinal);

        Assert.Contains("StoredGameSessionStatus.Finished", service, StringComparison.Ordinal);
        Assert.Contains("IsNewInCurrentGame", service, StringComparison.Ordinal);
        Assert.Contains("runtime.Status == RuntimeGameSessionStatus.Completed", service, StringComparison.Ordinal);
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
