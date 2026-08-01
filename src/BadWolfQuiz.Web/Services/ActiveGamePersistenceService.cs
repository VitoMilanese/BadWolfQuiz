using BadWolfQuiz.Game.Runtime;

namespace BadWolfQuiz.Web.Services;

public sealed class ActiveGamePersistenceService(
    GameSessionRegistry registry,
    ActiveGameStore store,
    TimeProvider timeProvider,
    ILogger<ActiveGamePersistenceService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        RestoreSavedGames();

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        do
        {
            await SaveAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await SaveAsync(cancellationToken);
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
            }
        }
    }

    private Task SaveAsync(CancellationToken cancellationToken)
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

        return store.ReplaceAsync(snapshots, cancellationToken);
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
