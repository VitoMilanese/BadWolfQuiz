namespace BadWolfQuiz.Web.Tests;

public sealed class PlayerAchievementExpansion3RegressionTests
{
    [Fact]
    public void Completion_pipeline_tracks_reward_modifiers_peer_ratings_and_previous_winner()
    {
        var root = FindRepositoryRoot();
        var history = File.ReadAllText(Path.Combine(
            root, "src", "BadWolfQuiz.Web", "Services", "GameHistoryStore.cs"));

        Assert.Contains("AnswerRewardModifier.Double => \"DoubleReward\"", history);
        Assert.Contains("AnswerRewardModifier.Half => \"HalfReward\"", history);
        Assert.Contains("RecordPeerMaximumRatingEventsAsync", history);
        Assert.Contains("item.Stars == 5", history);
        Assert.Contains("UnlockBeatPreviousWinnerAsync", history);
        Assert.Contains("current.Player.Score < currentWinningScore", history);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "BadWolfQuiz.sln")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException();
    }
}
