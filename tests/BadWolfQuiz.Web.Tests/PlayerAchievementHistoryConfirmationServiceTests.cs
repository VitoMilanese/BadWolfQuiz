using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using BadWolfQuiz.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Tests;

public sealed class PlayerAchievementHistoryConfirmationServiceTests
{
    [Fact]
    public async Task ConfirmAsync_adopts_old_finished_history_and_re_evaluates_account_immediately()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<QuizDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new QuizDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var host = CreateHost("host-confirm", "host-confirm@example.test");
        var account = CreateHost("account-confirm", "account-confirm@example.test");
        var quiz = CreateQuiz(host, "Manual confirmation");
        var startedAt = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
        var finishedGames = Enumerable.Range(1, 5)
            .Select(index => CreateStoredGame(
                quiz,
                host,
                $"CF{index:0000}",
                "Rose",
                1_000 + index,
                GameSessionStatus.Finished,
                countsForAchievementHistory: true,
                startedAt.AddDays(index)))
            .ToArray();
        var unfinished = CreateStoredGame(
            quiz,
            host,
            "CFRUN1",
            "Rose",
            9_999,
            GameSessionStatus.Running,
            countsForAchievementHistory: true,
            startedAt.AddDays(10));
        var removed = CreateStoredGame(
            quiz,
            host,
            "CFREM1",
            "Rose",
            9_999,
            GameSessionStatus.Finished,
            countsForAchievementHistory: false,
            startedAt.AddDays(11));

        db.Hosts.AddRange(host, account);
        db.GameSessions.AddRange(finishedGames.Append(unfinished).Append(removed));
        await db.SaveChangesAsync();

        var originalUnlockAt = startedAt.AddDays(1).AddMinutes(30);
        db.PlayerAchievements.Add(new PlayerAchievement
        {
            HostId = host.Id,
            PlayerKey = PlayerAchievementService.NormalizePlayerKey("Rose"),
            AchievementCode = "DoubleReward",
            UnlockedAtUtc = originalUnlockAt,
            SourceGameSessionId = finishedGames[0].Id
        });
        db.PlayerAchievements.Add(new PlayerAchievement
        {
            AccountId = account.Id,
            AchievementCode = "DoubleReward",
            UnlockedAtUtc = originalUnlockAt.AddDays(20),
            SourceGameSessionId = null
        });
        await db.SaveChangesAsync();

        var service = new PlayerAchievementHistoryConfirmationService(db);
        var preview = await service.LoadPendingAsync(account.Id, host.Id, "rose");

        Assert.NotNull(preview);
        Assert.Equal(5, preview!.PendingGameCount);
        Assert.Contains(preview.PendingAchievements, item => item.AchievementCode == "DoubleReward");

        var status = await service.ConfirmAsync(account.Id, host.Id, "Rose");

        Assert.Equal(PlayerAchievementHistoryConfirmationStatus.Confirmed, status);
        var linkedPlayers = await db.PlayerGameAccountLinks
            .AsNoTracking()
            .Where(link => link.AccountId == account.Id)
            .Select(link => link.GamePlayerId)
            .ToListAsync();
        Assert.Equal(5, linkedPlayers.Count);
        Assert.DoesNotContain(unfinished.Players.Single().Id, linkedPlayers);
        Assert.DoesNotContain(removed.Players.Single().Id, linkedPlayers);

        var adoptedDoubleReward = Assert.Single(await db.PlayerAchievements
            .AsNoTracking()
            .Where(item => item.AccountId == account.Id && item.AchievementCode == "DoubleReward")
            .ToListAsync());
        Assert.Equal(originalUnlockAt, adoptedDoubleReward.UnlockedAtUtc);
        Assert.Equal(finishedGames[0].Id, adoptedDoubleReward.SourceGameSessionId);
        Assert.True(await db.PlayerAchievements
            .AsNoTracking()
            .AnyAsync(item => item.AccountId == account.Id && item.AchievementCode == "Regular"));

