using BadWolfQuiz.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BadWolfQuiz.Web.Services;

namespace BadWolfQuiz.Web.Pages.Admin.Games;

public sealed class HistoryDetailsModel(QuizDbContext db, CurrentHost currentHost) : PageModel
{
    public GameHistoryDetails? Game { get; private set; }

    public async Task<IActionResult> OnGetAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var stored = await db.GameSessions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AsSplitQuery()
            .Include(game => game.Quiz)
            .Include(game => game.Players)
            .Include(game => game.Questions)
                .ThenInclude(question => question.QuizQuestion)
                    .ThenInclude(question => question.Category)
                        .ThenInclude(category => category.Round)
                            .ThenInclude(round => round.Rows)
            .Include(game => game.Questions)
                .ThenInclude(question => question.Results)
                    .ThenInclude(result => result.Player)
            .SingleOrDefaultAsync(
                game => game.Id == id &&
                    game.HostId == currentHost.RequiredId &&
                    game.Status == BadWolfQuiz.Web.Models.GameSessionStatus.Finished,
                cancellationToken);

        if (stored is null)
        {
            return NotFound();
        }

        var answers = stored.Questions
            .SelectMany(question => question.Results.Select(result =>
                new GameHistoryAnswer(
                    question.QuizQuestion.Category.Round.SortOrder,
                    question.QuizQuestion.Category.Round.Title,
                    question.QuizQuestion.Category.Title,
                    question.QuizQuestion.Category.Round.Rows
                        .Single(row => row.RowIndex == question.QuizQuestion.RowIndex)
                        .Points,
                    result.Player.Id,
                    result.Player.Name,
                    result.IsCorrect == true,
                    result.PointsAwarded,
                    result.CreatedAtUtc)))
            .OrderBy(answer => answer.JudgedAtUtc)
            .ToArray();

        var rounds = stored.Questions
            .Select(question => question.QuizQuestion.Category.Round)
            .DistinctBy(round => round.Id)
            .OrderBy(round => round.SortOrder)
            .ToArray();
        var roundStatistics = rounds
            .SelectMany(round => stored.Players.Select(player =>
            {
                var playerAnswers = answers
                    .Where(answer => answer.RoundSortOrder == round.SortOrder &&
                        answer.PlayerId == player.Id)
                    .ToArray();
                var attempts = playerAnswers.Length;
                var correctAnswers = playerAnswers.Count(answer => answer.IsCorrect);
                return new GameHistoryRoundStatistics(
                    round.SortOrder,
                    round.Title,
                    player.Name,
                    correctAnswers,
                    attempts,
                    attempts == 0 ? null : (double)correctAnswers / attempts,
                    playerAnswers.Sum(answer => answer.ScoreDelta));
            }))
            .OrderBy(item => item.RoundSortOrder)
            .ThenByDescending(item => item.CorrectAnswers)
            .ThenBy(item => item.PlayerName)
            .ToArray();

        Game = new GameHistoryDetails(
            stored.Quiz.Title,
            stored.PublicCode,
            stored.FinishedAtUtc ?? stored.CreatedAtUtc,
            stored.Players
                .OrderByDescending(player => player.TotalScore)
                .ThenBy(player => player.Name)
                .Select(player => new GameHistoryPlayer(
                    player.Name,
                    player.TotalScore))
                .ToArray(),
            roundStatistics,
            answers);

        return Page();
    }
}

public sealed record GameHistoryDetails(
    string QuizTitle,
    string PublicCode,
    DateTime FinishedAtUtc,
    IReadOnlyList<GameHistoryPlayer> Players,
    IReadOnlyList<GameHistoryRoundStatistics> RoundStatistics,
    IReadOnlyList<GameHistoryAnswer> Answers);

public sealed record GameHistoryPlayer(string Name, int Score);

public sealed record GameHistoryAnswer(
    int RoundSortOrder,
    string RoundTitle,
    string CategoryTitle,
    int Points,
    int PlayerId,
    string PlayerName,
    bool IsCorrect,
    int ScoreDelta,
    DateTime JudgedAtUtc);

public sealed record GameHistoryRoundStatistics(
    int RoundSortOrder,
    string RoundTitle,
    string PlayerName,
    int CorrectAnswers,
    int Attempts,
    double? Accuracy,
    int ScoreDelta);
