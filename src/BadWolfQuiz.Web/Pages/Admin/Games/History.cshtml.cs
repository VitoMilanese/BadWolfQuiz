using BadWolfQuiz.Web.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Pages.Admin.Games;

public sealed class HistoryModel(QuizDbContext db) : PageModel
{
    public IReadOnlyList<GameHistoryListItem> Games { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var storedGames = await db.GameSessions
            .AsNoTracking()
            .Include(game => game.Quiz)
            .Include(game => game.Players)
            .Where(game =>
                game.Status == BadWolfQuiz.Web.Models.GameSessionStatus.Finished)
            .OrderByDescending(game => game.FinishedAtUtc)
            .ToListAsync(cancellationToken);

        Games = storedGames.Select(game =>
        {
            var winningScore = game.Players.Count == 0
                ? (int?)null
                : game.Players.Max(player => player.TotalScore);
            var winners = winningScore.HasValue
                ? game.Players
                    .Where(player => player.TotalScore == winningScore.Value)
                    .OrderBy(player => player.Name)
                    .Select(player => player.Name)
                    .ToArray()
                : [];

            return new GameHistoryListItem(
                game.Id,
                game.Quiz.Title,
                game.PublicCode,
                game.FinishedAtUtc ?? game.CreatedAtUtc,
                game.Players.Count,
                winners,
                winningScore);
        }).ToArray();
    }
}

public sealed record GameHistoryListItem(
    int Id,
    string QuizTitle,
    string PublicCode,
    DateTime FinishedAtUtc,
    int PlayerCount,
    IReadOnlyList<string> Winners,
    int? WinningScore);
