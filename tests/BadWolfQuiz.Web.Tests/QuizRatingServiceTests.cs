using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using BadWolfQuiz.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Tests;

public sealed class QuizRatingServiceTests
{
    [Fact]
    public async Task RateAsync_creates_and_updates_one_rating_per_player_and_game()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<QuizDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new QuizDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var quiz = CreateStoredQuiz();
        db.Quizzes.Add(quiz);
        await db.SaveChangesAsync();

        var registry = new GameSessionRegistry(new FixedCodeGenerator());
        var registration = registry.Create(CreateSnapshot(quiz));
        var runtimePlayer = registration.Session.AddPlayer("Rose");
        registration.Session.Start();
        var questionId = quiz.Rounds.Single().Categories.Single().Questions.Single().Id;
        registration.Session.SelectQuestion(questionId);
        registration.Session.ActivateQuestionBuzzer(questionId);
        registration.Session.JudgeQuestionAnswer(questionId, runtimePlayer.Id, true);
        registration.Session.CloseQuestionAnswer(questionId);

        var storedSession = new BadWolfQuiz.Web.Models.GameSession
        {
            QuizId = quiz.Id,
            PublicCode = registration.PublicCode,
            Status = BadWolfQuiz.Web.Models.GameSessionStatus.Finished
        };
        storedSession.Players.Add(new BadWolfQuiz.Web.Models.GamePlayer
        {
            Name = "Rose",
            ReconnectToken = string.Empty
        });
        db.GameSessions.Add(storedSession);
        await db.SaveChangesAsync();

        var service = new QuizRatingService(db, registry);
        Assert.Equal(
            QuizRatingResult.Saved,
            await service.RateAsync(registration.PublicCode, runtimePlayer.Id, 5));
        Assert.Equal(
            QuizRatingResult.Saved,
            await service.RateAsync(registration.PublicCode, runtimePlayer.Id, 2));

