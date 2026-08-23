using System.Diagnostics;
using System.Security.Cryptography;
using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Services;

public interface IQuizMediaArchiveService
{
    Task<ArchiveQuizResult> ArchiveAsync(int quizId, string hostId, CancellationToken cancellationToken = default);
    Task<RestoreQuizResult> RestoreAsync(int quizId, string hostId, CancellationToken cancellationToken = default);
}

public sealed record ArchiveQuizResult(bool Succeeded, string Code, int MediaCount = 0, long MediaBytes = 0);
public sealed record RestoreQuizResult(bool Succeeded, string Code, int MediaCount = 0, long MediaBytes = 0);

public sealed class QuizMediaArchiveService(
    IDbContextFactory<QuizDbContext> quizDbFactory,
    IDbContextFactory<ArchiveDbContext> archiveDbFactory,
    ILogger<QuizMediaArchiveService> logger) : IQuizMediaArchiveService
{
    public async Task<ArchiveQuizResult> ArchiveAsync(
        int quizId,
        string hostId,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        Guid operationId;
        bool replaceExistingArchive;
        await using (var db = await quizDbFactory.CreateDbContextAsync(cancellationToken))
        {
            var quiz = await db.Quizzes.IgnoreQueryFilters().AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == quizId && x.HostId == hostId && !x.IsArchived, cancellationToken);
            if (quiz is null) return new(false, "not-found");
            if (quiz.MediaState == QuizMediaState.Archived) return new(true, "already-archived", quiz.ArchivedMediaCount, quiz.ArchivedMediaBytes);
            if (quiz.MediaState is QuizMediaState.Restoring) return new(false, "busy");
            if (quiz.MediaState == QuizMediaState.Failed && quiz.ArchivedMediaCount > 0) return new(false, "restore-required");
            if (await db.GameSessions.IgnoreQueryFilters().AnyAsync(
                x => x.QuizId == quizId && x.HostId == hostId && x.Status != GameSessionStatus.Finished,
                cancellationToken)) return new(false, "active-game");
            replaceExistingArchive = quiz.MediaState != QuizMediaState.Archiving &&
                quiz.CurrentArchiveOperationId.HasValue;
            operationId = quiz.CurrentArchiveOperationId ?? Guid.NewGuid();

            if (quiz.MediaState != QuizMediaState.Archiving)
            {
                var claimed = await db.Quizzes.IgnoreQueryFilters()
                    .Where(x => x.Id == quizId && x.HostId == hostId &&
                        (x.MediaState == QuizMediaState.Active || x.MediaState == QuizMediaState.Failed))
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(x => x.MediaState, QuizMediaState.Archiving)
                        .SetProperty(x => x.CurrentArchiveOperationId, operationId)
                        .SetProperty(x => x.MediaArchiveFailureReason, (string?)null), cancellationToken);
                if (claimed != 1) return new(false, "busy");
            }
        }

        try
        {
            var media = await ReadMediaAsync(quizId, hostId, operationId, cancellationToken);
            if (media.Count == 0)
            {
                await RestoreActiveStateAsync(quizId, hostId, operationId, cancellationToken);
                return new(false, "no-media");
            }

            await CopyToArchiveAsync(
                media,
                operationId,
                quizId,
                hostId,
                replaceExistingArchive,
                cancellationToken);
            await VerifyArchiveAsync(media, operationId, quizId, hostId, cancellationToken);
            var bytes = media.Sum(x => x.Length);
            await ClearMainMediaAsync(media, operationId, quizId, hostId, bytes, cancellationToken);
            await CompleteOperationAsync(operationId, media.Count, bytes, cancellationToken);

            logger.LogInformation(
                "Media archive completed. OperationId={OperationId} QuizId={QuizId} HostId={HostId} MediaCount={MediaCount} MediaBytes={MediaBytes} DurationMs={DurationMs}",
                operationId, quizId, hostId, media.Count, bytes, stopwatch.ElapsedMilliseconds);
            return new(true, "archived", media.Count, bytes);
        }
        catch (Exception exception)
        {
            logger.LogError(exception,
                "Media archive failed. OperationId={OperationId} QuizId={QuizId} HostId={HostId} DurationMs={DurationMs}",
                operationId, quizId, hostId, stopwatch.ElapsedMilliseconds);
            await MarkArchiveFailedAsync(quizId, hostId, "Media archiving failed. Retry the operation.", CancellationToken.None);
            await MarkOperationFailedAsync(operationId, "Media archiving failed.", CancellationToken.None);
            return new(false, "failed");
        }
    }

    public async Task<RestoreQuizResult> RestoreAsync(
        int quizId,
        string hostId,
        CancellationToken cancellationToken = default)
    {
        Guid operationId;
        int expectedCount;
        await using (var db = await quizDbFactory.CreateDbContextAsync(cancellationToken))
        {
            var quiz = await db.Quizzes.IgnoreQueryFilters().AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == quizId && x.HostId == hostId && !x.IsArchived, cancellationToken);
            if (quiz is null) return new(false, "not-found");
            if (quiz.MediaState == QuizMediaState.Active) return new(true, "already-active");
            if ((quiz.MediaState is not (QuizMediaState.Archived or QuizMediaState.Restoring) &&
                 !(quiz.MediaState == QuizMediaState.Failed && quiz.ArchivedMediaCount > 0)) ||
                !quiz.CurrentArchiveOperationId.HasValue) return new(false, "invalid-state");
            operationId = quiz.CurrentArchiveOperationId.Value;
            expectedCount = quiz.ArchivedMediaCount;
            if (quiz.MediaState is QuizMediaState.Archived or QuizMediaState.Failed)
            {
                var claimed = await db.Quizzes.IgnoreQueryFilters()
                    .Where(x => x.Id == quizId && x.HostId == hostId &&
                        (x.MediaState == QuizMediaState.Archived || x.MediaState == QuizMediaState.Failed))
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(x => x.MediaState, QuizMediaState.Restoring)
                        .SetProperty(x => x.MediaArchiveFailureReason, (string?)null), cancellationToken);
                if (claimed != 1) return new(false, "busy");
            }
        }

        try
        {
            await using var archiveDb = await archiveDbFactory.CreateDbContextAsync(cancellationToken);
            var media = await archiveDb.ArchivedQuizMedia.AsNoTracking()
                .Where(x => x.QuizId == quizId && x.HostId == hostId && x.OperationId == operationId)
                .OrderBy(x => x.Id).ToListAsync(cancellationToken);
            if (media.Count == 0 || media.Count != expectedCount) throw new InvalidDataException("Archive media count mismatch.");
            VerifyChecksums(media);
            await RestoreMainMediaAsync(media, operationId, quizId, hostId, cancellationToken);
            var bytes = media.Sum(x => x.Length);
            var operation = await archiveDb.ArchiveOperations.SingleAsync(x => x.Id == operationId, cancellationToken);
            operation.RestoredAtUtc = DateTime.UtcNow;
            await archiveDb.SaveChangesAsync(cancellationToken);
            return new(true, "restored", media.Count, bytes);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Media restore failed. OperationId={OperationId} QuizId={QuizId} HostId={HostId}", operationId, quizId, hostId);
            await MarkArchiveFailedAsync(quizId, hostId, "Media restore failed. The archive was not changed.", CancellationToken.None);
            return new(false, "failed");
        }
    }

    private async Task<List<ArchivedQuizMedia>> ReadMediaAsync(int quizId, string hostId, Guid operationId, CancellationToken token)
    {
        await using var db = await quizDbFactory.CreateDbContextAsync(token);
        var ownsQuiz = await db.Quizzes.IgnoreQueryFilters().AnyAsync(x => x.Id == quizId && x.HostId == hostId, token);
        if (!ownsQuiz) throw new InvalidOperationException("Quiz ownership changed.");
        var now = DateTime.UtcNow;
        var result = new List<ArchivedQuizMedia>();
        await AddAsync(db.QuestionContentBlocks.IgnoreQueryFilters().Where(x => x.Question.Category.Round.QuizId == quizId && x.FileData != null && x.FileData.Length > 0), ArchivedMediaRole.QuestionBlock);
        await AddAsync(db.AnswerContentBlocks.IgnoreQueryFilters().Where(x => x.Question.Category.Round.QuizId == quizId && x.FileData != null && x.FileData.Length > 0), ArchivedMediaRole.AnswerBlock);
        await AddAsync(db.FinalDescriptionContentBlocks.IgnoreQueryFilters().Where(x => x.QuizId == quizId && x.FileData != null && x.FileData.Length > 0), ArchivedMediaRole.FinalDescriptionBlock);
        await AddAsync(db.FinalQuestionContentBlocks.IgnoreQueryFilters().Where(x => x.QuizId == quizId && x.FileData != null && x.FileData.Length > 0), ArchivedMediaRole.FinalQuestionBlock);
        await AddAsync(db.FinalAnswerContentBlocks.IgnoreQueryFilters().Where(x => x.QuizId == quizId && x.FileData != null && x.FileData.Length > 0), ArchivedMediaRole.FinalAnswerBlock);
        return result;

        async Task AddAsync<T>(IQueryable<T> query, ArchivedMediaRole role) where T : ContentBlockBase
        {
            var items = await query.Select(x => new { x.Id, x.FileData, x.FileContentType, x.FileName }).ToListAsync(token);
            foreach (var item in items)
            {
                var data = item.FileData!;
                result.Add(new ArchivedQuizMedia
                {
                    OperationId = operationId, QuizId = quizId, HostId = hostId, EntityId = item.Id, Role = role,
                    ContentType = item.FileContentType, OriginalFileName = item.FileName, Data = data,
                    Length = data.LongLength, Sha256 = Convert.ToHexString(SHA256.HashData(data)), ArchivedAtUtc = now
                });
            }
        }
    }

    private async Task CopyToArchiveAsync(
        List<ArchivedQuizMedia> media,
        Guid operationId,
        int quizId,
        string hostId,
        bool replaceExistingArchive,
        CancellationToken token)
    {
        await using var db = await archiveDbFactory.CreateDbContextAsync(token);
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        var operation = await db.ArchiveOperations.SingleOrDefaultAsync(x => x.Id == operationId, token);
        if (operation is null)
        {
            db.ArchiveOperations.Add(new ArchiveOperation { Id = operationId, QuizId = quizId, HostId = hostId, State = ArchiveOperationState.Creating, CreatedAtUtc = DateTime.UtcNow });
        }
        else if (replaceExistingArchive)
        {
            await db.ArchivedQuizMedia
                .Where(x => x.OperationId == operationId)
                .ExecuteDeleteAsync(token);
            operation.State = ArchiveOperationState.Creating;
            operation.MediaCount = 0;
            operation.MediaBytes = 0;
            operation.CompletedAtUtc = null;
            operation.RestoredAtUtc = null;
            operation.OrphanedAtUtc = null;
            operation.FailureReason = null;
            db.ArchivedQuizMedia.AddRange(media);
            await db.SaveChangesAsync(token);
            await transaction.CommitAsync(token);
            return;
        }
        var existing = await db.ArchivedQuizMedia.Where(x => x.OperationId == operationId)
            .Select(x => new { x.EntityId, x.Role }).ToListAsync(token);
        var keys = existing.Select(x => (x.EntityId, x.Role)).ToHashSet();
        db.ArchivedQuizMedia.AddRange(media.Where(x => !keys.Contains((x.EntityId, x.Role))));
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
    }

    private async Task VerifyArchiveAsync(List<ArchivedQuizMedia> expected, Guid operationId, int quizId, string hostId, CancellationToken token)
    {
        await using var db = await archiveDbFactory.CreateDbContextAsync(token);
        var actual = await db.ArchivedQuizMedia.AsNoTracking().Where(x => x.OperationId == operationId && x.QuizId == quizId && x.HostId == hostId).ToListAsync(token);
        if (actual.Count != expected.Count || actual.Sum(x => x.Length) != expected.Sum(x => x.Length)) throw new InvalidDataException("Archive verification totals mismatch.");
        VerifyChecksums(actual);
        var expectedKeys = expected.ToDictionary(x => (x.EntityId, x.Role));
        foreach (var item in actual)
        {
            if (!expectedKeys.TryGetValue((item.EntityId, item.Role), out var source) || item.Sha256 != source.Sha256 || item.Length != source.Length)
                throw new InvalidDataException("Archive verification item mismatch.");
        }
        var operation = await db.ArchiveOperations.SingleAsync(x => x.Id == operationId, token);
        operation.State = ArchiveOperationState.Verified;
        await db.SaveChangesAsync(token);
    }

    private static void VerifyChecksums(IEnumerable<ArchivedQuizMedia> media)
    {
        foreach (var item in media)
            if (item.Data.LongLength != item.Length || Convert.ToHexString(SHA256.HashData(item.Data)) != item.Sha256)
                throw new InvalidDataException("Archived media checksum mismatch.");
    }

    private async Task ClearMainMediaAsync(List<ArchivedQuizMedia> media, Guid operationId, int quizId, string hostId, long bytes, CancellationToken token)
    {
        await using var db = await quizDbFactory.CreateDbContextAsync(token);
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        foreach (var group in media.GroupBy(x => x.Role))
        {
            var ids = group.Select(x => x.EntityId).ToArray();
            await ClearRoleAsync(db, group.Key, ids, token);
        }
        var changed = await db.Quizzes.IgnoreQueryFilters().Where(x => x.Id == quizId && x.HostId == hostId && x.MediaState == QuizMediaState.Archiving && x.CurrentArchiveOperationId == operationId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.MediaState, QuizMediaState.Archived)
                .SetProperty(x => x.ArchivedMediaCount, media.Count).SetProperty(x => x.ArchivedMediaBytes, bytes)
                .SetProperty(x => x.MediaArchivedAtUtc, DateTime.UtcNow).SetProperty(x => x.MediaArchiveFailureReason, (string?)null), token);
        if (changed != 1) throw new InvalidOperationException("Archive operation state changed.");
        await transaction.CommitAsync(token);
    }

    private static Task ClearRoleAsync(QuizDbContext db, ArchivedMediaRole role, int[] ids, CancellationToken token) => role switch
    {
        ArchivedMediaRole.QuestionBlock => db.QuestionContentBlocks.IgnoreQueryFilters().Where(x => ids.Contains(x.Id)).ExecuteUpdateAsync(s => s.SetProperty(x => x.FileData, (byte[]?)null), token),
        ArchivedMediaRole.AnswerBlock => db.AnswerContentBlocks.IgnoreQueryFilters().Where(x => ids.Contains(x.Id)).ExecuteUpdateAsync(s => s.SetProperty(x => x.FileData, (byte[]?)null), token),
        ArchivedMediaRole.FinalQuestionBlock => db.FinalQuestionContentBlocks.IgnoreQueryFilters().Where(x => ids.Contains(x.Id)).ExecuteUpdateAsync(s => s.SetProperty(x => x.FileData, (byte[]?)null), token),
        ArchivedMediaRole.FinalAnswerBlock => db.FinalAnswerContentBlocks.IgnoreQueryFilters().Where(x => ids.Contains(x.Id)).ExecuteUpdateAsync(s => s.SetProperty(x => x.FileData, (byte[]?)null), token),
        ArchivedMediaRole.FinalDescriptionBlock => db.FinalDescriptionContentBlocks.IgnoreQueryFilters().Where(x => ids.Contains(x.Id)).ExecuteUpdateAsync(s => s.SetProperty(x => x.FileData, (byte[]?)null), token),
        _ => throw new InvalidDataException("Unknown media role.")
    };

    private async Task RestoreMainMediaAsync(List<ArchivedQuizMedia> media, Guid operationId, int quizId, string hostId, CancellationToken token)
    {
        await using var db = await quizDbFactory.CreateDbContextAsync(token);
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        foreach (var item in media)
        {
            var changed = await RestoreItemAsync(db, item, token);
            if (changed != 1) throw new InvalidDataException("An archived media target no longer exists.");
        }
        var stateChanged = await db.Quizzes.IgnoreQueryFilters().Where(x => x.Id == quizId && x.HostId == hostId && x.MediaState == QuizMediaState.Restoring && x.CurrentArchiveOperationId == operationId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.MediaState, QuizMediaState.Active)
                .SetProperty(x => x.MediaRestoredAtUtc, DateTime.UtcNow).SetProperty(x => x.MediaArchiveFailureReason, (string?)null), token);
        if (stateChanged != 1) throw new InvalidOperationException("Restore operation state changed.");
        await transaction.CommitAsync(token);
    }

    private static Task<int> RestoreItemAsync(QuizDbContext db, ArchivedQuizMedia item, CancellationToken token) => item.Role switch
    {
        ArchivedMediaRole.QuestionBlock => db.QuestionContentBlocks.IgnoreQueryFilters().Where(x => x.Id == item.EntityId).ExecuteUpdateAsync(s => s.SetProperty(x => x.FileData, item.Data), token),
        ArchivedMediaRole.AnswerBlock => db.AnswerContentBlocks.IgnoreQueryFilters().Where(x => x.Id == item.EntityId).ExecuteUpdateAsync(s => s.SetProperty(x => x.FileData, item.Data), token),
        ArchivedMediaRole.FinalQuestionBlock => db.FinalQuestionContentBlocks.IgnoreQueryFilters().Where(x => x.Id == item.EntityId).ExecuteUpdateAsync(s => s.SetProperty(x => x.FileData, item.Data), token),
        ArchivedMediaRole.FinalAnswerBlock => db.FinalAnswerContentBlocks.IgnoreQueryFilters().Where(x => x.Id == item.EntityId).ExecuteUpdateAsync(s => s.SetProperty(x => x.FileData, item.Data), token),
        ArchivedMediaRole.FinalDescriptionBlock => db.FinalDescriptionContentBlocks.IgnoreQueryFilters().Where(x => x.Id == item.EntityId).ExecuteUpdateAsync(s => s.SetProperty(x => x.FileData, item.Data), token),
        _ => throw new InvalidDataException("Unknown media role.")
    };

    private async Task CompleteOperationAsync(Guid id, int count, long bytes, CancellationToken token)
    {
        await using var db = await archiveDbFactory.CreateDbContextAsync(token);
        await db.ArchiveOperations.Where(x => x.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.State, ArchiveOperationState.Completed)
            .SetProperty(x => x.MediaCount, count).SetProperty(x => x.MediaBytes, bytes).SetProperty(x => x.CompletedAtUtc, DateTime.UtcNow), token);
    }

    private async Task MarkArchiveFailedAsync(int quizId, string hostId, string reason, CancellationToken token)
    {
        await using var db = await quizDbFactory.CreateDbContextAsync(token);
        await db.Quizzes.IgnoreQueryFilters().Where(x => x.Id == quizId && x.HostId == hostId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.MediaState, QuizMediaState.Failed).SetProperty(x => x.MediaArchiveFailureReason, reason), token);
    }

    private async Task RestoreActiveStateAsync(
        int quizId,
        string hostId,
        Guid operationId,
        CancellationToken token)
    {
        await using var db = await quizDbFactory.CreateDbContextAsync(token);
        await db.Quizzes.IgnoreQueryFilters()
            .Where(x => x.Id == quizId && x.HostId == hostId &&
                x.MediaState == QuizMediaState.Archiving &&
                x.CurrentArchiveOperationId == operationId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.MediaState, QuizMediaState.Active)
                .SetProperty(x => x.MediaArchiveFailureReason, (string?)null), token);
    }

    private async Task MarkOperationFailedAsync(Guid id, string reason, CancellationToken token)
    {
        await using var db = await archiveDbFactory.CreateDbContextAsync(token);
        await db.ArchiveOperations.Where(x => x.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.State, ArchiveOperationState.Failed).SetProperty(x => x.FailureReason, reason), token);
    }
}
