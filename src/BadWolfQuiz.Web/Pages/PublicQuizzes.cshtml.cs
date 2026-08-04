using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Localization;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace BadWolfQuiz.Web.Pages;

public sealed class PublicQuizzesModel(
    QuizDbContext db,
    GameSessionLauncher gameSessionLauncher,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    public IReadOnlyList<PublicQuizListItem> Quizzes { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Search = string.IsNullOrWhiteSpace(Search) ? null : Search.Trim();
        var query = db.Quizzes
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(quiz => quiz.IsPublic && !quiz.IsArchived);

        if (Search is { Length: > 0 })
        {
            var search = Search;
            query = query.Where(quiz =>
                quiz.Title.Contains(search) ||
                (quiz.Description != null && quiz.Description.Contains(search)));
        }

        Quizzes = await query
            .OrderByDescending(quiz => quiz.Ratings.Any())
            .ThenByDescending(quiz => quiz.Ratings.Average(rating => (double?)rating.Score))
            .ThenByDescending(quiz => quiz.PublishedAtUtc)
            .Select(quiz => new PublicQuizListItem(
                quiz.Id,
                quiz.Title,
                quiz.Description,
                quiz.Rounds.Count,
                quiz.Ratings.Average(rating => (double?)rating.Score),
                quiz.Ratings.Count))
            .ToListAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostCreateGameAsync(
        int quizId,
        CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return RedirectToPage("/Account/Login", new
            {
                returnUrl = Url.Page("/PublicQuizzes")
            });
        }

        try
        {
            var session = await gameSessionLauncher.CreateAsync(
                quizId,
                cancellationToken);
            return session is null
                ? NotFound()
                : RedirectToPage(
                    "/Admin/Games/Lobby",
                    new { id = session.Session.Id.Value });
        }
        catch (ArgumentException)
        {
            TempData["ErrorMessage"] = localizer["Error_QuizCannotStart"].Value;
        }
        catch (InvalidOperationException)
        {
            TempData["ErrorMessage"] = localizer["Error_QuizCannotStart"].Value;
        }

        return RedirectToPage(new { Search });
    }
}

public sealed record PublicQuizListItem(
    int Id,
    string Title,
    string? Description,
    int RoundCount,
    double? AverageRating,
    int RatingCount);
