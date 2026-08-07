using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Pages;

public sealed class QuestionModel(QuizDbContext db) : PageModel
{
    public UserQuestion UserQuestion { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(
        string token,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return NotFound();
        }

        await LoadQuestionAsync(token, cancellationToken);

        return UserQuestion is null
            ? NotFound()
            : Page();
    }

    public async Task<IActionResult> OnPostReplyAsync(
        string token,
        string message,
        CancellationToken cancellationToken)
    {
        message = message?.Trim() ?? string.Empty;

        if (message.Length is < 1 or > 5000)
        {
            ModelState.AddModelError(
                string.Empty,
                "Повідомлення має містити від 1 до 5000 символів.");

            await LoadQuestionAsync(token, cancellationToken);
            return Page();
        }

        var question = await db.UserQuestions
            .Include(x => x.Messages)
            .SingleOrDefaultAsync(
                x => x.PublicToken == token,
                cancellationToken);

        if (question is null)
        {
            return NotFound();
        }

        var lastMessage = question.Messages
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .FirstOrDefault();

        // A user can send a follow-up only after a developer response.
        if (lastMessage?.AuthorType != UserQuestionAuthorType.Developer)
        {
            return RedirectToPage(new { token });
        }

        var now = DateTimeOffset.UtcNow;

        question.Messages.Add(new UserQuestionMessage
        {
            AuthorType = UserQuestionAuthorType.User,
            Text = message,
            CreatedAtUtc = now
        });

        question.UpdatedAtUtc = now;

        await db.SaveChangesAsync(cancellationToken);

        return RedirectToPage(new { token });
    }

    private async Task LoadQuestionAsync(
        string token,
        CancellationToken cancellationToken)
    {
        UserQuestion = await db.UserQuestions
            .AsNoTracking()
            .Include(x => x.Messages)
            .SingleOrDefaultAsync(
                x => x.PublicToken == token,
                cancellationToken);
    }
}