        Assert.Null(await service.LoadPendingAsync(account.Id, host.Id, "Rose"));
    }

    [Fact]
    public async Task Pending_confirmation_is_hidden_and_rejected_when_same_nickname_history_has_other_account_claim()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<QuizDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new QuizDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var host = CreateHost("host-conflict", "host-conflict@example.test");
        var account = CreateHost("account-conflict", "account-conflict@example.test");
        var otherAccount = CreateHost("account-other", "account-other@example.test");
        var quiz = CreateQuiz(host, "Conflict confirmation");
        var first = CreateStoredGame(
            quiz,
            host,
            "CC0001",
            "Rose",
            100,
            GameSessionStatus.Finished,
            countsForAchievementHistory: true,
            DateTime.UtcNow.AddDays(-3));
        var second = CreateStoredGame(
            quiz,
            host,
            "CC0002",
            "rose",
            200,
            GameSessionStatus.Finished,
            countsForAchievementHistory: true,
            DateTime.UtcNow.AddDays(-2));

        db.Hosts.AddRange(host, account, otherAccount);
        db.GameSessions.AddRange(first, second);
        await db.SaveChangesAsync();
        db.PlayerGameAccountLinks.Add(new PlayerGameAccountLink
        {
            GamePlayerId = first.Players.Single().Id,
            AccountId = otherAccount.Id
        });
        db.PlayerAchievements.Add(new PlayerAchievement
        {
            HostId = host.Id,
            PlayerKey = PlayerAchievementService.NormalizePlayerKey("Rose"),
            AchievementCode = "DoubleReward",
            UnlockedAtUtc = DateTime.UtcNow.AddDays(-2),
            SourceGameSessionId = second.Id
        });
        await db.SaveChangesAsync();

        var service = new PlayerAchievementHistoryConfirmationService(db);

        Assert.Null(await service.LoadPendingAsync(account.Id, host.Id, "Rose"));
        Assert.Equal(
            PlayerAchievementHistoryConfirmationStatus.ConflictingAccountClaim,
            await service.ConfirmAsync(account.Id, host.Id, "Rose"));
        Assert.False(await db.PlayerGameAccountLinks
            .AsNoTracking()
            .AnyAsync(link =>
                link.GamePlayerId == second.Players.Single().Id &&
                link.AccountId == account.Id));
        Assert.False(await db.PlayerAchievements
            .AsNoTracking()
            .AnyAsync(item => item.AccountId == account.Id && item.AchievementCode == "DoubleReward"));
    }

    [Fact]
    public async Task Already_synchronized_history_is_not_offered_again()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<QuizDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new QuizDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var host = CreateHost("host-done", "host-done@example.test");
        var account = CreateHost("account-done", "account-done@example.test");
        var quiz = CreateQuiz(host, "Already synchronized");
        var game = CreateStoredGame(
            quiz,
            host,
            "CD0001",
            "Rose",
            500,
            GameSessionStatus.Finished,
            countsForAchievementHistory: true,
            DateTime.UtcNow.AddDays(-1));
        db.Hosts.AddRange(host, account);
        db.GameSessions.Add(game);
        await db.SaveChangesAsync();

        var unlockedAt = DateTime.UtcNow.AddDays(-1);
        db.PlayerGameAccountLinks.Add(new PlayerGameAccountLink
        {
            GamePlayerId = game.Players.Single().Id,
            AccountId = account.Id
        });
        db.PlayerAchievements.AddRange(
            new PlayerAchievement
            {
                HostId = host.Id,
                PlayerKey = PlayerAchievementService.NormalizePlayerKey("Rose"),
                AchievementCode = "DoubleReward",
                UnlockedAtUtc = unlockedAt,
                SourceGameSessionId = game.Id
            },
            new PlayerAchievement
            {
                AccountId = account.Id,
                AchievementCode = "DoubleReward",
                UnlockedAtUtc = unlockedAt,
                SourceGameSessionId = game.Id
            });
        await db.SaveChangesAsync();

        var service = new PlayerAchievementHistoryConfirmationService(db);

        Assert.Null(await service.LoadPendingAsync(account.Id, host.Id, "Rose"));
        Assert.Equal(
            PlayerAchievementHistoryConfirmationStatus.NothingToConfirm,
            await service.ConfirmAsync(account.Id, host.Id, "Rose"));
    }

    private static HostAccount CreateHost(string id, string email) => new()
    {
        Id = id,
        Email = email,
        NormalizedEmail = email.ToUpperInvariant(),
        PasswordHash = "test"
    };

    private static Quiz CreateQuiz(HostAccount host, string title) => new()
    {
        Host = host,
        HostId = host.Id,
        Title = title
    };

    private static GameSession CreateStoredGame(
        Quiz quiz,
        HostAccount host,
        string publicCode,
        string playerName,
        int score,
        GameSessionStatus status,
        bool countsForAchievementHistory,
        DateTime timestamp)
    {
        var session = new GameSession
        {
            Quiz = quiz,
            Host = host,
            HostId = host.Id,
            PublicCode = publicCode,
            Status = status,
            CreatedAtUtc = timestamp.AddHours(-1),
            FinishedAtUtc = status == GameSessionStatus.Finished ? timestamp : null
        };
        session.Players.Add(new GamePlayer
        {
            Name = playerName,
            ReconnectToken = string.Empty,
            TotalScore = score,
            JoinedAtUtc = timestamp.AddMinutes(-45),
            LastSeenAtUtc = timestamp,
            IsActive = false,
            CountsForAchievementHistory = countsForAchievementHistory
        });
        return session;
    }
}
