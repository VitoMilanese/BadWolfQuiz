using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Localization;
using BadWolfQuiz.Web.Models;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace BadWolfQuiz.Web.Pages;

public sealed class PublicQuizzesModel(
    QuizDbContext db,
    GameSessionLauncher gameSessionLauncher,
    IQuizMediaArchiveService mediaArchiveService,
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

        var quizzes = await query
            .OrderByDescending(quiz => quiz.Ratings.Any())
            .ThenByDescending(quiz => quiz.Ratings.Average(rating => (double?)rating.Score))
            .ThenByDescending(quiz => quiz.PublishedAtUtc)
            .Select(quiz => new PublicQuizListItem(
                quiz.Id,
                quiz.Title,
                quiz.Description,
                quiz.Host == null ? null : quiz.Host.DisplayName,
                quiz.Rounds.Count,
                quiz.Ratings.Average(rating => (double?)rating.Score),
                quiz.Ratings.Count,
                quiz.MediaState))
            .ToListAsync(cancellationToken);

        if (Search is { Length: > 0 } search)
        {
            quizzes = quizzes
                .Where(quiz => MatchesSearch(quiz.Title, quiz.Description, search))
                .ToList();
        }

        Quizzes = quizzes;
    }

    internal static bool MatchesSearch(
        string title,
        string? description,
        string search)
    {
        return title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
            (description?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false);
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
            var quiz = await db.Quizzes
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(item => item.Id == quizId && item.IsPublic && !item.IsArchived)
                .Select(item => new { item.HostId, item.MediaState })
                .SingleOrDefaultAsync(cancellationToken);
            if (quiz is null)
            {
                return NotFound();
            }

            if (quiz.MediaState != QuizMediaState.Active)
            {
                if (quiz.MediaState != QuizMediaState.Archived || string.IsNullOrWhiteSpace(quiz.HostId))
                {
                    TempData["ErrorMessage"] = localizer["MediaArchive_PublicRestoreUnavailable"].Value;
                    return RedirectToPage(new { Search });
                }

                var restore = await mediaArchiveService.RestoreAsync(
                    quizId,
                    quiz.HostId,
                    cancellationToken);
                if (!restore.Succeeded)
                {
                    TempData["ErrorMessage"] = localizer["MediaArchive_RestoreFailed"].Value;
                    return RedirectToPage(new { Search });
                }
            }

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
    string? AuthorName,
    int RoundCount,
    double? AverageRating,
    int RatingCount,
    QuizMediaState MediaState);
