using BadWolfQuiz.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Pages.Admin.Games;

public sealed class HistoryDetailsModel(QuizDbContext db) : PageModel
{
    public GameHistoryDetails? Game { get; private set; }

    public async Task<IActionResult> OnGetAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var stored = await db.GameSessions
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
                    game.Status == BadWolfQuiz.Web.Models.GameSessionStatus.Finished,
                cancellationToken);

        if (stored is null)
        {
            return NotFound();
        }

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
            stored.Questions
                .SelectMany(question => question.Results.Select(result =>
                    new GameHistoryAnswer(
                        question.QuizQuestion.Category.Round.Title,
                        question.QuizQuestion.Category.Title,
                        question.QuizQuestion.Category.Round.Rows
                            .Single(row => row.RowIndex == question.QuizQuestion.RowIndex)
                            .Points,
                        result.Player.Name,
                        result.IsCorrect == true,
                        result.PointsAwarded,
                        result.CreatedAtUtc)))
                .OrderBy(answer => answer.JudgedAtUtc)
                .ToArray());

        return Page();
    }
}

public sealed record GameHistoryDetails(
    string QuizTitle,
    string PublicCode,
    DateTime FinishedAtUtc,
    IReadOnlyList<GameHistoryPlayer> Players,
    IReadOnlyList<GameHistoryAnswer> Answers);

public sealed record GameHistoryPlayer(string Name, int Score);

public sealed record GameHistoryAnswer(
    string RoundTitle,
    string CategoryTitle,
    int Points,
    string PlayerName,
    bool IsCorrect,
    int ScoreDelta,
    DateTime JudgedAtUtc);
