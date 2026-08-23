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
    private readonly Dictionary<(string HostId, int SourceQuizId), CommittedGame>
        _committedGames = store.GetAll()
            .Where(snapshot => HasPersistableGameplay(snapshot.SessionState))
            .GroupBy(snapshot =>
                (snapshot.HostId, snapshot.Quiz.SourceQuizId))
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var snapshot = group
                        .OrderByDescending(item => item.SessionState.CreatedAtUtc)
                        .First();
                    return new CommittedGame(
                        snapshot.SessionState.Id,
                        snapshot.SessionState.CreatedAtUtc);
                });
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
        foreach (var snapshot in store.GetAll()
                     .Where(snapshot => HasPersistableGameplay(snapshot.SessionState)))
        {
            try
            {
                var game = registry.Restore(
                    snapshot.PublicCode,
                    GameSession.Restore(
                        snapshot.Quiz,
                        snapshot.Settings,
                        snapshot.SessionState,
                        timeProvider),
                    snapshot.HostId,
                    snapshot.AllowsNewPlayers);
                game.RestoreQuestionOpenSequence(snapshot.QuestionOpenSequence);
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
            var games = SelectPersistableGames();

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

    private GameSessionRegistration[] SelectPersistableGames()
    {
        var selected = new List<GameSessionRegistration>();
        var groups = registry.GetAll()
            .Where(game => !string.IsNullOrWhiteSpace(game.HostId))
            .GroupBy(game =>
                (HostId: game.HostId!, game.Session.Quiz.SourceQuizId));

        foreach (var group in groups)
        {
            _committedGames.TryGetValue(group.Key, out var committed);

            var persistable = group
                .Where(game => IsPersistable(game.Session))
                .OrderByDescending(game => game.Session.CreatedAtUtc)
                .ToArray();

            if (committed is null)
            {
                var first = persistable.FirstOrDefault();
                if (first is null)
                {
                    continue;
                }

                committed = new CommittedGame(
                    first.Session.Id,
                    first.Session.CreatedAtUtc);
                _committedGames[group.Key] = committed;
            }
            else
            {
                var replacement = persistable.FirstOrDefault(game =>
                    game.Session.CreatedAtUtc > committed.CreatedAtUtc);
                if (replacement is not null)
                {
                    committed = new CommittedGame(
                        replacement.Session.Id,
                        replacement.Session.CreatedAtUtc);
                    _committedGames[group.Key] = committed;
                }
            }

            var current = persistable.FirstOrDefault(game =>
                game.Session.Id == committed.SessionId);
            if (current is not null)
            {
                selected.Add(current);
            }
        }

        return selected
            .OrderBy(game => game.Session.Id.Value)
            .ToArray();
    }

    private static bool IsPersistable(GameSession session) =>
        !IsComplete(session) &&
        (session.Board.Questions.Any(question =>
             question.Status != RuntimeQuestionStatus.Available) ||
         IsFinalQuestionInProgress(session.Status));

    private static bool HasPersistableGameplay(GameSessionState state) =>
        state.Questions.Any(question =>
            question.Status != RuntimeQuestionStatus.Available) ||
        IsFinalQuestionInProgress(state.Status);

    private static bool IsFinalQuestionInProgress(GameSessionStatus status) =>
        status is
            GameSessionStatus.FinalWagering or
            GameSessionStatus.FinalAnswering or
            GameSessionStatus.FinalJudging;

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
                timeProvider.GetUtcNow(),
                game.CaptureQuestionOpenSequence());
        }
    }

    private sealed record PersistedGameRevision(
        GameSessionId SessionId,
        long Revision,
        bool AllowsNewPlayers);

    private sealed record CommittedGame(
        GameSessionId SessionId,
        DateTimeOffset CreatedAtUtc);
}