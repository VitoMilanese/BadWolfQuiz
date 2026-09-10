namespace BadWolfQuiz.Web.Tests;

public sealed class PlayerAchievementTriggerRegressionTests
{
    [Fact]
    public void Expanded_achievements_are_wired_to_site_and_game_events()
    {
        var root = FindRepositoryRoot();
        string Read(params string[] parts) => File.ReadAllText(Path.Combine([root, .. parts]));

        var service = Read("src", "BadWolfQuiz.Web", "Services", "PlayerAchievementService.cs");
        var history = Read("src", "BadWolfQuiz.Web", "Services", "GameHistoryStore.cs");
        var minigame = Read("src", "BadWolfQuiz.Web", "Hubs", "MinigameHub.cs");
        var password = Read("src", "BadWolfQuiz.Web", "Pages", "Account", "ChangePassword.cshtml.cs");
        var question = Read("src", "BadWolfQuiz.Web", "Pages", "AskQuestion.cshtml.cs");
        var github = Read("src", "BadWolfQuiz.Web", "Pages", "ProjectGitHub.cshtml.cs");
        var githubTagHelper = Read("src", "BadWolfQuiz.Web", "TagHelpers", "GitHubAchievementLinkTagHelper.cs");
        var playerTagHelper = Read("src", "BadWolfQuiz.Web", "TagHelpers", "PlayerAchievementsTagHelper.cs");

        Assert.Contains("Registered", service);
        Assert.Contains("OwnQuizWithOthers", service);
        Assert.Contains("PublicQuizGuest", service);
        Assert.Contains("QuizRated", service);
        Assert.Contains("DeveloperReplied", service);
        Assert.Contains("OrderByDescending(item => item.Progress.IsUnlocked)", service);

        Assert.Contains("AllInCorrect", history);
        Assert.Contains("AllInWrong", history);
        Assert.Contains("submission.MaximumWager", history);
        Assert.Contains("highestQuestionValue", history);
        Assert.Contains("SilentRoundGain", service);
        Assert.Contains("roundScoreDelta > 0", history);
        Assert.Contains("item.PlayerCount >= 3", service);

        Assert.Contains("SoloAi", minigame);
        Assert.Contains("RoomCreatorWin", minigame);
        Assert.Contains("state.PlayerNumber == 1", minigame);
        Assert.Contains("state.WinnerPlayerNumber == 1", minigame);

        Assert.Contains("PasswordChanged", password);
        Assert.Contains("UserQuestionAccountLink", question);
        Assert.Contains("DeveloperContacted", question);
        Assert.Contains("GitHubVisitor", github);
        Assert.Contains("/project/github", githubTagHelper);
        Assert.Contains("ContributorRecognition.IsContributor", playerTagHelper);
        Assert.Contains("AdoptHostNicknameHistoryAsync", service);
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
