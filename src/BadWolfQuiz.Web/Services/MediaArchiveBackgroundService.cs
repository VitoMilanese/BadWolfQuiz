using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BadWolfQuiz.Web.Services;

public sealed class MediaArchiveBackgroundService(
    IServiceScopeFactory scopeFactory,
    IDbContextFactory<QuizDbContext> quizDbFactory,
    IDbContextFactory<ArchiveDbContext> archiveDbFactory,
    IOptions<MediaArchiveOptions> options,
    TimeProvider timeProvider,
    ILogger<MediaArchiveBackgroundService> logger) : BackgroundService
{
    private readonly SemaphoreSlim _scanLock = new(1, 1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RecoverInterruptedOperationsAsync(stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            var nextScanUtc = CalculateNextScanUtc(
                timeProvider.GetUtcNow(),
                TimeSpan.FromHours(options.Value.ScanIntervalHours),
                options.Value.ScanStartTimeUtc);
            var delay = nextScanUtc - timeProvider.GetUtcNow();
            if (delay > TimeSpan.Zero)
            {
                logger.LogInformation(
                    "Next automatic media archive scan scheduled for {NextScanUtc}",
                    nextScanUtc);
                await Task.Delay(delay, timeProvider, stoppingToken);
            }
            if (options.Value.Enabled) await ScanAsync(stoppingToken);
        }
    }

    internal static DateTimeOffset CalculateNextScanUtc(
        DateTimeOffset nowUtc,
        TimeSpan interval,
        TimeSpan startTimeUtc)
    {
        if (interval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(interval));
        if (startTimeUtc < TimeSpan.Zero || startTimeUtc >= TimeSpan.FromDays(1))
            throw new ArgumentOutOfRangeException(nameof(startTimeUtc));

        var anchor = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero)
            .Add(startTimeUtc);
        if (nowUtc < anchor) return anchor;

        var elapsedTicks = (nowUtc - anchor).Ticks;
        var intervalCount = elapsedTicks / interval.Ticks + 1;
        return anchor.AddTicks(checked(intervalCount * interval.Ticks));
    }

    internal async Task ScanAsync(CancellationToken token)
    {
        if (!await _scanLock.WaitAsync(0, token)) return;
        try
        {
            var cutoff = timeProvider.GetUtcNow().UtcDateTime.AddDays(-options.Value.ArchiveAfterDays);
            await using var db = await quizDbFactory.CreateDbContextAsync(token);
            var candidates = await db.Quizzes.IgnoreQueryFilters().AsNoTracking()
                .Where(x => !x.IsArchived && x.HostId != null && x.MediaState == QuizMediaState.Active &&
                    !x.PreventAutomaticArchiving && (x.LastPlayedAtUtc ?? x.UpdatedAtUtc) < cutoff &&
                    !db.GameSessions.IgnoreQueryFilters().Any(s => s.QuizId == x.Id && s.Status != GameSessionStatus.Finished) &&
                    (db.QuestionContentBlocks.IgnoreQueryFilters().Any(b => b.Question.Category.Round.QuizId == x.Id && b.FileData != null && b.FileData.Length > 0) ||
                     db.AnswerContentBlocks.IgnoreQueryFilters().Any(b => b.Question.Category.Round.QuizId == x.Id && b.FileData != null && b.FileData.Length > 0) ||
                     db.FinalQuestionContentBlocks.IgnoreQueryFilters().Any(b => b.QuizId == x.Id && b.FileData != null && b.FileData.Length > 0) ||
                     db.FinalAnswerContentBlocks.IgnoreQueryFilters().Any(b => b.QuizId == x.Id && b.FileData != null && b.FileData.Length > 0)))
                .OrderBy(x => x.LastPlayedAtUtc ?? x.UpdatedAtUtc)
                .Select(x => new { x.Id, HostId = x.HostId! })
                .Take(options.Value.MaximumQuizzesPerRun).ToListAsync(token);

            foreach (var candidate in candidates)
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<IQuizMediaArchiveService>();
                    await service.ArchiveAsync(candidate.Id, candidate.HostId, token);
                }
                catch (Exception exception) when (!token.IsCancellationRequested)
                {
                    logger.LogError(exception, "Automatic media archive failed. QuizId={QuizId} HostId={HostId}", candidate.Id, candidate.HostId);
                }
            }
            await CleanupAsync(token);
        }
        finally { _scanLock.Release(); }
    }

    private async Task RecoverInterruptedOperationsAsync(CancellationToken token)
    {
        await using var db = await quizDbFactory.CreateDbContextAsync(token);
        var interrupted = await db.Quizzes.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.MediaState == QuizMediaState.Archiving || x.MediaState == QuizMediaState.Restoring)
            .Where(x => x.HostId != null)
            .Select(x => new { x.Id, HostId = x.HostId!, x.MediaState })
            .ToListAsync(token);
        foreach (var quiz in interrupted)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IQuizMediaArchiveService>();
                if (quiz.MediaState == QuizMediaState.Archiving)
                    await service.ArchiveAsync(quiz.Id, quiz.HostId, token);
                else
                    await service.RestoreAsync(quiz.Id, quiz.HostId, token);
            }
            catch (Exception exception) when (!token.IsCancellationRequested)
            {
                logger.LogError(exception, "Interrupted media operation recovery failed. QuizId={QuizId} HostId={HostId}", quiz.Id, quiz.HostId);
            }
        }
    }

    private async Task CleanupAsync(CancellationToken token)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        await using var archiveDb = await archiveDbFactory.CreateDbContextAsync(token);
        await using var quizDb = await quizDbFactory.CreateDbContextAsync(token);
        var groups = await archiveDb.ArchiveOperations.Where(x => x.State == ArchiveOperationState.Completed)
            .Select(x => new { x.Id, x.QuizId, x.HostId, x.RestoredAtUtc, x.OrphanedAtUtc }).ToListAsync(token);
        foreach (var group in groups)
        {
            var quiz = await quizDb.Quizzes.IgnoreQueryFilters().AsNoTracking()
                .Where(x => x.Id == group.QuizId && x.HostId == group.HostId)
                .Select(x => new { x.MediaState, x.CurrentArchiveOperationId }).SingleOrDefaultAsync(token);
            var operation = await archiveDb.ArchiveOperations.SingleAsync(x => x.Id == group.Id, token);
            if (quiz is null)
            {
                operation.OrphanedAtUtc ??= now;
                if (operation.OrphanedAtUtc <= now.AddDays(-options.Value.OrphanRetentionDays))
                {
                    await archiveDb.ArchivedQuizMedia.Where(x => x.OperationId == group.Id).ExecuteDeleteAsync(token);
                    archiveDb.ArchiveOperations.Remove(operation);
                }
            }
            else
            {
                operation.OrphanedAtUtc = null;
                if (group.RestoredAtUtc <= now.AddDays(-options.Value.DeleteArchiveCopyAfterRestoreDays) &&
                    quiz.MediaState == QuizMediaState.Active && quiz.CurrentArchiveOperationId == group.Id)
                {
                    await archiveDb.ArchivedQuizMedia.Where(x => x.OperationId == group.Id).ExecuteDeleteAsync(token);
                    archiveDb.ArchiveOperations.Remove(operation);
                }
            }
        }
        await archiveDb.SaveChangesAsync(token);
    }
}
