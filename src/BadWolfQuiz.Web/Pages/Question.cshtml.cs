using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Localization;
using BadWolfQuiz.Web.Models;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace BadWolfQuiz.Web.Pages;

public sealed class QuestionModel(
    QuizDbContext db,
    DiscordQuestionSender questionSender,
    IStringLocalizer<SharedResource> localizer) : PageModel
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
                localizer["Question_InvalidMessage"].Value);

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

        var userMessage = new UserQuestionMessage
        {
            AuthorType = UserQuestionAuthorType.User,
            Text = message,
            CreatedAtUtc = now
        };

        question.Messages.Add(userMessage);
        question.UpdatedAtUtc = now;

        await db.SaveChangesAsync(cancellationToken);

        var discordMessageId = await questionSender.SendAsync(
            question.Id,
            question.SenderName,
            message,
            isFirstMessage: false,
            cancellationToken: cancellationToken);

        if (discordMessageId is null)
        {
            ModelState.AddModelError(
                string.Empty,
                localizer["Question_DiscordSendFailed"].Value);

            await LoadQuestionAsync(token, cancellationToken);
            return Page();
        }

        userMessage.DiscordMessageId = discordMessageId.Value;
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
