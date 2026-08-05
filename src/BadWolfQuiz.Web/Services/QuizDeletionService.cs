using BadWolfQuiz.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Services;

public interface IQuizDeletionService
{
    Task<DeleteQuizResult> DeletePermanentlyAsync(int quizId, string hostId, CancellationToken cancellationToken = default);
}

public sealed record DeleteQuizResult(bool Succeeded, string Code);

public sealed class QuizDeletionService(
    IDbContextFactory<QuizDbContext> quizDbFactory,
    IDbContextFactory<ArchiveDbContext> archiveDbFactory,
    ILogger<QuizDeletionService> logger) : IQuizDeletionService
{
    public async Task<DeleteQuizResult> DeletePermanentlyAsync(int quizId, string hostId, CancellationToken cancellationToken = default)
    {
        await using var mainDb = await quizDbFactory.CreateDbContextAsync(cancellationToken);
        var quiz = await mainDb.Quizzes.IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.Id == quizId && x.HostId == hostId && x.IsArchived, cancellationToken);
        if (quiz is null) return new(false, "not-found");

        try
        {
            await using (var archiveDb = await archiveDbFactory.CreateDbContextAsync(cancellationToken))
            await using (var archiveTransaction = await archiveDb.Database.BeginTransactionAsync(cancellationToken))
            {
                await archiveDb.ArchivedQuizMedia.Where(x => x.QuizId == quizId && x.HostId == hostId)
                    .ExecuteDeleteAsync(cancellationToken);
                await archiveDb.ArchiveOperations.Where(x => x.QuizId == quizId && x.HostId == hostId)
                    .ExecuteDeleteAsync(cancellationToken);
                await archiveTransaction.CommitAsync(cancellationToken);
            }

            mainDb.Quizzes.Remove(quiz);
            await mainDb.SaveChangesAsync(cancellationToken);
            return new(true, "deleted");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Permanent quiz deletion failed. QuizId={QuizId} HostId={HostId}", quizId, hostId);
            return new(false, "failed");
        }
    }
}
