using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Services;

public sealed class QuizRatingService(
    QuizDbContext db,
    GameSessionRegistry sessionRegistry)
{
    private const string PlayerKeyPrefix = "player:";
    private const string HostKeyPrefix = "host:";

    public static bool IsRatingAvailable(BadWolfQuiz.Game.Runtime.GameSession session) =>
        session.Status == BadWolfQuiz.Game.Runtime.GameSessionStatus.Completed ||
        (session.Quiz.FinalQuestion is null &&
            !session.HasNextRound &&
            session.IsCurrentRoundComplete);

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

        var identity = FindCompletedPlayer(publicCode, runtimePlayerId);
        if (identity is null)
        {
            return QuizRatingResult.NotAllowed;
        }

        var storedPlayer = await db.GamePlayers
            .IgnoreQueryFilters()
            .Where(player =>
                player.Session.PublicCode == publicCode &&
                player.Session.Status == BadWolfQuiz.Web.Models.GameSessionStatus.Finished &&
                player.Name == identity)
            .Select(player => new
            {
                player.GameSessionId,
                player.Session.QuizId
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

        var state = await GetHostRatingStateAsync(game, hostId, cancellationToken);
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

    private static string PlayerKey(string playerName) =>
        PlayerKeyPrefix + playerName;

    private static string HostKey(string hostId) => HostKeyPrefix + hostId;
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
