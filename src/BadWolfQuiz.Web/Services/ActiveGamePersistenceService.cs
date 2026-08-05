using BadWolfQuiz.Game.Runtime;

namespace BadWolfQuiz.Web.Services;

public sealed class ActiveGamePersistenceService(
    GameSessionRegistry registry,
    ActiveGameStore store,
    TimeProvider timeProvider,
    ILogger<ActiveGamePersistenceService> logger,
    CrashLog crashLog) : BackgroundService
{
    private readonly SemaphoreSlim _persistenceGate = new(1, 1);
    private PersistedGameRevision[]? _lastPersistedRevisions;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        RestoreSavedGames();

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PersistIfChangedAsync();
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Unable to persist active games.");
                crashLog.Write("Active game persistence failed", exception);
            }

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                {
                    break;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            // The host-provided token may already be canceled. Allow the final
            // atomic snapshot to finish during shutdown.
            await PersistIfChangedAsync();
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Unable to save active games during application shutdown.");
            crashLog.Write(
                "Final active game persistence failed during shutdown",
                exception);
        }

        await base.StopAsync(cancellationToken);
    }

    private void RestoreSavedGames()
    {
        foreach (var snapshot in store.GetAll())
        {
            try
            {
                registry.Restore(
                    snapshot.PublicCode,
                    GameSession.Restore(
                        snapshot.Quiz,
                        snapshot.Settings,
                        snapshot.SessionState,
                        timeProvider),
                    snapshot.HostId,
                    snapshot.AllowsNewPlayers);
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Unable to restore active game {PublicCode}.",
                    snapshot.PublicCode);
                crashLog.Write(
                    $"Unable to restore active game {snapshot.PublicCode}",
                    exception);
            }
        }
    }

    internal async Task PersistIfChangedAsync()
    {
        await _persistenceGate.WaitAsync(CancellationToken.None);
        try
        {
            var games = registry.GetAll()
                .Where(game =>
                    !string.IsNullOrWhiteSpace(game.HostId) &&
                    !IsComplete(game.Session))
                .GroupBy(game => new
                {
                    HostId = game.HostId!,
                    game.Session.Quiz.SourceQuizId
                })
                .Select(group => group
                    .OrderByDescending(game => game.Session.CreatedAtUtc)
                    .First())
                .OrderBy(game => game.Session.Id.Value)
                .ToArray();

            var revisions = games
                .Select(game => new PersistedGameRevision(
                    game.Session.Id,
                    game.PersistenceRevision,
                    game.AllowsNewPlayers))
                .ToArray();

            if (_lastPersistedRevisions is not null &&
                revisions.SequenceEqual(_lastPersistedRevisions))
            {
                return;
            }

            var snapshots = games.Select(CaptureSnapshot).ToArray();
            await store.ReplaceAsync(snapshots);
            _lastPersistedRevisions = revisions;
        }
        finally
        {
            _persistenceGate.Release();
        }
    }

    private static bool IsComplete(GameSession session) =>
        session.Status == GameSessionStatus.Completed ||
        session.Quiz.FinalQuestion is null &&
        !session.HasNextRound &&
        session.IsCurrentRoundComplete;

    private ActiveGameSnapshot CaptureSnapshot(
        GameSessionRegistration game)
    {
        lock (game)
        {
            return new ActiveGameSnapshot(
                game.PublicCode,
                game.HostId!,
                game.AllowsNewPlayers,
                game.Session.Quiz,
                game.Session.Settings,
                game.Session.CaptureState(),
                timeProvider.GetUtcNow());
        }
    }

    private sealed record PersistedGameRevision(
        GameSessionId SessionId,
        long Revision,
        bool AllowsNewPlayers);
}
