using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Pages.Admin;

public sealed class QuestionInboxModel(QuizDbContext db) : PageModel
{
    public IReadOnlyList<UserQuestion> Questions { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadQuestionsAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostAnswerAsync(
        int id,
        string answer,
        CancellationToken cancellationToken)
    {
        answer = answer?.Trim() ?? string.Empty;

        if (answer.Length is < 1 or > 5000)
        {
            ModelState.AddModelError(
                string.Empty,
                "Відповідь має містити від 1 до 5000 символів.");

            await LoadQuestionsAsync(cancellationToken);
            return Page();
        }

        var question = await db.UserQuestions
            .Include(x => x.Messages)
            .SingleOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);

        if (question is null)
        {
            return NotFound();
        }

        var now = DateTimeOffset.UtcNow;

        question.Messages.Add(new UserQuestionMessage
        {
            AuthorType = UserQuestionAuthorType.Developer,
            Text = answer,
            CreatedAtUtc = now
        });

        question.UpdatedAtUtc = now;

        await db.SaveChangesAsync(cancellationToken);

        return RedirectToPage();
    }

    private async Task LoadQuestionsAsync(CancellationToken cancellationToken)
    {
        var questions = await db.UserQuestions
            .AsNoTracking()
            .Include(x => x.Messages)
            .ToListAsync(cancellationToken);

        Questions = questions
            .OrderBy(x =>
                x.Messages
                    .OrderByDescending(message => message.CreatedAtUtc)
                    .FirstOrDefault()?.AuthorType
                == UserQuestionAuthorType.Developer)
            .ThenByDescending(x => x.UpdatedAtUtc)
            .ToList();
    }
}