        var rating = Assert.Single(await db.QuizRatings.ToListAsync());
        Assert.Equal(2, rating.Score);
        Assert.Equal("player:Rose", rating.RaterKey);
    }

    [Fact]
    public async Task RateAsync_rejects_changes_after_rating_phase_is_finalized()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<QuizDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new QuizDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var quiz = CreateStoredQuiz();
        db.Quizzes.Add(quiz);
        await db.SaveChangesAsync();

        var registry = new GameSessionRegistry(new FixedCodeGenerator());
        var registration = registry.Create(CreateSnapshot(quiz));
        var runtimePlayer = registration.Session.AddPlayer("Rose");
        registration.Session.Start();
        var questionId = quiz.Rounds.Single().Categories.Single().Questions.Single().Id;
        registration.Session.SelectQuestion(questionId);
        registration.Session.ActivateQuestionBuzzer(questionId);
        registration.Session.JudgeQuestionAnswer(questionId, runtimePlayer.Id, true);
        registration.Session.CloseQuestionAnswer(questionId);

        var storedSession = new BadWolfQuiz.Web.Models.GameSession
        {
            QuizId = quiz.Id,
            PublicCode = registration.PublicCode,
            Status = BadWolfQuiz.Web.Models.GameSessionStatus.Finished
        };
        storedSession.Players.Add(new BadWolfQuiz.Web.Models.GamePlayer
        {
            Name = "Rose",
            ReconnectToken = string.Empty
        });
        db.GameSessions.Add(storedSession);
        await db.SaveChangesAsync();

        var service = new QuizRatingService(db, registry);
        Assert.True(QuizRatingService.IsRatingAvailable(registration.Session));
        Assert.Equal(
            QuizRatingResult.Saved,
            await service.RateAsync(registration.PublicCode, runtimePlayer.Id, 5));

        await service.FinalizeRatingPhaseAsync(registration.Session);

        Assert.False(QuizRatingService.IsRatingAvailable(registration.Session));
        Assert.Null(await service.GetPlayerRatingAsync(
            registration.PublicCode,
            runtimePlayer.Id));
        Assert.Equal(
            QuizRatingResult.NotAllowed,
            await service.RateAsync(registration.PublicCode, runtimePlayer.Id, 2));

        var rating = Assert.Single(await db.QuizRatings.ToListAsync());
        Assert.Equal(5, rating.Score);
        Assert.Equal("player:Rose", rating.RaterKey);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(6)]
    public async Task RateAsync_rejects_scores_outside_range(int score)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<QuizDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new QuizDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var service = new QuizRatingService(
            db,
            new GameSessionRegistry(new FixedCodeGenerator()));

        Assert.Equal(
            QuizRatingResult.InvalidScore,
            await service.RateAsync("ABC123", new(Guid.NewGuid()), score));
    }

    [Fact]
    public async Task RateHostAsync_allows_runner_to_rate_another_hosts_public_quiz()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<QuizDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new QuizDbContext(options);
        await db.Database.EnsureCreatedAsync();

        db.Hosts.AddRange(
            CreateHost("owner", "owner@example.com"),
            CreateHost("runner", "runner@example.com"));
        var quiz = CreateStoredQuiz();
        quiz.HostId = "owner";
        quiz.IsPublic = true;
        db.Quizzes.Add(quiz);
        await db.SaveChangesAsync();

        var registry = new GameSessionRegistry(new FixedCodeGenerator());
        var registration = registry.Create(
            CreateSnapshot(quiz),
            BadWolfQuiz.Game.Runtime.GameSessionSettings.Default,
            "runner");
        var player = registration.Session.AddPlayer("Rose");
        registration.Session.Start();
        var questionId = quiz.Rounds.Single().Categories.Single().Questions.Single().Id;
        registration.Session.SelectQuestion(questionId);
        registration.Session.ActivateQuestionBuzzer(questionId);
        registration.Session.JudgeQuestionAnswer(questionId, player.Id, true);
        registration.Session.CloseQuestionAnswer(questionId);

        db.GameSessions.Add(new BadWolfQuiz.Web.Models.GameSession
        {
            QuizId = quiz.Id,
            HostId = "runner",
            PublicCode = registration.PublicCode,
            Status = BadWolfQuiz.Web.Models.GameSessionStatus.Finished
        });
        await db.SaveChangesAsync();

        var service = new QuizRatingService(db, registry);
        Assert.Equal(
            QuizRatingResult.Saved,
            await service.RateHostAsync(registration, "runner", 4));
        var rating = Assert.Single(await db.QuizRatings.ToListAsync());
        Assert.Equal("host:runner", rating.RaterKey);
        Assert.Equal(4, rating.Score);
    }

    private static Quiz CreateStoredQuiz()
    {
        var quiz = new Quiz { Title = "Rated quiz" };
        var round = new QuizRound { Title = "Round", SortOrder = 1 };
        round.Rows.Add(new QuizRoundRow { RowIndex = 1, Points = 100 });
        var category = new QuizCategory { Title = "Category", SortOrder = 1 };
        category.Questions.Add(new QuizQuestion { RowIndex = 1 });
        round.Categories.Add(category);
        quiz.Rounds.Add(round);
        return quiz;
    }

    private static HostAccount CreateHost(string id, string email) => new()
    {
        Id = id,
        Email = email,
        NormalizedEmail = email.ToUpperInvariant(),
        PasswordHash = "hash"
    };

    private static QuizSnapshot CreateSnapshot(Quiz quiz)
    {
        var round = quiz.Rounds.Single();
        var category = round.Categories.Single();
        var question = category.Questions.Single();
        return new QuizSnapshot(
            quiz.Id,
            quiz.Title,
            [
                new QuizRoundSnapshot(
                    round.Id,
                    round.Title,
                    round.SortOrder,
                    [
                        new QuizQuestionSnapshot(
                            question.Id,
                            category.Id,
                            1,
                            100,
                            false,
                            category.Title)
                    ])
            ]);
    }

    private sealed class FixedCodeGenerator : IGameCodeGenerator
    {
        public string Create() => "ABC123";
    }
}
