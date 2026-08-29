using System.Runtime.CompilerServices;
using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using Microsoft.EntityFrameworkCore;
using RuntimeGameSession = BadWolfQuiz.Game.Runtime.GameSession;

namespace BadWolfQuiz.Web.Services;

public sealed class QuizRatingService(
    QuizDbContext db,
    GameSessionRegistry sessionRegistry)
{
    private const string PlayerKeyPrefix = "player:";
    private const string HostKeyPrefix = "host:";
    private static readonly ConditionalWeakTable<RuntimeGameSession, RatingPhaseState>
        RatingPhases = new();

    public static bool IsRatingAvailable(RuntimeGameSession session)
    {
        var phase = GetRatingPhase(session);
        return !phase.IsFinalized && IsRatingWindowOpen(session);
    }

    public async Task FinalizeRatingPhaseAsync(
        RuntimeGameSession session,
        CancellationToken cancellationToken = default)
    {
        var phase = GetRatingPhase(session);
        await phase.Gate.WaitAsync(cancellationToken);
        try
        {
            phase.Finalize();
        }
        finally
        {
            phase.Gate.Release();
        }
    }

    public async Task<int?> GetPlayerRatingAsync(
        string publicCode,
        GamePlayerId runtimePlayerId,
        CancellationToken cancellationToken = default)
    {
        var identity = FindCompletedPlayer(publicCode, runtimePlayerId);
        if (identity is null)
        {
            return null;
        }

        return await db.QuizRatings
            .IgnoreQueryFilters()
            .Where(rating =>
                rating.GameSession.PublicCode == publicCode &&
                rating.RaterKey == PlayerKey(identity))
            .Select(rating => (int?)rating.Score)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<QuizRatingResult> RateAsync(
        string publicCode,
        GamePlayerId runtimePlayerId,
        int score,
        CancellationToken cancellationToken = default)
    {
        if (score is < 0 or > 5)
        {
            return QuizRatingResult.InvalidScore;
        }

        var game = sessionRegistry.Find(publicCode);
        if (game is null)
        {
            return QuizRatingResult.NotAllowed;
        }

        var phase = GetRatingPhase(game.Session);
        await phase.Gate.WaitAsync(cancellationToken);
        try
        {
            if (phase.IsFinalized || !IsRatingWindowOpen(game.Session))
            {
                return QuizRatingResult.NotAllowed;
            }

            var player = sessionRegistry.GetPlayers(game)
                .SingleOrDefault(item => item.Id == runtimePlayerId);
            if (player is null)
            {
                return QuizRatingResult.NotAllowed;
            }

            var identity = player.Name;
            var storedPlayer = await db.GamePlayers
                .IgnoreQueryFilters()
                .Where(item =>
                    item.Session.PublicCode == publicCode &&
                    item.Session.Status == BadWolfQuiz.Web.Models.GameSessionStatus.Finished &&
                    item.Name == identity)
                .Select(item => new
                {
                    item.GameSessionId,
                    item.Session.QuizId
                })
                .SingleOrDefaultAsync(cancellationToken);

            if (storedPlayer is null)
            {
                return QuizRatingResult.NotReady;
            }

            var rating = await db.QuizRatings
                .IgnoreQueryFilters()
                .SingleOrDefaultAsync(item =>
                    item.GameSessionId == storedPlayer.GameSessionId &&
                    item.RaterKey == PlayerKey(identity),
                    cancellationToken);

            if (rating is null)
            {
                db.QuizRatings.Add(new QuizRating
                {
                    QuizId = storedPlayer.QuizId,
                    GameSessionId = storedPlayer.GameSessionId,
                    RaterKey = PlayerKey(identity),
                    Score = score
                });
            }
            else
            {
                rating.Score = score;
                rating.CreatedAtUtc = DateTime.UtcNow;
            }

            await db.SaveChangesAsync(cancellationToken);
            return QuizRatingResult.Saved;
        }
        finally
        {
            phase.Gate.Release();
        }
    }

    public async Task<HostQuizRatingState> GetHostRatingStateAsync(
        GameSessionRegistration game,
        string hostId,
        CancellationToken cancellationToken = default)
    {
        if (!IsRatingAvailable(game.Session) || game.HostId != hostId)
        {
            return HostQuizRatingState.Unavailable;
        }

        var source = await db.Quizzes
            .IgnoreQueryFilters()
            .Where(quiz => quiz.Id == game.Session.Quiz.SourceQuizId)
            .Select(quiz => new { quiz.HostId })
            .SingleOrDefaultAsync(cancellationToken);
        if (source is null || source.HostId == hostId)
        {
            return HostQuizRatingState.Unavailable;
        }

        var score = await db.QuizRatings
            .IgnoreQueryFilters()
            .Where(rating =>
                rating.GameSession.PublicCode == game.PublicCode &&
                rating.RaterKey == HostKey(hostId))
            .Select(rating => (int?)rating.Score)
            .SingleOrDefaultAsync(cancellationToken);
        return new HostQuizRatingState(true, score);
    }

    public async Task<QuizRatingResult> RateHostAsync(
        GameSessionRegistration game,
        string hostId,
        int score,
        CancellationToken cancellationToken = default)
    {
        if (score is < 0 or > 5)
        {
            return QuizRatingResult.InvalidScore;
        }

        var phase = GetRatingPhase(game.Session);
        await phase.Gate.WaitAsync(cancellationToken);
        try
        {
            var state = await GetHostRatingStateAsync(
                game,
                hostId,
                cancellationToken);
            if (!state.IsAvailable)
            {
                return QuizRatingResult.NotAllowed;
            }

            var storedSession = await db.GameSessions
                .IgnoreQueryFilters()
                .Where(session =>
                    session.PublicCode == game.PublicCode &&
                    session.HostId == hostId &&
                    session.Status == BadWolfQuiz.Web.Models.GameSessionStatus.Finished)
                .Select(session => new { session.Id, session.QuizId })
                .SingleOrDefaultAsync(cancellationToken);
            if (storedSession is null)
            {
                return QuizRatingResult.NotReady;
            }

            var key = HostKey(hostId);
            var rating = await db.QuizRatings
                .IgnoreQueryFilters()
                .SingleOrDefaultAsync(item =>
                    item.GameSessionId == storedSession.Id && item.RaterKey == key,
                    cancellationToken);
            if (rating is null)
            {
                db.QuizRatings.Add(new QuizRating
                {
                    QuizId = storedSession.QuizId,
                    GameSessionId = storedSession.Id,
                    RaterKey = key,
                    Score = score
                });
            }
            else
            {
                rating.Score = score;
                rating.CreatedAtUtc = DateTime.UtcNow;
            }

            await db.SaveChangesAsync(cancellationToken);
            return QuizRatingResult.Saved;
        }
        finally
        {
            phase.Gate.Release();
        }
    }

    private string? FindCompletedPlayer(
        string publicCode,
        GamePlayerId runtimePlayerId)
    {
        var game = sessionRegistry.Find(publicCode);
        if (game is null || !IsRatingAvailable(game.Session))
        {
            return null;
        }

        var player = sessionRegistry.GetPlayers(game)
            .SingleOrDefault(item => item.Id == runtimePlayerId);
        return player?.Name;
    }

    private static bool IsRatingWindowOpen(RuntimeGameSession session) =>
        session.Status == BadWolfQuiz.Game.Runtime.GameSessionStatus.Completed ||
        (session.Quiz.FinalQuestion is null &&
            !session.HasNextRound &&
            session.IsCurrentRoundComplete);

    private static RatingPhaseState GetRatingPhase(RuntimeGameSession session) =>
        RatingPhases.GetValue(session, static _ => new RatingPhaseState());

    private static string PlayerKey(string playerName) =>
        PlayerKeyPrefix + playerName;

    private static string HostKey(string hostId) => HostKeyPrefix + hostId;

    private sealed class RatingPhaseState
    {
        private int finalized;

        public SemaphoreSlim Gate { get; } = new(1, 1);

        public bool IsFinalized => Volatile.Read(ref finalized) != 0;

        public void Finalize() => Volatile.Write(ref finalized, 1);
    }
}

public sealed record HostQuizRatingState(bool IsAvailable, int? Score)
{
    public static HostQuizRatingState Unavailable { get; } = new(false, null);
}

public enum QuizRatingResult
{
    Saved,
    InvalidScore,
    NotAllowed,
    NotReady
}
