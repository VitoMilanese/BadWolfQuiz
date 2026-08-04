using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Services;

public sealed class QuizRatingService(
    QuizDbContext db,
    GameSessionRegistry sessionRegistry)
{
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
                rating.PlayerName == identity)
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
                item.PlayerName == identity,
                cancellationToken);

        if (rating is null)
        {
            db.QuizRatings.Add(new QuizRating
            {
                QuizId = storedPlayer.QuizId,
                GameSessionId = storedPlayer.GameSessionId,
                PlayerName = identity,
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
        if (game?.Session.Status != BadWolfQuiz.Game.Runtime.GameSessionStatus.Completed)
        {
            return null;
        }

        var player = sessionRegistry.GetPlayers(game)
            .SingleOrDefault(item => item.Id == runtimePlayerId);
        return player?.Name;
    }
}

public enum QuizRatingResult
{
    Saved,
    InvalidScore,
    NotAllowed,
    NotReady
}
