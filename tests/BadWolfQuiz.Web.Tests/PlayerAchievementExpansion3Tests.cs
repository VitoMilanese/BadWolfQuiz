using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Tests;

public sealed class PlayerAchievementExpansion3Tests
{
    private static readonly string[] ExpectedTail =
    [
        "StarTrekTag",
        "Anime25",
        "CounterStrike25",
        "Dota25",
        "Ukraine25",
        "DoubleReward",
        "HalfReward",
        "TwoWinsInRow",
        "BeatPreviousWinner",
        "Geography25",
        "History25",
        "PeerMaxRatings10",
        "PlayOneMonth",
        "PlaySixMonths",
        "PlayOneYear"
    ];

    [Fact]
    public void Requested_achievements_are_the_catalog_tail_in_exact_order()
    {
        Assert.Equal(80, PlayerAchievementService.Catalog.Count);
        Assert.Equal(
            ExpectedTail,
            PlayerAchievementService.Catalog.TakeLast(ExpectedTail.Length).Select(item => item.Code));

        Assert.True(PlayerAchievementService.Catalog.Single(item => item.Code == "StarTrekTag").IsSecret);
        Assert.All(
            PlayerAchievementService.Catalog.TakeLast(ExpectedTail.Length).Skip(1),
            item => Assert.False(item.IsSecret));
    }

    [Theory]
    [InlineData("StarTrekTag", "Зоряний Шлях")]
    [InlineData("StarTrekTag", "Star Trek")]
    [InlineData("Anime25", "аніме")]
    [InlineData("Anime25", "anime")]
    [InlineData("CounterStrike25", "кс")]
    [InlineData("CounterStrike25", "cs")]
    [InlineData("CounterStrike25", "counter strike")]
    [InlineData("Dota25", "dota")]
    [InlineData("Dota25", "dota2")]
    [InlineData("Ukraine25", "Україна")]
    [InlineData("Ukraine25", "Ukraine")]
    [InlineData("Geography25", "географія")]
    [InlineData("Geography25", "geography")]
    [InlineData("History25", "історія")]
    [InlineData("History25", "history")]
    public void New_tag_aliases_count_correct_answers(string group, string tag)
    {
        var counts = PlayerTagAchievementCatalog.CountAnswers(
        [
            new PlayerAchievementAnswerSource(1, true, 100, DateTime.UtcNow, [$"  {tag}  "])
        ]);

        Assert.Equal(1, counts[group]);
    }

    [Fact]
    public void History_tracks_consecutive_wins_and_full_calendar_months()
    {
        var first = new DateTime(2025, 9, 11, 12, 0, 0, DateTimeKind.Utc);
        PlayerAchievementGameSource[] appearances =
        [
            new(1, 10, "Wolf", 100, first),
            new(2, 20, "Wolf", 200, first.AddMonths(1)),
            new(3, 30, "Wolf", 50, first.AddMonths(6)),
            new(4, 40, "Wolf", 300, first.AddYears(1))
        ];
        PlayerAchievementGameScoreSource[] scores =
        [
            new(10, 100), new(10, 0),
            new(20, 200), new(20, 0),
            new(30, 100), new(30, 50),
            new(40, 300), new(40, 0)
        ];

        var history = PlayerAchievementService.BuildHistory(appearances, scores, []);

        Assert.Equal(2, history.BestWinStreak);
        Assert.Equal(12, history.PlayingMonths);
        var progress = PlayerAchievementService.BuildProgress(history, []);
        Assert.Equal(2, progress.Single(item => item.Code == "TwoWinsInRow").Progress);
        Assert.Equal(1, progress.Single(item => item.Code == "PlayOneMonth").Progress);
        Assert.Equal(6, progress.Single(item => item.Code == "PlaySixMonths").Progress);
        Assert.Equal(12, progress.Single(item => item.Code == "PlayOneYear").Progress);
    }

    [Fact]
    public async Task Ten_distinct_maximum_peer_ratings_unlock_the_achievement_without_duplicates()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<QuizDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new QuizDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var service = new PlayerAchievementService(db);
        var events = Enumerable.Range(0, 10)
            .Select(index => new PlayerMaximumRatingEventSource(
                "account-1",
                "host-1",
                "Wolf",
                100 + index,
                new GamePlayerId(Guid.NewGuid())))
            .ToArray();

        await service.RecordPeerMaximumRatingEventsAsync(42, events);
        await service.RecordPeerMaximumRatingEventsAsync(42, events);
        var progress = await service.LoadForPlayerAsync(
            "host-1",
            "Wolf",
            currentGameCode: null,
            accountId: "account-1");

        Assert.True(progress.Single(item => item.Code == "PeerMaxRatings10").IsUnlocked);
        Assert.Equal(
            10,
            await db.PlayerAchievements.CountAsync(item =>
                item.AccountId == "account-1" &&
                item.AchievementCode.StartsWith("__Peer5:")));
    }
}
