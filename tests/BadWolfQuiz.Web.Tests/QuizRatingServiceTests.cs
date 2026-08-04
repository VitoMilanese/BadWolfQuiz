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
        Assert.Equal("Rose", rating.PlayerName);
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
