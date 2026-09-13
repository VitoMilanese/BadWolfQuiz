using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using BadWolfQuiz.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Tests;

public sealed class PeerRatedAchievementCorrectnessRegressionTests
{
    [Fact]
    public void BuildHistory_ignores_peer_rated_answers_for_all_correctness_metrics()
    {
        var now = new DateTime(2026, 9, 13, 8, 0, 0, DateTimeKind.Utc);
        PlayerAchievementGameSource[] appearances =
        [
            new(1, 10, "Rose", 500, now.AddMinutes(10))
        ];
        PlayerAchievementGameScoreSource[] scores =
        [
            new(10, 500)
        ];
        PlayerAchievementAnswerSource[] answers =
        [
            new(1, true, 100, now.AddMinutes(1)),
            new(
                1,
                true,
                300,
                now.AddMinutes(2),
                ["FILMS"],
                HasAudioBlock: true,
                HasVideoBlock: true,
                CountsForCorrectnessAchievements: false),
            new(
                1,
                false,
                0,
                now.AddMinutes(3),
                CountsForCorrectnessAchievements: false),
            new(1, true, 100, now.AddMinutes(4))
        ];

        var history = PlayerAchievementService.BuildHistory(
            appearances,
            scores,
            answers);

        Assert.Equal(2, history.CorrectAnswers);
        Assert.Equal(2, history.BestCorrectStreak);
        Assert.Equal(2, history.BestFlawlessAttempts);
        Assert.Equal(0, history.AudioQuestionAnswers);
        Assert.Equal(0, history.VideoQuestionAnswers);
        Assert.All(history.TaggedAnswers!, entry => Assert.Equal(0, entry.Value));
    }

    [Fact]
    public async Task Loading_builtin_achievements_recalculates_legacy_peer_rated_unlocks()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<QuizDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new QuizDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var host = CreateHost("peer-built-in");
        var question = AddQuiz(host, QuestionPresentationType.AllPlayerPeerRatedText, "FILMS");
        db.Hosts.Add(host);
        var session = AddFinishedGame(host, question, "PEER01", "Rose", isCorrect: true);
        await db.SaveChangesAsync();

        db.PlayerAchievements.Add(new PlayerAchievement
        {
            HostId = host.Id,
            PlayerKey = PlayerAchievementService.NormalizePlayerKey("Rose"),
            AchievementCode = "FirstBite",
            SourceGameSessionId = session.Id,
            UnlockedAtUtc = session.FinishedAtUtc!.Value
        });
        await db.SaveChangesAsync();

        var progress = await new PlayerAchievementService(db).LoadForPlayerAsync(
            host.Id,
            "Rose",
            currentGameCode: null);

        var firstBite = progress.Single(item => item.Code == "FirstBite");
        Assert.False(firstBite.IsUnlocked);
        Assert.Equal(0, firstBite.Progress);
        Assert.False(await db.PlayerAchievements
            .AsNoTracking()
            .AnyAsync(item =>
                item.HostId == host.Id &&
                item.PlayerKey == PlayerAchievementService.NormalizePlayerKey("Rose") &&
                item.AchievementCode == "FirstBite"));
    }

    [Fact]
    public async Task Loading_custom_achievements_recalculates_legacy_peer_rated_unlocks()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<QuizDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new QuizDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var host = CreateHost("peer-custom");
        var question = AddQuiz(host, QuestionPresentationType.AllPlayerPeerRatedText, "FILMS");
        db.Hosts.Add(host);
        var definition = new HostCustomAchievement
        {
            HostId = host.Id,
            Name = "Film expert",
            Description = "Correct film answers",
            Target = 1,
            ArtworkPng = [1]
        };
        definition.Tags.Add(new HostCustomAchievementTag
        {
            Name = "films",
            NormalizedName = "FILMS"
        });
        db.HostCustomAchievements.Add(definition);
        var session = AddFinishedGame(host, question, "PEER02", "Rose", isCorrect: true);
        await db.SaveChangesAsync();

        var code = HostCustomAchievementService.BuildCode(definition.Id);
        db.PlayerAchievements.Add(new PlayerAchievement
        {
            HostId = host.Id,
            PlayerKey = PlayerAchievementService.NormalizePlayerKey("Rose"),
            AchievementCode = code,
            SourceGameSessionId = session.Id,
            UnlockedAtUtc = session.FinishedAtUtc!.Value
        });
        await db.SaveChangesAsync();

        var progress = await new HostCustomAchievementService(db).LoadForPlayerAsync(
            host.Id,
            "Rose",
            currentGameCode: null,
            accountId: null);

        var achievement = Assert.Single(progress);
        Assert.False(achievement.IsUnlocked);
        Assert.Equal(0, achievement.Progress);
        Assert.False(await db.PlayerAchievements
            .AsNoTracking()
            .AnyAsync(item => item.AchievementCode == code));
    }

    [Fact]
    public void Runtime_and_persistence_paths_explicitly_treat_peer_rated_questions_as_neutral()
    {
        var root = FindRepositoryRoot();
        var background = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Services",
            "AchievementUnlockNotificationBackgroundService.cs"));
        var historyStore = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Services",
            "GameHistoryStore.cs"));

        Assert.Contains(
            "question.PresentationType !=\n                QuestionPresentationType.AllPlayerPeerRatedText",
            background.Replace("\r\n", "\n"),
            StringComparison.Ordinal);
        Assert.Contains(
            "IsCorrect = question.PresentationType ==\n                        QuestionPresentationType.AllPlayerPeerRatedText\n                            ? null",
            historyStore.Replace("\r\n", "\n"),
            StringComparison.Ordinal);
    }

    private static HostAccount CreateHost(string id) => new()
    {
        Id = id,
        Email = $"{id}@example.test",
        NormalizedEmail = $"{id}@example.test".ToUpperInvariant(),
        PasswordHash = "test"
    };

    private static QuizQuestion AddQuiz(
        HostAccount host,
        QuestionPresentationType presentationType,
        string normalizedTag)
    {
        var quiz = new Quiz
        {
            HostId = host.Id,
            Host = host,
            Title = "Peer-rated achievement regression"
        };
        var round = new QuizRound { Title = "Round", SortOrder = 1 };
        round.Rows.Add(new QuizRoundRow { RowIndex = 1, Points = 100 });
        var category = new QuizCategory { Title = "Category", SortOrder = 1 };
        var question = new QuizQuestion
        {
            RowIndex = 1,
            PresentationType = presentationType
        };
        question.Tags.Add(new QuizQuestionTag
        {
            Name = normalizedTag,
            NormalizedName = normalizedTag
        });
        category.Questions.Add(question);
        round.Categories.Add(category);
        quiz.Rounds.Add(round);
        host.Quizzes.Add(quiz);
        return question;
    }

    private static GameSession AddFinishedGame(
        HostAccount host,
        QuizQuestion question,
        string code,
        string playerName,
        bool isCorrect)
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
            TotalScore = 100,
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
            IsCorrect = isCorrect,
            PointsAwarded = 100,
            CreatedAtUtc = DateTime.UtcNow
        });
        session.Questions.Add(gameQuestion);
        host.GameSessions.Add(session);
        return session;
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
