using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using BadWolfQuiz.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Tests;

public sealed class PlayerAchievementHistoryServiceTests
{
    [Fact]
    public async Task LoadPersistedUnlocksAsync_is_read_only_and_uses_the_authoritative_identity()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<QuizDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new QuizDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var start = new DateTime(2026, 9, 11, 10, 0, 0, DateTimeKind.Utc);
        db.PlayerAchievements.AddRange(
            new PlayerAchievement
            {
                AccountId = "account-1",
                AchievementCode = "FirstGame",
                UnlockedAtUtc = start.AddMinutes(1),
                SourceGameSessionId = 9
            },
            new PlayerAchievement
            {
                AccountId = "account-1",
                AchievementCode = "DoubleReward",
                UnlockedAtUtc = start.AddMinutes(3),
                SourceGameSessionId = 10
            },
            new PlayerAchievement
            {
                AccountId = "account-1",
                AchievementCode = "__Peer5:internal",
                UnlockedAtUtc = start.AddMinutes(4),
                SourceGameSessionId = 10
            },
            new PlayerAchievement
            {
                HostId = "host-1",
                PlayerKey = PlayerAchievementService.NormalizePlayerKey("Rose"),
                AchievementCode = "Winner",
                UnlockedAtUtc = start,
                SourceGameSessionId = 10
            },
            new PlayerAchievement
            {
                HostId = "host-1",
                PlayerKey = PlayerAchievementService.NormalizePlayerKey("Rose"),
                AchievementCode = "HalfReward",
                UnlockedAtUtc = start.AddMinutes(2),
                SourceGameSessionId = 12
            });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var service = new PlayerAchievementService(db);
        var accountCurrentGame = await service.LoadPersistedUnlocksAsync(
            "account-1",
            "host-1",
            "Rose",
            sourceGameSessionId: 10);

        var accountUnlock = Assert.Single(accountCurrentGame);
        Assert.Equal("DoubleReward", accountUnlock.AchievementCode);
        Assert.Empty(db.ChangeTracker.Entries<PlayerAchievement>());

        var anonymousHistory = await service.LoadPersistedUnlocksAsync(
            accountId: null,
            hostId: "host-1",
            playerName: "rose");

        Assert.Equal(
            ["HalfReward", "Winner"],
            anonymousHistory.Select(item => item.AchievementCode).ToArray());
        Assert.DoesNotContain(
            anonymousHistory,
            item => item.AchievementCode.StartsWith("__", StringComparison.Ordinal));
        Assert.Empty(db.ChangeTracker.Entries<PlayerAchievement>());
    }

    [Fact]
    public async Task LoadPersistedUnlocksAsync_reads_new_rows_on_each_request()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<QuizDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new QuizDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var service = new PlayerAchievementService(db);
        var first = await service.LoadPersistedUnlocksAsync(
            null,
            "host-1",
            "Donna");
        Assert.Empty(first);

        db.PlayerAchievements.Add(new PlayerAchievement
        {
            HostId = "host-1",
            PlayerKey = PlayerAchievementService.NormalizePlayerKey("Donna"),
            AchievementCode = "FirstGame",
            UnlockedAtUtc = DateTime.UtcNow,
            SourceGameSessionId = 42
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var refreshed = await service.LoadPersistedUnlocksAsync(
            null,
            "host-1",
            "Donna");

        Assert.Equal("FirstGame", Assert.Single(refreshed).AchievementCode);
        Assert.Empty(db.ChangeTracker.Entries<PlayerAchievement>());
    }
}
