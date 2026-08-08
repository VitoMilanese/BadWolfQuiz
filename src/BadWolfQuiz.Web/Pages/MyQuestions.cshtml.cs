using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Pages;

public sealed class MyQuestionsModel(
    QuizDbContext db,
    UserQuestionHistoryService questionHistory,
    UserQuestionDeletionService deletionService) : PageModel
{
    public IReadOnlyList<QuestionListItem> Questions { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostDeleteAsync(
        string token,
        CancellationToken cancellationToken)
    {
        var savedTokens = questionHistory.GetTokens(Request);
        if (!savedTokens.Contains(token, StringComparer.Ordinal))
        {
            return BadRequest();
        }

        var questionId = await db.UserQuestions
            .AsNoTracking()
            .Where(question => question.PublicToken == token)
            .Select(question => (int?)question.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (questionId.HasValue)
        {
            await deletionService.DeleteAsync(questionId.Value, cancellationToken);
        }

        questionHistory.Remove(Request, Response, token);

        return RedirectToPage();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var savedTokens = questionHistory.GetTokens(Request);
        if (savedTokens.Count == 0)
        {
            Questions = [];
            return;
        }

        var questions = await db.UserQuestions
            .AsNoTracking()
            .Include(question => question.Messages)
            .Where(question => savedTokens.Contains(question.PublicToken))
            .ToListAsync(cancellationToken);

        var validTokens = questions
            .Select(question => question.PublicToken)
            .ToHashSet(StringComparer.Ordinal);

        if (validTokens.Count != savedTokens.Count)
        {
            questionHistory.Replace(
                Response,
                savedTokens.Where(validTokens.Contains));
        }

        Questions = questions
            .OrderByDescending(question => question.UpdatedAtUtc)
            .Select(question =>
            {
                var firstMessage = question.Messages
                    .OrderBy(message => message.CreatedAtUtc)
                    .ThenBy(message => message.Id)
                    .FirstOrDefault();

                var lastMessage = question.Messages
                    .OrderByDescending(message => message.CreatedAtUtc)
                    .ThenByDescending(message => message.Id)
                    .FirstOrDefault();

                return new QuestionListItem(
                    question.PublicToken,
                    firstMessage?.Text ?? string.Empty,
                    question.UpdatedAtUtc,
                    lastMessage?.AuthorType == UserQuestionAuthorType.User);
            })
            .ToArray();
    }

    public sealed record QuestionListItem(
        string Token,
        string Preview,
        DateTimeOffset UpdatedAtUtc,
        bool AwaitingDeveloper);
}
