using BadWolfQuiz.Web.Services;

namespace BadWolfQuiz.Web.Tests;

public sealed class PlayerAchievementGameplayExpansion2RegressionTests
{
    private static readonly string[] Codes =
    [
        "AnonymousStake100Profit",
        "AnonymousStakeZeroSave",
        "FourCluesTwoClues",
        "BuzzerPhotoFinishFirst",
        "BuzzerPhotoFinishSecond",
        "KickedAndReturned",
        "FirstToThirdReturn",
        "LateJoiner",
        "AvatarChanged",
        "WebcamEnabled"
    ];

    [Fact]
    public void New_achievement_triggers_are_wired_to_runtime_events()
    {
        var root = FindRepositoryRoot();
        string Read(params string[] parts) => File.ReadAllText(Path.Combine([root, .. parts]));

        var service = Read("src", "BadWolfQuiz.Web", "Services", "PlayerAchievementService.cs");
        var runtime = Read("src", "BadWolfQuiz.Web", "Services", "PlayerAchievementRuntimeState.Expansion.cs");
        var registry = Read("src", "BadWolfQuiz.Web", "Services", "GameSessionRegistry.cs");
        var anonymous = Read("src", "BadWolfQuiz.Web", "Services", "AnonymousSharedWagerWebStore.cs");
        var history = Read("src", "BadWolfQuiz.Web", "Services", "GameHistoryStore.cs");

        foreach (var code in Codes)
        {
            Assert.Contains($"new(\"{code}\"", service, StringComparison.Ordinal);
        }

        Assert.Contains("contribution.Percentage == 100", anonymous, StringComparison.Ordinal);
        Assert.Contains("contribution.Percentage == 0", anonymous, StringComparison.Ordinal);
        Assert.Contains("contribution.Amount > 0", anonymous, StringComparison.Ordinal);
        Assert.Contains("!item.IsForced", anonymous, StringComparison.Ordinal);
        Assert.Contains("RevealedClueCount == 2", history, StringComparison.Ordinal);
        Assert.Contains("attempt.IsCorrect", history, StringComparison.Ordinal);
        Assert.Contains("second.DelayMilliseconds >= 50", runtime, StringComparison.Ordinal);
        Assert.Contains("RecordPlayerKicked", registry, StringComparison.Ordinal);
        Assert.Contains("RecordKickedPlayerReturned", registry, StringComparison.Ordinal);
        Assert.Contains("RecordPlayerDisconnected", registry, StringComparison.Ordinal);
        Assert.Contains("hasPendingTransition", registry, StringComparison.Ordinal);
        Assert.Contains("!hasPendingTransition", registry, StringComparison.Ordinal);
        Assert.Contains("RecordPlayerReconnected", registry, StringComparison.Ordinal);
        Assert.Contains("CurrentRoundIndex != 2", runtime, StringComparison.Ordinal);
        Assert.Contains("CurrentRoundIndex < 1", runtime, StringComparison.Ordinal);
        Assert.Contains("hadAvatar && changedAvatar", registry, StringComparison.Ordinal);
        Assert.Contains("\"WebcamEnabled\"", registry, StringComparison.Ordinal);
        Assert.Contains("GetPendingAchievementCodes", history, StringComparison.Ordinal);
        Assert.Contains("DidCompleteFirstToThirdReturn", history, StringComparison.Ordinal);
    }

    [Fact]
    public void New_achievement_localizations_exist_in_every_supported_resource()
    {
        var root = FindRepositoryRoot();
        var resourceDirectory = Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Resources",
            "Localization");

        foreach (var file in new[]
                 {
                     "AchievementResource.resx",
                     "AchievementResource.uk.resx",
                     "AchievementResource.it.resx",
                     "AchievementResource.ru.resx"
                 })
        {
            var content = File.ReadAllText(Path.Combine(resourceDirectory, file));
            foreach (var code in Codes)
            {
                Assert.Contains($"name=\"{code}_Name\"", content, StringComparison.Ordinal);
                Assert.Contains($"name=\"{code}_Description\"", content, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void Secret_flags_match_requested_achievements()
    {
        Assert.True(PlayerAchievementService.Catalog.Single(item => item.Code == "KickedAndReturned").IsSecret);
        Assert.True(PlayerAchievementService.Catalog.Single(item => item.Code == "FirstToThirdReturn").IsSecret);
        Assert.True(PlayerAchievementService.Catalog.Single(item => item.Code == "LateJoiner").IsSecret);
        Assert.False(PlayerAchievementService.Catalog.Single(item => item.Code == "AnonymousStake100Profit").IsSecret);
        Assert.False(PlayerAchievementService.Catalog.Single(item => item.Code == "AnonymousStakeZeroSave").IsSecret);
        Assert.False(PlayerAchievementService.Catalog.Single(item => item.Code == "FourCluesTwoClues").IsSecret);
        Assert.False(PlayerAchievementService.Catalog.Single(item => item.Code == "BuzzerPhotoFinishFirst").IsSecret);
        Assert.False(PlayerAchievementService.Catalog.Single(item => item.Code == "BuzzerPhotoFinishSecond").IsSecret);
        Assert.False(PlayerAchievementService.Catalog.Single(item => item.Code == "AvatarChanged").IsSecret);
        Assert.False(PlayerAchievementService.Catalog.Single(item => item.Code == "WebcamEnabled").IsSecret);
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
