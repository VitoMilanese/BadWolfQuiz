using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Services;

public sealed class PlayerStatisticsService(QuizDbContext db)
{
    public async Task<IReadOnlyList<PlayerLifetimeStatistics>> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        var appearances = await db.GamePlayers
            .AsNoTracking()
            .Where(player => player.Session.Status == GameSessionStatus.Finished)
            .Select(player => new PlayerGameStatisticsSource(
                player.Id,
                player.GameSessionId,
                player.Name,
                player.TotalScore,
                player.Session.FinishedAtUtc ?? player.Session.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        var results = await db.PlayerQuestionResults
            .AsNoTracking()
            .Where(result => result.GameQuestion.Session.Status == GameSessionStatus.Finished)
            .Select(result => new PlayerAnswerStatisticsSource(
                result.GamePlayerId,
                result.IsCorrect == true,
                result.PointsAwarded))
            .ToListAsync(cancellationToken);

        return Build(appearances, results);
    }

    public static IReadOnlyList<PlayerLifetimeStatistics> Build(
        IReadOnlyCollection<PlayerGameStatisticsSource> appearances,
        IReadOnlyCollection<PlayerAnswerStatisticsSource> results)
    {
        var resultsByPlayer = results
            .GroupBy(result => result.GamePlayerId)
            .ToDictionary(group => group.Key, group => group.ToArray());

        return appearances
            .GroupBy(
                player => NormalizeName(player.Name),
                StringComparer.Ordinal)
            .Select(group =>
            {
                var latest = group.OrderByDescending(player => player.FinishedAtUtc).First();
                var answers = group
                    .SelectMany(player => resultsByPlayer.GetValueOrDefault(player.GamePlayerId) ?? [])
                    .ToArray();
                var attempts = answers.Length;
                var correctAnswers = answers.Count(answer => answer.IsCorrect);

                return new PlayerLifetimeStatistics(
                    latest.Name.Trim(),
                    group.Select(player => player.GameSessionId).Distinct().Count(),
                    group.Sum(player => player.FinalScore),
                    correctAnswers,
                    attempts,
                    attempts == 0 ? null : (double)correctAnswers / attempts,
                    answers.Sum(answer => answer.PointsAwarded),
                    latest.FinishedAtUtc);
            })
            .OrderByDescending(player => player.GamesPlayed)
            .ThenByDescending(player => player.CorrectAnswers)
            .ThenBy(player => player.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private static string NormalizeName(string name) => name.Trim().ToUpperInvariant();
}

public sealed record PlayerGameStatisticsSource(
    int GamePlayerId,
    int GameSessionId,
    string Name,
    int FinalScore,
    DateTime FinishedAtUtc);

public sealed record PlayerAnswerStatisticsSource(
    int GamePlayerId,
    bool IsCorrect,
    int PointsAwarded);

public sealed record PlayerLifetimeStatistics(
    string Name,
    int GamesPlayed,
    int TotalFinalScore,
    int CorrectAnswers,
    int Attempts,
    double? Accuracy,
    int AnswerScoreDelta,
    DateTime LastPlayedAtUtc);
