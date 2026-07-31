using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Localization;
using BadWolfQuiz.Web.Models;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace BadWolfQuiz.Web.Pages.Admin.Quizzes;

public sealed class IndexModel(
    QuizDbContext db,
    GameSessionLauncher gameSessionLauncher,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    public IReadOnlyList<Quiz> Quizzes { get; private set; } = [];

    public async Task OnGetAsync()
    {
        await LoadQuizzesAsync();
    }

    public async Task<IActionResult> OnPostCreateGameAsync(
        int quizId,
        CancellationToken cancellationToken)
    {
        try
        {
            var session = await gameSessionLauncher.CreateAsync(quizId, cancellationToken);

            if (session is null)
            {
                return NotFound();
            }

            return RedirectToPage(
                "/Admin/Games/Lobby",
                new { id = session.Session.Id.Value });
        }
        catch (ArgumentException)
        {
            TempData["ErrorMessage"] = localizer["Error_QuizCannotStart"].Value;
            return RedirectToPage();
        }
        catch (InvalidOperationException)
        {
            TempData["ErrorMessage"] = localizer["Error_QuizCannotStart"].Value;
            return RedirectToPage();
        }
    }

    public async Task<IActionResult> OnPostRenameAsync(
        int quizId,
        string title,
        string? description)
    {
        title = title.Trim();
        description = string.IsNullOrWhiteSpace(description)
            ? null
            : description.Trim();

        if (string.IsNullOrWhiteSpace(title))
        {
            TempData["ErrorMessage"] = localizer["Error_QuizNameRequired"].Value;
            return RedirectToPage();
        }

        if (title.Length > 160)
        {
            TempData["ErrorMessage"] = localizer["Error_QuizNameMaxLength"].Value;
            return RedirectToPage();
        }

        if (description?.Length > 1000)
        {
            TempData["ErrorMessage"] = localizer["Error_QuizDescriptionMaxLength"].Value;
            return RedirectToPage();
        }

        var quiz = await db.Quizzes
            .FirstOrDefaultAsync(x => x.Id == quizId && !x.IsArchived);

        if (quiz is null)
        {
            return NotFound();
        }

        quiz.Title = title;
        quiz.Description = description;
        quiz.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync();

        TempData["SuccessMessage"] = localizer["Message_QuizRenamed"].Value;
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int quizId)
    {
        var quiz = await db.Quizzes
            .FirstOrDefaultAsync(x => x.Id == quizId && !x.IsArchived);

        if (quiz is null)
        {
            return NotFound();
        }

        quiz.IsArchived = true;
        quiz.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();

        TempData["SuccessMessage"] = localizer["Message_QuizDeleted"].Value;
        return RedirectToPage();
    }

    private async Task LoadQuizzesAsync()
    {
        Quizzes = await db.Quizzes
            .AsNoTracking()
            .Include(x => x.Rounds)
            .Where(x => !x.IsArchived)
            .OrderByDescending(x => x.UpdatedAtUtc)
            .ToListAsync();
    }
}
