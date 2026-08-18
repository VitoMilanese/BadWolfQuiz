using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Localization;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace BadWolfQuiz.Web.Pages.Admin.Quizzes;

public sealed class CloneModel(
    QuizDbContext db,
    IStringLocalizer<SharedResource> localizer,
    IStringLocalizer<QuizCloneResource> cloneLocalizer) : PageModel
{
    public IActionResult OnGet() => NotFound();

    public async Task<IActionResult> OnPostAsync(
        int quizId,
        string? title,
        CancellationToken cancellationToken)
    {
        title = title?.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            TempData["ErrorMessage"] = localizer["Error_QuizNameRequired"].Value;
            return RedirectToPage("Index");
        }

        if (title.Length > 160)
        {
            TempData["ErrorMessage"] = localizer["Error_QuizNameMaxLength"].Value;
            return RedirectToPage("Index");
        }

        var clone = await QuizCloneOperations.CloneAsync(
            db,
            quizId,
            title,
            cancellationToken);
        if (clone is null)
        {
            return NotFound();
        }

        TempData["SuccessMessage"] = cloneLocalizer["Success"].Value;
        return RedirectToPage("Editor", new { id = clone.Id });
    }
}
