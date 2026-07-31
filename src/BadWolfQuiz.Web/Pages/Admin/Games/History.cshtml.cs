using BadWolfQuiz.Web.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Pages.Admin.Games;

public sealed class HistoryModel(QuizDbContext db) : PageModel
{
    public IReadOnlyList<GameHistoryListItem> Games { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Games = await db.GameSessions
            .AsNoTracking()
            .Where(game =>
                game.Status == BadWolfQuiz.Web.Models.GameSessionStatus.Finished)
            .OrderByDescending(game => game.FinishedAtUtc)
            .Select(game => new GameHistoryListItem(
                game.Id,
                game.Quiz.Title,
                game.PublicCode,
                game.FinishedAtUtc ?? game.CreatedAtUtc,
                game.Players.Count))
            .ToListAsync(cancellationToken);
    }
}

public sealed record GameHistoryListItem(
    int Id,
    string QuizTitle,
    string PublicCode,
    DateTime FinishedAtUtc,
    int PlayerCount);
