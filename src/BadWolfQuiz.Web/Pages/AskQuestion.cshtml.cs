using BadWolfQuiz.Web.Localization;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;
using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;

namespace BadWolfQuiz.Web.Pages;

[EnableRateLimiting("discord-questions")]
public sealed class AskQuestionModel(
    QuizDbContext db,
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
            ModelState.AddModelError(
                string.Empty,
                localizer["AskQuestion_Invalid"].Value);

            return Page();
        }

        var now = DateTimeOffset.UtcNow;

        var userQuestion = new UserQuestion
        {
            PublicToken = Guid.NewGuid().ToString("N"),
            SenderName = SenderName,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            Messages =
            [
                new UserQuestionMessage
                {
                    AuthorType = UserQuestionAuthorType.User,
                    Text = Question,
                    CreatedAtUtc = now
                }
            ]
        };

        db.UserQuestions.Add(userQuestion);
        await db.SaveChangesAsync(cancellationToken);

        if (!await questionSender.SendAsync(
            userQuestion.Id,
            SenderName,
            Question,
            cancellationToken))
        {
            db.UserQuestions.Remove(userQuestion);
            await db.SaveChangesAsync(cancellationToken);

            ModelState.AddModelError(
                string.Empty,
                localizer["AskQuestion_Failed"].Value);

            return Page();
        }

        return RedirectToPage(
            "/Question",
            new { token = userQuestion.PublicToken });
    }
}
