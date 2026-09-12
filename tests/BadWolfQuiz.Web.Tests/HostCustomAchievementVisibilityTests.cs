using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using BadWolfQuiz.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Tests;

public sealed class HostCustomAchievementVisibilityTests
{
    [Fact]
    public async Task Logged_in_player_sees_cross_host_unlocks_and_current_host_progress_without_duplicates()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<QuizDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new QuizDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var hostA = CreateHost("visibility-a");
        var hostB = CreateHost("visibility-b");
        var questionB = AddQuiz(hostB, "Host B", "GEOGRAPHY");
        db.Hosts.AddRange(hostA, hostB);

        var unlockedA = CreateDefinition(hostA, "Unlocked A", target: 1);
        var deletedA = CreateDefinition(hostA, "Deleted A", target: 1);
        deletedA.IsDeleted = true;
        var lockedA = CreateDefinition(hostA, "Locked A", target: 5);
        var unlockedB = CreateDefinition(hostB, "Unlocked B", target: 10);
        var progressB = CreateDefinition(hostB, "Progress B", target: 2);
        db.HostCustomAchievements.AddRange(unlockedA, deletedA, lockedA, unlockedB, progressB);

        var playerB = AddFinishedGame(hostB, questionB, "B001", "Rose");
        const string accountId = "player-account";
        db.PlayerGameAccountLinks.Add(new PlayerGameAccountLink
        {
            Player = playerB,
            AccountId = accountId
        });
        await db.SaveChangesAsync();

        db.PlayerAchievements.AddRange(
            new PlayerAchievement
            {
                AccountId = accountId,
                AchievementCode = HostCustomAchievementService.BuildCode(unlockedA.Id)
            },
            new PlayerAchievement
            {
                AccountId = accountId,
                AchievementCode = HostCustomAchievementService.BuildCode(deletedA.Id)
            },
            new PlayerAchievement
            {
                AccountId = accountId,
                AchievementCode = HostCustomAchievementService.BuildCode(unlockedB.Id),
                SourceGameSessionId = playerB.GameSessionId
            });
        await db.SaveChangesAsync();

        var items = await new HostCustomAchievementVisibilityService(db).LoadForGameAsync(
            hostB.Id,
            playerB.Name,
            "B001",
            accountId);

        Assert.Equal(4, items.Count);
        Assert.Contains(items, item => item.Id == unlockedA.Id && item.IsUnlocked && item.HostId == hostA.Id);
        Assert.Contains(items, item => item.Id == deletedA.Id && item.IsUnlocked && item.HostId == hostA.Id);
        Assert.DoesNotContain(items, item => item.Id == lockedA.Id);

        var currentUnlocked = Assert.Single(items.Where(item => item.Id == unlockedB.Id));
        Assert.True(currentUnlocked.IsUnlocked);
        Assert.True(currentUnlocked.IsNewInCurrentGame);

        var currentProgress = Assert.Single(items.Where(item => item.Id == progressB.Id));
        Assert.False(currentProgress.IsUnlocked);
        Assert.Equal(1, currentProgress.Progress);
        Assert.Equal(2, currentProgress.Target);
    }

    [Fact]
    public async Task Guest_player_remains_scoped_to_current_host()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<QuizDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new QuizDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var hostA = CreateHost("guest-a");
        var hostB = CreateHost("guest-b");
        db.Hosts.AddRange(hostA, hostB);
        var unlockedA = CreateDefinition(hostA, "Unlocked A", target: 1);
        var currentB = CreateDefinition(hostB, "Current B", target: 1);
        db.HostCustomAchievements.AddRange(unlockedA, currentB);
        db.PlayerAchievements.Add(new PlayerAchievement
        {
            HostId = hostA.Id,
            PlayerKey = PlayerAchievementService.NormalizePlayerKey("Rose"),
            AchievementCode = HostCustomAchievementService.BuildCode(unlockedA.Id)
        });
        await db.SaveChangesAsync();

        var items = await new HostCustomAchievementVisibilityService(db).LoadForGameAsync(
            hostB.Id,
            "Rose",
            currentGameCode: null,
            accountId: null);

        var item = Assert.Single(items);
        Assert.Equal(currentB.Id, item.Id);
        Assert.False(item.IsUnlocked);
    }

    private static HostAccount CreateHost(string id) => new()
    {
        Id = id,
        Email = $"{id}@example.test",
        NormalizedEmail = $"{id}@example.test".ToUpperInvariant(),
        PasswordHash = "test"
    };

    private static HostCustomAchievement CreateDefinition(
        HostAccount host,
        string name,
        int target)
    {
        var definition = new HostCustomAchievement
        {
            HostId = host.Id,
            Name = name,
            Description = $"{name} description",
            Target = target,
            ArtworkPng = [1]
        };
        definition.Tags.Add(new HostCustomAchievementTag
        {
            Name = "geography",
            NormalizedName = "GEOGRAPHY"
        });
        return definition;
    }

    private static QuizQuestion AddQuiz(HostAccount host, string title, string normalizedTag)
    {
        var quiz = new Quiz { HostId = host.Id, Host = host, Title = title };
        var round = new QuizRound { Title = "Round", SortOrder = 1 };
        round.Rows.Add(new QuizRoundRow { RowIndex = 1, Points = 100 });
        var category = new QuizCategory { Title = "Category", SortOrder = 1 };
        var question = new QuizQuestion { RowIndex = 1 };
        question.Tags.Add(new QuizQuestionTag { Name = normalizedTag, NormalizedName = normalizedTag });
        category.Questions.Add(question);
        round.Categories.Add(category);
        quiz.Rounds.Add(round);
        host.Quizzes.Add(quiz);
        return question;
    }

    private static GamePlayer AddFinishedGame(
        HostAccount host,
        QuizQuestion question,
        string code,
        string playerName)
    {
        var quiz = host.Quizzes.Single(item => item.Rounds
            .SelectMany(round => round.Categories)
            .SelectMany(category => category.Questions)
            .Contains(question));
        var session = new GameSession
        {
            Quiz = quiz,
            Host = host,
            HostId = host.Id,
            PublicCode = code,
            Status = GameSessionStatus.Finished,
            FinishedAtUtc = DateTime.UtcNow
        };
        var player = new GamePlayer
        {
            Name = playerName,
            ReconnectToken = string.Empty,
            CountsForAchievementHistory = true
        };
        session.Players.Add(player);
        var gameQuestion = new GameQuestion
        {
            QuizQuestion = question,
            QuizQuestionId = question.Id,
            Status = GameQuestionStatus.Finished
        };
        gameQuestion.Results.Add(new PlayerQuestionResult
        {
            Player = player,
            IsCorrect = true,
            CreatedAtUtc = DateTime.UtcNow
        });
        session.Questions.Add(gameQuestion);
        host.GameSessions.Add(session);
        return player;
    }
}
