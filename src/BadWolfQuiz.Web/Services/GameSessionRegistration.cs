using BadWolfQuiz.Game.Runtime;

namespace BadWolfQuiz.Web.Services;

public sealed class GameSessionRegistration
{
    private long _persistenceRevision;

    public GameSessionRegistration(
        string publicCode,
        GameSession session,
        string? hostId = null,
        bool isRecovered = false)
    {
        PublicCode = publicCode;
        Session = session;
        HostId = hostId;
        IsRecovered = isRecovered;
        RecoveredPlayerIdsAwaitingReconnect = isRecovered
            ? session.Players.Select(player => player.Id).ToHashSet()
            : [];
    }

    public string PublicCode { get; }

    public GameSession Session { get; private set; }
    public string? HostId { get; private set; }

    public string ClientInstanceId { get; } = Guid.NewGuid().ToString("N");

    public bool IsRecovered { get; }

    internal HashSet<GamePlayerId> RecoveredPlayerIdsAwaitingReconnect { get; }

    internal HashSet<GamePlayerId> DisconnectedPlayerIdsAwaitingReconnect { get; } = [];

    internal HashSet<GamePlayerId> UnblockedPlayerIdsAwaitingReconnect { get; } = [];

    public bool AllowsNewPlayers { get; internal set; } = true;

    public long PersistenceRevision => Interlocked.Read(ref _persistenceRevision);

    internal void MarkPersistenceChanged() =>
        Interlocked.Increment(ref _persistenceRevision);

    internal void AssignHost(string hostId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostId);

        if (!string.IsNullOrWhiteSpace(HostId) &&
            !string.Equals(HostId, hostId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "A game session cannot be reassigned to a different host.");
        }

        HostId = hostId;
    }

    public BuzzerRaceSnapshot? BuzzerRace { get; internal set; }

    internal Dictionary<int, AllPlayerTextReviewState> AllPlayerTextReviews { get; } = [];

    internal AllPlayerTextReviewState GetOrCreateAllPlayerTextReview(
        RuntimeQuestion question)
    {
        if (!AllPlayerTextReviews.TryGetValue(
                question.SourceQuestionId,
                out var review))
        {
            review = new AllPlayerTextReviewState();
            AllPlayerTextReviews[question.SourceQuestionId] = review;
        }

        foreach (var attempt in question.AnswerAttempts)
        {
            review.Answers.TryAdd(attempt.PlayerId, "—");
        }

        if (question.Status == RuntimeQuestionStatus.ShowingAnswer)
        {
            review.Accepting = false;
        }

        return review;
    }

    public void RestartSession(TimeProvider? timeProvider = null)
    {
        lock (this)
        {
            var current = Session;
            var currentState = current.CaptureState();
            var fresh = GameSession.Create(
                current.Quiz,
                current.Settings,
                timeProvider ?? TimeProvider.System);
            var freshState = fresh.CaptureState();
            var players = currentState.Players
                .Select(player => player with { Score = 0 })
                .ToArray();
            var removedPlayers = currentState.RemovedPlayers
                .Select(player => player with { Score = 0 })
                .ToArray();
            var activePlayerId = currentState.ActivePlayerId is { } activeId &&
                players.Any(player => player.Id == activeId)
                    ? activeId
                    : players.FirstOrDefault()?.Id;
            var now = (timeProvider ?? TimeProvider.System).GetUtcNow();

            var restartedState = freshState with
            {
                Id = currentState.Id,
                Status = GameSessionStatus.Running,
                ActivePlayerId = activePlayerId,
                CurrentRoundIndex = 0,
                CreatedAtUtc = currentState.CreatedAtUtc,
                StartedAtUtc = now,
                CurrentRoundStartScores = players
                    .Select(player => new PlayerRoundStartScoreState(player.Id, 0))
                    .ToArray(),
                Players = players,
                RemovedPlayers = removedPlayers,
                IsForcedRoundAdvancePending = false,
                FurthestVisitedRoundIndex = 0,
                IsPreviousRoundReturnPending = false,
                IsFinalQuestionAdvancePending = false,
                IsUnfinishedRoundReturnPending = false
            };

            Session = GameSession.Restore(
                current.Quiz,
                current.Settings,
                restartedState,
                timeProvider ?? TimeProvider.System);
            BuzzerRace = null;
            AllPlayerTextReviews.Clear();
            MarkPersistenceChanged();
        }
    }
}

internal sealed class AllPlayerTextReviewState
{
    public Dictionary<GamePlayerId, string> Answers { get; } = [];
    public HashSet<GamePlayerId> JudgedPlayers { get; } = [];
    public bool Accepting { get; set; } = true;
}

public sealed record BuzzerRaceSnapshot(
    int SourceQuestionId,
    DateTimeOffset WinnerPressedAt,
    GamePlayerId WinnerPlayerId,
    string WinnerPlayerName,
    IReadOnlyList<BuzzerRaceLatePlayer> LatePlayers);

public sealed record BuzzerRaceLatePlayer(
    GamePlayerId PlayerId,
    string PlayerName,
    int DelayMilliseconds);
