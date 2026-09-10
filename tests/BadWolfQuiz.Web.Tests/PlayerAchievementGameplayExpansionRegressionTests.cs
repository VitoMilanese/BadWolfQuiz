namespace BadWolfQuiz.Web.Tests;

public sealed class PlayerAchievementGameplayExpansionRegressionTests
{
    [Fact]
    public void New_gameplay_achievements_are_catalogued_tracked_and_persisted()
    {
        var root = FindRepositoryRoot();
        string Read(params string[] parts) => File.ReadAllText(Path.Combine([root, .. parts]));

        var service = Read("src", "BadWolfQuiz.Web", "Services", "PlayerAchievementService.cs");
        var history = Read("src", "BadWolfQuiz.Web", "Services", "GameHistoryStore.cs");
        var runtime = Read("src", "BadWolfQuiz.Web", "Services", "PlayerAchievementRuntimeState.cs");
        var registration = Read("src", "BadWolfQuiz.Web", "Services", "GameSessionRegistration.cs");
        var activeGame = Read("src", "BadWolfQuiz.Web", "Services", "ActiveGamePersistenceService.cs");

        foreach (var code in new[]
                 {
                     "FirstPick",
                     "SecondRoundFirstPick",
                     "LastRoundComebackWin",
                     "FinalLeaderZero",
                     "EveryCategoryAttempt",
                     "EveryCategoryCorrect",
                     "SilentRound"
                 })
        {
            Assert.Contains($"new(\"{code}\"", service, StringComparison.Ordinal);
            Assert.Contains($"\"{code}\"", history, StringComparison.Ordinal);
        }

        Assert.Contains("new(\"FirstPick\", \"🏁\", false", service, StringComparison.Ordinal);
        Assert.Contains("new(\"SecondRoundFirstPick\", \"2️⃣\", false", service, StringComparison.Ordinal);
        Assert.Contains("new(\"LastRoundComebackWin\", \"🐺\", true", service, StringComparison.Ordinal);
        Assert.Contains("new(\"FinalLeaderZero\", \"📉\", true", service, StringComparison.Ordinal);
        Assert.Contains("new(\"EveryCategoryAttempt\", \"🧭\", true", service, StringComparison.Ordinal);
        Assert.Contains("new(\"EveryCategoryCorrect\", \"🧠\", true", service, StringComparison.Ordinal);
        Assert.Contains("new(\"SilentRound\", \"🤐\", true", service, StringComparison.Ordinal);

        Assert.Contains("GetQuestionOpenSequence", history, StringComparison.Ordinal);
        Assert.Contains("WasLowestScore: true", history, StringComparison.Ordinal);
        Assert.Contains("categoryIds.Length >= 5", history, StringComparison.Ordinal);
        Assert.Contains("player.Score - finalDelta", history, StringComparison.Ordinal);
        Assert.Contains("HasBuzzerPressInRound", history, StringComparison.Ordinal);
        Assert.Contains("PresentPlayerIds.Contains(player.Id)", history, StringComparison.Ordinal);

        Assert.Contains("RecordQuestionOpened", runtime, StringComparison.Ordinal);
        Assert.Contains("RoundFirstPickSnapshot", runtime, StringComparison.Ordinal);
        Assert.Contains("RoundBuzzerPressSnapshot", runtime, StringComparison.Ordinal);
        Assert.Contains("RecordBuzzerRace", runtime, StringComparison.Ordinal);
        Assert.Contains("PlayerAchievementRuntimeState.RecordQuestionOpened(this, question);", registration, StringComparison.Ordinal);
        Assert.Contains("PlayerAchievementRuntimeState.RecordBuzzerRace(this, value);", registration, StringComparison.Ordinal);
        Assert.Contains("PlayerAchievementRuntimeState.Capture(game)", activeGame, StringComparison.Ordinal);
        Assert.Contains("PlayerAchievementRuntimeState.Restore(", activeGame, StringComparison.Ordinal);
    }

    [Fact]
    public void New_gameplay_achievement_localizations_exist_in_every_supported_resource()
    {
        var root = FindRepositoryRoot();
        var resourceDirectory = Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Resources",
            "Localization");
        var files = new[]
        {
            "AchievementResource.resx",
            "AchievementResource.uk.resx",
            "AchievementResource.it.resx",
            "AchievementResource.ru.resx"
        };
        var codes = new[]
        {
            "FirstPick",
            "SecondRoundFirstPick",
            "LastRoundComebackWin",
            "FinalLeaderZero",
            "EveryCategoryAttempt",
            "EveryCategoryCorrect",
            "SilentRound"
        };

        foreach (var file in files)
        {
            var content = File.ReadAllText(Path.Combine(resourceDirectory, file));
            foreach (var code in codes)
            {
                Assert.Contains($"name=\"{code}_Name\"", content, StringComparison.Ordinal);
                Assert.Contains($"name=\"{code}_Description\"", content, StringComparison.Ordinal);
            }
        }
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
