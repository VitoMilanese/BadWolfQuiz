using BadWolfQuiz.Web.Services;

namespace BadWolfQuiz.Web.Tests;

public sealed class PlayerStatisticsServiceTests
{
    [Fact]
    public void Build_combines_case_insensitive_names_and_calculates_lifetime_results()
    {
        var firstGame = new DateTime(2026, 7, 1, 20, 0, 0, DateTimeKind.Utc);
        var secondGame = firstGame.AddDays(7);
        PlayerGameStatisticsSource[] games =
        [
            new(1, 10, "Player X", 300, firstGame),
            new(2, 11, " player x ", -100, secondGame),
            new(3, 11, "Other", 50, secondGame)
        ];
        PlayerAnswerStatisticsSource[] answers =
        [
            new(1, true, 200),
            new(1, false, -100),
            new(2, true, 100),
            new(3, false, -50)
        ];

        var statistics = PlayerStatisticsService.Build(games, answers);

        var player = Assert.Single(statistics, item => item.Name == "player x");
        Assert.Equal(2, player.GamesPlayed);
        Assert.Equal(200, player.TotalFinalScore);
        Assert.Equal(2, player.CorrectAnswers);
        Assert.Equal(3, player.Attempts);
        Assert.Equal(2d / 3d, player.Accuracy!.Value, 10);
        Assert.Equal(200, player.AnswerScoreDelta);
        Assert.Equal(secondGame, player.LastPlayedAtUtc);
    }

    [Fact]
    public void Build_keeps_players_without_attempts()
    {
        PlayerGameStatisticsSource[] games =
        [
            new(1, 10, "Observer", 0, DateTime.UtcNow)
        ];

        var player = Assert.Single(PlayerStatisticsService.Build(games, []));

        Assert.Equal(0, player.Attempts);
        Assert.Null(player.Accuracy);
    }
}
