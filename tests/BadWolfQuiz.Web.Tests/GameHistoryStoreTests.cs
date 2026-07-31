using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using BadWolfQuiz.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Tests;

public sealed class GameHistoryStoreTests
{
    [Fact]
    public async Task SaveCompletedGameAsync_persists_players_and_answers()
    {
        await using var fixture = await HistoryFixture.CreateAsync();
        var registration = fixture.CreateCompletedGame();

        var saved = await fixture.Store.SaveCompletedGameAsync(registration);

        Assert.True(saved);

        var history = await fixture.Db.GameSessions
            .Include(game => game.Players)
            .Include(game => game.Questions)
                .ThenInclude(question => question.Results)
            .SingleAsync();

        Assert.Equal(BadWolfQuiz.Web.Models.GameSessionStatus.Finished, history.Status);
        Assert.Equal("ABC123", history.PublicCode);
        Assert.Equal(2, history.Players.Count);
        Assert.Equal(2, history.Questions.Single().Results.Count);
        Assert.Equal(
            100,
            history.Players.Single(player => player.Name == "Rose").TotalScore);
        Assert.Equal(
            -100,
            history.Players.Single(player => player.Name == "Mickey").TotalScore);
        Assert.All(history.Players, player =>
            Assert.Equal(string.Empty, player.ReconnectToken));
    }

    [Fact]
    public async Task SaveCompletedGameAsync_updates_existing_history_after_correction()
    {
        await using var fixture = await HistoryFixture.CreateAsync();
        var registration = fixture.CreateCompletedGame();
        await fixture.Store.SaveCompletedGameAsync(registration);
        var question = registration.Session.Board.Questions.Single();
        var wrongAttempt = question.AnswerAttempts.Single(attempt => !attempt.IsCorrect);

        registration.Session.UpdateQuestionAnswerHistoryEntry(
            question.SourceQuestionId,
            wrongAttempt.Id,
            wrongAttempt.PlayerId,
            true,
            250);

        var saved = await fixture.Store.SaveCompletedGameAsync(registration);

        Assert.True(saved);
        Assert.Equal(1, await fixture.Db.GameSessions.CountAsync());
        Assert.Equal(2, await fixture.Db.GamePlayers.CountAsync());
        Assert.Equal(1, await fixture.Db.GameQuestions.CountAsync());
        Assert.Equal(2, await fixture.Db.PlayerQuestionResults.CountAsync());
        Assert.Equal(
            250,
            await fixture.Db.GamePlayers
                .Where(player => player.Name == "Mickey")
                .Select(player => player.TotalScore)
                .SingleAsync());
    }

    private sealed class HistoryFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly int _quizId;
        private readonly int _roundId;
        private readonly int _categoryId;
        private readonly int _questionId;

        private HistoryFixture(
            SqliteConnection connection,
            QuizDbContext db,
            int quizId,
            int roundId,
            int categoryId,
            int questionId)
        {
            _connection = connection;
            Db = db;
            Store = new GameHistoryStore(db);
            _quizId = quizId;
            _roundId = roundId;
            _categoryId = categoryId;
            _questionId = questionId;
        }

        public QuizDbContext Db { get; }

        public GameHistoryStore Store { get; }

        public static async Task<HistoryFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<QuizDbContext>()
                .UseSqlite(connection)
                .Options;
            var db = new QuizDbContext(options);
            await db.Database.EnsureCreatedAsync();

            var quiz = new Quiz { Title = "History Quiz" };
            var round = new QuizRound
            {
                Title = "Round 1",
                SortOrder = 0
            };
            round.Rows.Add(new QuizRoundRow
            {
                RowIndex = 0,
                Points = 100
            });
            var category = new QuizCategory
            {
                Title = "History",
                SortOrder = 0
            };
            var question = new QuizQuestion
            {
                RowIndex = 0
            };

            category.Questions.Add(question);
            round.Categories.Add(category);
            quiz.Rounds.Add(round);
            db.Quizzes.Add(quiz);
            await db.SaveChangesAsync();

            return new HistoryFixture(
                connection,
                db,
                quiz.Id,
                round.Id,
                category.Id,
                question.Id);
        }

        public GameSessionRegistration CreateCompletedGame()
        {
            var snapshot = new QuizSnapshot(
                _quizId,
                "History Quiz",
                [
                    new QuizRoundSnapshot(
                        _roundId,
                        "Round 1",
                        0,
                        [
                            new QuizQuestionSnapshot(
                                _questionId,
                                _categoryId,
                                0,
                                100,
                                false,
                                "History")
                        ])
                ]);
            var session = BadWolfQuiz.Game.Runtime.GameSession.Create(snapshot);
            var rose = session.AddPlayer("Rose");
            var mickey = session.AddPlayer("Mickey");
            session.Start();
            session.SelectQuestion(_questionId);
            session.ActivateQuestionBuzzer(_questionId);
            session.JudgeQuestionAnswer(_questionId, mickey.Id, false);
            session.JudgeQuestionAnswer(_questionId, rose.Id, true);
            session.CloseQuestionAnswer(_questionId);

            return new GameSessionRegistration("ABC123", session);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
