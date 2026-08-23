using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Game.Runtime;

namespace BadWolfQuiz.Web.Services;

public sealed class GameSessionRegistration
{
    private long _persistenceRevision;
    private long _nextQuestionOpenSequence;
    private readonly Dictionary<int, long> _questionOpenSequence = [];

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

    public long? GetQuestionOpenSequence(int sourceQuestionId) =>
        _questionOpenSequence.TryGetValue(sourceQuestionId, out var sequence)
            ? sequence
            : null;

    public IReadOnlyList<QuestionOpenSequenceState> CaptureQuestionOpenSequence() =>
        _questionOpenSequence
            .OrderBy(item => item.Value)
            .Select(item => new QuestionOpenSequenceState(item.Key, item.Value))
            .ToArray();

    internal void RestoreQuestionOpenSequence(
        IReadOnlyList<QuestionOpenSequenceState>? states)
    {
        _questionOpenSequence.Clear();
        _nextQuestionOpenSequence = 0;

        var validQuestionIds = Session.Board.Questions
            .Select(question => question.SourceQuestionId)
            .ToHashSet();

        foreach (var state in states ?? [])
        {
            if (state.Sequence <= 0 ||
                !validQuestionIds.Contains(state.SourceQuestionId) ||
                _questionOpenSequence.ContainsKey(state.SourceQuestionId))
            {
                continue;
            }

            _questionOpenSequence[state.SourceQuestionId] = state.Sequence;
            _nextQuestionOpenSequence = Math.Max(
                _nextQuestionOpenSequence,
                state.Sequence);
        }

        if (_questionOpenSequence.Count == 0)
        {
            foreach (var question in Session.Board.Questions
                         .Select(question => new
                         {
                             Question = question,
                             OpenedAtUtc = InferLegacyQuestionOpenedAt(question)
                         })
                         .Where(item => item.OpenedAtUtc.HasValue)
                         .OrderBy(item => item.OpenedAtUtc)
                         .ThenBy(item => item.Question.SourceQuestionId))
            {
                _questionOpenSequence[question.Question.SourceQuestionId] =
                    ++_nextQuestionOpenSequence;
            }
        }

        TrackQuestionOpenings();
    }

    internal void MarkPersistenceChanged()
    {
        TrackQuestionOpenings();
        AutoAdvanceCompletedAllPlayerQuestion();
        Interlocked.Increment(ref _persistenceRevision);
    }

    private void TrackQuestionOpenings()
    {
        foreach (var question in Session.Board.Questions.Where(question =>
                     question.Status is
                         RuntimeQuestionStatus.Selected or
                         RuntimeQuestionStatus.AwaitingWager or
                         RuntimeQuestionStatus.Active or
                         RuntimeQuestionStatus.ShowingAnswer))
        {
            if (_questionOpenSequence.ContainsKey(question.SourceQuestionId))
            {
                continue;
            }

            _questionOpenSequence[question.SourceQuestionId] =
                ++_nextQuestionOpenSequence;
        }
    }

    private static DateTimeOffset? InferLegacyQuestionOpenedAt(
        RuntimeQuestion question)
    {
        DateTimeOffset? latest = null;

        foreach (var attempt in question.AnswerAttempts)
        {
            if (latest is null || attempt.JudgedAtUtc > latest.Value)
            {
                latest = attempt.JudgedAtUtc;
            }
        }

        if (question.Wager is { } singleWager &&
            (latest is null || singleWager.SubmittedAtUtc > latest.Value))
        {
            latest = singleWager.SubmittedAtUtc;
        }

        foreach (var wager in question.AllPlayerWagers)
        {
            if (latest is null || wager.SubmittedAtUtc > latest.Value)
            {
                latest = wager.SubmittedAtUtc;
            }
        }

        return latest;
    }

    private void AutoAdvanceCompletedAllPlayerQuestion()
    {
        if (Session.Status != GameSessionStatus.Running)
        {
            return;
        }

        var question = Session.Board.Questions.FirstOrDefault(item =>
            item.PresentationType is
                QuestionPresentationType.AllPlayerMultipleChoice or
                QuestionPresentationType.AllPlayerText &&
            item.Status is RuntimeQuestionStatus.Selected or
                RuntimeQuestionStatus.Active);

        if (question is null)
        {
            return;
        }

        var participants = question.IsSpecial
            ? Session.Players
                .Where(player => question.AllPlayerWagers.Any(wager =>
                    wager.PlayerId == player.Id))
                .ToArray()
            : Session.Players.ToArray();

        if (participants.Length == 0 ||
            participants.Any(player => question.AnswerAttempts.All(attempt =>
                attempt.PlayerId != player.Id)))
        {
            return;
        }

        if (question.PresentationType == QuestionPresentationType.AllPlayerText)
        {
            Session.Timer.Stop();
            Session.AnswerTimer.Stop();
            GetOrCreateAllPlayerTextReview(question).Accepting = false;
            return;
        }

        Session.ResolveQuestionWithoutCorrectAnswer(question.SourceQuestionId);
    }

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
            _questionOpenSequence.Clear();
            _nextQuestionOpenSequence = 0;
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

public sealed record QuestionOpenSequenceState(
    int SourceQuestionId,
    long Sequence);

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