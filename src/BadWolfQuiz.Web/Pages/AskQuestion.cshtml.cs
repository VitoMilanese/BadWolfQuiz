using BadWolfQuiz.Web.Localization;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;

namespace BadWolfQuiz.Web.Pages;

[EnableRateLimiting("discord-questions")]
public sealed class AskQuestionModel(
    DiscordQuestionSender questionSender,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    [BindProperty]
    public string? SenderName { get; set; }

    [BindProperty]
    public string Question { get; set; } = string.Empty;

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        SenderName = SenderName?.Trim();
        Question = Question.Trim();

        if (SenderName?.Length > 80 ||
            Question.Length is < 1 or > 1500)
        {
            ModelState.AddModelError(string.Empty, localizer["AskQuestion_Invalid"].Value);
            return Page();
        }

        if (!await questionSender.SendAsync(SenderName, Question, cancellationToken))
        {
            ModelState.AddModelError(string.Empty, localizer["AskQuestion_Failed"].Value);
            return Page();
        }

        TempData["SuccessMessage"] = localizer["AskQuestion_Sent"].Value;
        return RedirectToPage();
    }
}
