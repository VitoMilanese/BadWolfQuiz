using BadWolfQuiz.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BadWolfQuiz.Web.Services;

public sealed class UserQuestionCleanupService(
    QuizDbContext db,
    IOptions<UserQuestionOptions> options)
{
    public async Task CleanupAsync(
        CancellationToken cancellationToken = default)
    {
        var cutoff = DateTimeOffset.UtcNow
            .AddMonths(-options.Value.RetentionMonths);

        var questions = await db.UserQuestions
            .AsNoTracking()
            .Select(x => new
            {
                x.Id,
                x.UpdatedAtUtc
            })
            .ToListAsync(cancellationToken);

        var expiredIds = questions
            .Where(x => x.UpdatedAtUtc < cutoff)
            .Select(x => x.Id)
            .ToList();

        if (expiredIds.Count == 0)
        {
            return;
        }

        var expiredQuestions = await db.UserQuestions
            .Where(x => expiredIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        db.UserQuestions.RemoveRange(expiredQuestions);

        await db.SaveChangesAsync(cancellationToken);
    }
}
