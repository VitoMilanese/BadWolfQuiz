using BadWolfQuiz.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Services;

public sealed class GameSessionLauncher(
    QuizDbContext db,
    QuizSnapshotFactory snapshotFactory,
    GameSessionRegistry sessionRegistry)
{
    public async Task<GameSessionRegistration?> CreateAsync(
        int quizId,
        CancellationToken cancellationToken = default)
    {
        var quiz = await db.Quizzes
            .AsNoTracking()
            .AsSplitQuery()
            .Include(item => item.Rounds)
                .ThenInclude(round => round.Rows)
            .Include(item => item.Rounds)
                .ThenInclude(round => round.Categories)
                    .ThenInclude(category => category.Questions)
                        .ThenInclude(question => question.QuestionBlocks)
            .Include(item => item.Rounds)
                .ThenInclude(round => round.Categories)
                    .ThenInclude(category => category.Questions)
                        .ThenInclude(question => question.AnswerBlocks)
            .SingleOrDefaultAsync(
                item => item.Id == quizId && !item.IsArchived,
                cancellationToken);

        if (quiz is null)
        {
            return null;
        }

        var snapshot = snapshotFactory.Create(quiz);
        return sessionRegistry.Create(snapshot);
    }
}
