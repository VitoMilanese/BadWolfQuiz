using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using Microsoft.EntityFrameworkCore;
using RuntimeGameSession = BadWolfQuiz.Game.Runtime.GameSession;
using StoredGamePlayer = BadWolfQuiz.Web.Models.GamePlayer;
using StoredGameQuestion = BadWolfQuiz.Web.Models.GameQuestion;
using StoredGameSession = BadWolfQuiz.Web.Models.GameSession;

namespace BadWolfQuiz.Web.Services;

public sealed class GameHistoryStore(QuizDbContext db)
{
    public async Task<bool> SaveCompletedGameAsync(
        GameSessionRegistration registration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registration);

        var runtime = registration.Session;

        if (!IsComplete(runtime))
        {
            return false;
        }

        var quizExists = await db.Quizzes
            .IgnoreQueryFilters()
            .AnyAsync(
            quiz => quiz.Id == runtime.Quiz.SourceQuizId,
            cancellationToken);

        if (!quizExists)
        {
            return false;
        }

        var stored = await db.GameSessions
            .Include(session => session.Players)
            .Include(session => session.Questions)
                .ThenInclude(question => question.Results)
            .SingleOrDefaultAsync(
                session => session.PublicCode == registration.PublicCode,
                cancellationToken);

        if (stored is null)
        {
            stored = new StoredGameSession
            {
                QuizId = runtime.Quiz.SourceQuizId,
                HostId = registration.HostId,
                PublicCode = registration.PublicCode
            };
            db.GameSessions.Add(stored);
        }
        else
        {
            db.PlayerQuestionResults.RemoveRange(
                stored.Questions.SelectMany(question => question.Results));
            db.GameQuestions.RemoveRange(stored.Questions);
            db.GamePlayers.RemoveRange(stored.Players);
            stored.Questions.Clear();
            stored.Players.Clear();
        }

        stored.Status = BadWolfQuiz.Web.Models.GameSessionStatus.Finished;
        stored.CreatedAtUtc = runtime.CreatedAtUtc.UtcDateTime;
        stored.StartedAtUtc = runtime.StartedAtUtc?.UtcDateTime;
        stored.FinishedAtUtc = DateTime.UtcNow;

        var players = runtime.AllPlayers.ToDictionary(
            player => player.Id,
            player => new StoredGamePlayer
            {
                Name = player.Name,
                ReconnectToken = string.Empty,
                TotalScore = player.Score,
                JoinedAtUtc = player.JoinedAtUtc.UtcDateTime,
                LastSeenAtUtc = stored.FinishedAtUtc,
                IsActive = false
            });

        foreach (var player in players.Values)
        {
            stored.Players.Add(player);
        }

        foreach (var question in runtime.Board.Questions)
        {
            var storedQuestion = new StoredGameQuestion
            {
                QuizQuestionId = question.SourceQuestionId,
                Status = question.Status == RuntimeQuestionStatus.Resolved
                    ? GameQuestionStatus.Finished
                    : GameQuestionStatus.Pending,
                FinishedAtUtc = question.Status == RuntimeQuestionStatus.Resolved
                    ? stored.FinishedAtUtc
                    : null
            };

            foreach (var attempt in question.AnswerAttempts)
            {
                storedQuestion.Results.Add(new PlayerQuestionResult
                {
                    Player = players[attempt.PlayerId],
                    IsCorrect = attempt.IsCorrect,
                    PointsAwarded = attempt.ScoreDelta,
                    CreatedAtUtc = attempt.JudgedAtUtc.UtcDateTime
                });
            }

            stored.Questions.Add(storedQuestion);
        }

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static bool IsComplete(RuntimeGameSession session)
    {
        return session.Status == BadWolfQuiz.Game.Runtime.GameSessionStatus.Completed ||
            session.Quiz.FinalQuestion is null &&
            !session.HasNextRound &&
            session.IsCurrentRoundComplete;
    }
}
