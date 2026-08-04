using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Services;

public sealed class GameSessionLauncher(
    QuizDbContext db,
    QuizSnapshotFactory snapshotFactory,
    GameSessionRegistry sessionRegistry,
    GameSettingsStore settingsStore,
    CurrentHost currentHost)
{
    public async Task<GameSessionRegistration?> CreateAsync(
        int quizId,
        CancellationToken cancellationToken = default)
    {
        var settings = await settingsStore.LoadAsync(
            currentHost.RequiredId,
            cancellationToken);

        return await CreateAsync(
            quizId,
            settings,
            cancellationToken);
    }

    public async Task<GameSessionRegistration?> CreateAsync(
        int quizId,
        GameSessionSettings settings,
        CancellationToken cancellationToken = default)
    {
        var quiz = await db.Quizzes
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AsSplitQuery()
            .Include(item => item.FinalQuestionBlocks)
            .Include(item => item.FinalAnswerBlocks)
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
                item => item.Id == quizId &&
                    !item.IsArchived &&
                    (item.HostId == currentHost.RequiredId || item.IsPublic),
                cancellationToken);

        if (quiz is null)
        {
            return null;
        }

        var snapshot = snapshotFactory.Create(quiz);
        return sessionRegistry.Create(snapshot, settings, currentHost.RequiredId);
    }
}
