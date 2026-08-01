using BadWolfQuiz.Game.Runtime;

namespace BadWolfQuiz.Web.Services;

public sealed class ActiveGamePersistenceService(
    GameSessionRegistry registry,
    ActiveGameStore store,
    TimeProvider timeProvider,
    ILogger<ActiveGamePersistenceService> logger,
    CrashLog crashLog) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        RestoreSavedGames();

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SaveAsync();
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
            // The host-provided token may already be canceled. The final atomic
            // snapshot is small and must be allowed to finish during shutdown.
            await SaveAsync();
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

    private Task SaveAsync()
    {
        var snapshots = registry.GetAll()
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
            .Select(CaptureSnapshot)
            .ToArray();

        return store.ReplaceAsync(snapshots);
    }

    private static bool IsComplete(GameSession session) =>
        session.Status == GameSessionStatus.Completed ||
        session.Quiz.FinalQuestion is null &&
        !session.HasNextRound &&
        session.IsCurrentRoundComplete;

    private static ActiveGameSnapshot CaptureSnapshot(
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
                game.Session.CaptureState());
        }
    }
}
