using System.Collections.ObjectModel;
using BadWolfQuiz.Game.Definitions;

namespace BadWolfQuiz.Game.Runtime;

public sealed class GameSession
{
    public const int MinimumQuestionWager = 5;
    public static readonly TimeSpan DefaultBuzzerDuration = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan DefaultAnswerDuration = TimeSpan.FromSeconds(10);
    private const int MaxPlayerNameLength = 60;
    private readonly List<GamePlayer> _players = [];
    private readonly List<GamePlayer> _removedPlayers = [];
    private readonly Dictionary<GamePlayerId, int> _currentRoundStartScores = [];
    private readonly ReadOnlyCollection<GamePlayer> _readOnlyPlayers;
    private readonly ReadOnlyCollection<GamePlayer> _readOnlyRemovedPlayers;
    private readonly TimeProvider _timeProvider;

    private GameSession(
        QuizSnapshot quiz,
        GameSessionSettings settings,
        TimeProvider timeProvider)
    {
        Id = GameSessionId.New();
        Quiz = quiz;
        Settings = settings;
        Board = new GameBoard(quiz);
        Timer = new GameTimer(settings.BuzzerDuration, timeProvider);
        AnswerTimer = new GameTimer(settings.AnswerDuration, timeProvider);
        CreatedAtUtc = timeProvider.GetUtcNow();
        _timeProvider = timeProvider;
        _readOnlyPlayers = _players.AsReadOnly();
        _readOnlyRemovedPlayers = _removedPlayers.AsReadOnly();
    }

    public GameSessionId Id { get; private set; }

    public QuizSnapshot Quiz { get; }

    public GameSessionSettings Settings { get; private set; }

    public GameSessionStatus Status { get; private set; } = GameSessionStatus.Lobby;

    public IReadOnlyList<GamePlayer> Players => _readOnlyPlayers;

    public IReadOnlyList<GamePlayer> RemovedPlayers => _readOnlyRemovedPlayers;

    public IReadOnlyList<GamePlayer> AllPlayers => [.. _players, .. _removedPlayers];

    public GamePlayerId? ActivePlayerId { get; private set; }

    public GamePlayer? ActivePlayer => ActivePlayerId is { } activePlayerId
        ? _players.Single(player => player.Id == activePlayerId)
        : null;

    public int CurrentRoundIndex { get; private set; }

    public int CurrentRoundNumber => CurrentRoundIndex + 1;

    public QuizRoundSnapshot CurrentRound => Quiz.Rounds
        .OrderBy(round => round.SortOrder)
        .ElementAt(CurrentRoundIndex);

    public bool IsCurrentRoundComplete => Board.Questions
        .Where(question => question.SourceRoundId == CurrentRound.SourceRoundId)
        .All(question => question.Status == RuntimeQuestionStatus.Resolved);

    public bool HasNextRound => CurrentRoundIndex < Quiz.Rounds.Count - 1;

    public bool IsActivePlayerChangeLocked => Board.Questions.Any(question =>
        question.IsSpecial &&
        question.Status is RuntimeQuestionStatus.AwaitingWager or
            RuntimeQuestionStatus.Active);

    public GameBoard Board { get; }

    public FinalQuestion? FinalQuestion { get; private set; }

    public GameTimer Timer { get; private set; }

    public GameTimer AnswerTimer { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? StartedAtUtc { get; private set; }

    public static GameSession Create(
        QuizSnapshot quiz,
        TimeProvider? timeProvider = null)
    {
        return Create(quiz, GameSessionSettings.Default, timeProvider);
    }

    public GameSessionState CaptureState() => new(
        Id,
        Status,
        ActivePlayerId,
        CurrentRoundIndex,
        CreatedAtUtc,
        StartedAtUtc,
        _currentRoundStartScores
            .Select(score => new PlayerRoundStartScoreState(score.Key, score.Value))
            .ToArray(),
        _players.Select(player => player.CaptureState()).ToArray(),
        _removedPlayers.Select(player => player.CaptureState()).ToArray(),
        Board.Questions.Select(question => question.CaptureState()).ToArray(),
        FinalQuestion?.CaptureState());

    public static GameSession Restore(
        QuizSnapshot quiz,
        GameSessionSettings settings,
        GameSessionState state,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        var session = new GameSession(
            quiz,
            settings,
            timeProvider ?? TimeProvider.System)
        {
            Id = state.Id,
            Status = state.Status,
            ActivePlayerId = state.ActivePlayerId,
            CurrentRoundIndex = state.CurrentRoundIndex,
            CreatedAtUtc = state.CreatedAtUtc,
            StartedAtUtc = state.StartedAtUtc
        };

        session._players.AddRange(state.Players.Select(GamePlayer.Restore));
        session._removedPlayers.AddRange(
            state.RemovedPlayers.Select(GamePlayer.Restore));
        session._currentRoundStartScores.Clear();
        foreach (var score in state.CurrentRoundStartScores)
        {
            session._currentRoundStartScores[score.PlayerId] = score.Score;
        }
        session.Board.RestoreState(state.Questions);
        foreach (var question in session.Board.Questions)
        {
            question.SuspendOpenBuzzerForRecovery();
        }
        session.FinalQuestion = state.FinalQuestion is null
            ? null
            : FinalQuestion.Restore(
                quiz.FinalQuestion ?? throw new InvalidOperationException(
                    "The saved final question no longer exists."),
                state.FinalQuestion);

        // Timer progress is intentionally not restored.
        session.Timer.Stop();
        session.AnswerTimer.Stop();
        return session;
    }

    public static GameSession Create(
        QuizSnapshot quiz,
        GameSessionSettings settings,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(quiz);
        ArgumentNullException.ThrowIfNull(settings);
        return new GameSession(
            quiz,
            settings,
            timeProvider ?? TimeProvider.System);
    }

    public void UpdateSettings(GameSessionSettings settings)
    {
        if (Status is not GameSessionStatus.Lobby and not GameSessionStatus.Running)
        {
            throw new GameRuleViolationException(
                "Game settings can only be changed in the lobby or during regular play.");
        }
        ArgumentNullException.ThrowIfNull(settings);

        Settings = settings;
        Timer = new GameTimer(settings.BuzzerDuration, _timeProvider);
        AnswerTimer = new GameTimer(settings.AnswerDuration, _timeProvider);
    }

    public GamePlayer AddPlayer(string name)
    {
        EnsureAcceptingPlayers();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var normalizedName = name.Trim();

        if (normalizedName.Length > MaxPlayerNameLength)
        {
            throw new ArgumentException(
                $"Player name cannot exceed {MaxPlayerNameLength} characters.",
                nameof(name));
        }

        if (_players.Any(player =>
                string.Equals(player.Name, normalizedName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new GameRuleViolationException(
                $"A player named '{normalizedName}' has already joined this game.");
        }

        var player = new GamePlayer(GamePlayerId.New(), normalizedName, _timeProvider.GetUtcNow());
        _players.Add(player);

        if (Status == GameSessionStatus.Running)
        {
            _currentRoundStartScores[player.Id] = player.Score;
        }

        ActivePlayerId ??= player.Id;
        return player;
    }

    public GamePlayer RemovePlayer(GamePlayerId playerId)
    {
        if (Status is not GameSessionStatus.Lobby and not GameSessionStatus.Running)
        {
            throw new GameRuleViolationException(
                "Players cannot be removed during or after the final question.");
        }

        var player = FindPlayer(playerId);
        var currentQuestion = Board.Questions.FirstOrDefault(question =>
            question.Status is RuntimeQuestionStatus.Selected or
                RuntimeQuestionStatus.AwaitingWager or
                RuntimeQuestionStatus.Active);

        if (currentQuestion?.SelectedByPlayerId == playerId ||
            currentQuestion?.AnsweringPlayerId == playerId ||
            currentQuestion?.Wager?.PlayerId == playerId)
        {
            throw new GameRuleViolationException(
                "A player involved in the current question cannot be removed.");
        }

        _players.Remove(player);
        _removedPlayers.Add(player);
        _currentRoundStartScores.Remove(player.Id);

        if (ActivePlayerId == player.Id)
        {
            ActivePlayerId = _players.FirstOrDefault()?.Id;
        }

        return player;
    }

    public GamePlayer RestoreRemovedPlayer(GamePlayerId playerId)
    {
        if (Status is not GameSessionStatus.Lobby and not GameSessionStatus.Running)
        {
            throw new GameRuleViolationException(
                "Players cannot rejoin during or after the final question.");
        }

        var player = _removedPlayers.SingleOrDefault(player => player.Id == playerId)
            ?? throw new GameRuleViolationException("The removed player was not found.");

        _removedPlayers.Remove(player);
        _players.Add(player);

        if (Status == GameSessionStatus.Running)
        {
            _currentRoundStartScores[player.Id] = player.Score;
        }

        ActivePlayerId ??= player.Id;
        return player;
    }

    public void Start()
    {
        EnsureLobby();

        Status = GameSessionStatus.Running;
        StartedAtUtc = _timeProvider.GetUtcNow();
        CaptureRoundStartScores();
    }

    public RuntimeQuestion SelectQuestion(int sourceQuestionId)
    {
        if (Status != GameSessionStatus.Running)
        {
            throw new GameRuleViolationException(
                "Questions can only be selected while the game is running.");
        }

        if (Board.Questions.Any(question =>
                question.Status is not RuntimeQuestionStatus.Available and
                    not RuntimeQuestionStatus.Resolved))
        {
            throw new GameRuleViolationException(
                "Resolve the current question before selecting another one.");
        }

        var question = Board.Questions.SingleOrDefault(
            item => item.SourceQuestionId == sourceQuestionId)
            ?? throw new GameRuleViolationException(
                $"Question {sourceQuestionId} does not belong to this game.");

        if (question.SourceRoundId != CurrentRound.SourceRoundId)
        {
            throw new GameRuleViolationException(
                "Only a question from the current round can be selected.");
        }

        var activePlayerId = ActivePlayerId
            ?? throw new GameRuleViolationException(
                "A question cannot be selected without an active player.");

        question.Select(activePlayerId);

        if (!question.IsSpecial &&
            Settings.RegularQuestionBuzzerStartMode == GamePhaseStartMode.Automatic)
        {
            ActivateQuestionBuzzer(sourceQuestionId);
        }

        return question;
    }

    public WagerLimits GetQuestionWagerLimits(int sourceQuestionId)
    {
        var question = FindQuestion(sourceQuestionId);
        var playerId = question.SelectedByPlayerId ?? ActivePlayerId
            ?? throw new GameRuleViolationException(
                "A wager cannot be calculated without an active player.");
        var player = _players.Single(item => item.Id == playerId);
        var highestQuestionValue = Board.Questions
            .Where(item => item.SourceRoundId == question.SourceRoundId)
            .Max(item => item.Points);

        return new WagerLimits(
            MinimumQuestionWager,
            Math.Max(player.Score, highestQuestionValue));
    }

    public RuntimeQuestion SubmitQuestionWager(
        int sourceQuestionId,
        int amount)
    {
        if (Status != GameSessionStatus.Running)
        {
            throw new GameRuleViolationException(
                "Question wagers can only be submitted while the game is running.");
        }

        var question = FindQuestion(sourceQuestionId);
        var limits = GetQuestionWagerLimits(sourceQuestionId);

        if (!limits.Contains(amount))
        {
            throw new GameRuleViolationException(
                $"A question wager must be between {limits.Minimum} and {limits.Maximum}.");
        }

        var playerId = question.SelectedByPlayerId
            ?? throw new GameRuleViolationException(
                "The wager question does not have a selecting player.");

        question.SubmitWager(playerId, amount, _timeProvider.GetUtcNow());
        Timer.Stop();

        if (Settings.WagerQuestionAnswerTimerStartMode ==
            GamePhaseStartMode.Automatic)
        {
            AnswerTimer.Restart();
        }
        else
        {
            AnswerTimer.Stop();
        }

        return question;
    }

    public GameTimer StartWagerAnswerTimer(int sourceQuestionId)
    {
        EnsureRunning();

        var question = FindQuestion(sourceQuestionId);

        if (!question.IsSpecial ||
            question.Status != RuntimeQuestionStatus.Active ||
            question.Wager is null)
        {
            throw new GameRuleViolationException(
                "A wager answer timer can only start for an active wager question.");
        }

        AnswerTimer.Start();
        return AnswerTimer;
    }

    public RuntimeQuestion ActivateQuestionBuzzer(int sourceQuestionId)
    {
        EnsureRunning();

        var question = FindQuestion(sourceQuestionId);
        question.ActivateBuzzer();

        if (Timer.IsPaused)
        {
            Timer.Resume();
        }
        else
        {
            Timer.Restart();
        }

        AnswerTimer.Stop();
        return question;
    }

    public RuntimeQuestion RevealNextClue(int sourceQuestionId)
    {
        EnsureRunning();

        var question = FindQuestion(sourceQuestionId);
        question.RevealNextClue();

        Timer.Restart();

        return question;
    }

    public RuntimeQuestion ClaimQuestionBuzzer(
        int sourceQuestionId,
        GamePlayerId playerId)
    {
        EnsureRunning();

        var question = FindQuestion(sourceQuestionId);
        var player = FindPlayer(playerId);
        question.ClaimBuzzer(player.Id);

        if (Timer.Status == GameTimerStatus.Running)
        {
            Timer.Pause();
        }

        AnswerTimer.Restart();
        return question;
    }

    public QuestionAnswerAttempt JudgeQuestionAnswer(
        int sourceQuestionId,
        GamePlayerId playerId,
        bool isCorrect)
    {
        EnsureRunning();

        var question = FindQuestion(sourceQuestionId);
        var player = FindPlayer(playerId);
        var attempt = question.JudgeAnswer(
            player.Id,
            isCorrect,
            _timeProvider.GetUtcNow());

        player.ApplyScore(attempt.ScoreDelta);
        AnswerTimer.Stop();

        if (isCorrect)
        {
            ActivePlayerId = player.Id;
            Timer.Stop();
        }
        else if (question.IsSpecial)
        {
            Timer.Stop();
        }
        else if (Timer.IsPaused)
        {
            Timer.Resume();
        }

        return attempt;
    }

    public QuestionAnswerAttempt AddQuestionAnswerHistoryEntry(
        int sourceQuestionId,
        GamePlayerId playerId,
        bool isCorrect,
        int value)
    {
        EnsureHistoryEditingAllowed();

        var question = FindQuestion(sourceQuestionId);
        EnsureQuestionCanHaveHistoryAdded(question);
        var player = FindPlayer(playerId);
        var attempt = question.AddHistoricalAttempt(
            player.Id,
            isCorrect,
            value,
            _timeProvider.GetUtcNow());
        question.ResolveFromHistory();

        player.ApplyScore(attempt.ScoreDelta);
        ApplyHistoricalScoreCorrection(question, player.Id, attempt.ScoreDelta);
        return attempt;
    }

    public QuestionAnswerAttempt UpdateQuestionAnswerHistoryEntry(
        int sourceQuestionId,
        Guid attemptId,
        GamePlayerId playerId,
        bool isCorrect,
        int value)
    {
        EnsureHistoryEditingAllowed();

        var question = FindQuestion(sourceQuestionId);
        EnsureQuestionHasBeenPlayed(question);
        var newPlayer = FindPlayer(playerId);
        var (previous, updated) = question.UpdateHistoricalAttempt(
            attemptId,
            newPlayer.Id,
            isCorrect,
            value);
        var previousPlayer = FindAnyPlayer(previous.PlayerId);

        previousPlayer.ApplyScore(-previous.ScoreDelta);
        newPlayer.ApplyScore(updated.ScoreDelta);
        ApplyHistoricalScoreCorrection(
            question,
            previousPlayer.Id,
            -previous.ScoreDelta);
        ApplyHistoricalScoreCorrection(
            question,
            newPlayer.Id,
            updated.ScoreDelta);
        return updated;
    }

    public QuestionAnswerAttempt RemoveQuestionAnswerHistoryEntry(
        int sourceQuestionId,
        Guid attemptId)
    {
        EnsureHistoryEditingAllowed();

        var question = FindQuestion(sourceQuestionId);
        EnsureQuestionHasBeenPlayed(question);
        var removed = question.RemoveHistoricalAttempt(attemptId);
        var player = FindAnyPlayer(removed.PlayerId);

        player.ApplyScore(-removed.ScoreDelta);
        ApplyHistoricalScoreCorrection(
            question,
            player.Id,
            -removed.ScoreDelta);
        return removed;
    }

    public RuntimeQuestion ResolveQuestionWithoutCorrectAnswer(
        int sourceQuestionId)
    {
        EnsureRunning();

        var question = FindQuestion(sourceQuestionId);
        question.ResolveWithoutCorrectAnswer();
        Timer.Stop();
        AnswerTimer.Stop();
        return question;
    }

    public RuntimeQuestion CloseQuestionAnswer(int sourceQuestionId)
    {
        EnsureRunning();

        var question = FindQuestion(sourceQuestionId);
        question.CloseAnswer();
        Timer.Stop();
        AnswerTimer.Stop();
        return question;
    }

    public GameTimer PauseQuestionTimer()
    {
        EnsureRunning();

        var timer = AnswerTimer.Status == GameTimerStatus.Running
            ? AnswerTimer
            : Timer.Status == GameTimerStatus.Running
                ? Timer
                : throw new GameRuleViolationException(
                    "There is no running question timer to pause.");

        timer.Pause();
        return timer;
    }

    public GameTimer ResumeQuestionTimer()
    {
        EnsureRunning();

        var timer = AnswerTimer.IsPaused
            ? AnswerTimer
            : Timer.IsPaused
                ? Timer
                : throw new GameRuleViolationException(
                    "There is no paused question timer to resume.");

        timer.Resume();
        return timer;
    }

    public QuestionTimerOutcome ProcessQuestionTimers()
    {
        EnsureRunning();

        var question = Board.Questions.FirstOrDefault(item =>
            item.Status is RuntimeQuestionStatus.Selected or
                RuntimeQuestionStatus.Active);

        if (question is null)
        {
            return QuestionTimerOutcome.None;
        }

        _ = AnswerTimer.Remaining;

        if (AnswerTimer.Status == GameTimerStatus.Expired &&
            question.AnsweringPlayerId is { } answeringPlayerId)
        {
            JudgeQuestionAnswer(
                question.SourceQuestionId,
                answeringPlayerId,
                false);

            return QuestionTimerOutcome.AnswerExpired;
        }

        _ = Timer.Remaining;

        if (Timer.Status == GameTimerStatus.Expired &&
            question.AnsweringPlayerId is null)
        {
            if (question.CanRevealClue)
            {
                RevealNextClue(question.SourceQuestionId);

                return QuestionTimerOutcome.ClueRevealed;
            }

            ResolveQuestionWithoutCorrectAnswer(question.SourceQuestionId);
            return QuestionTimerOutcome.BuzzerExpired;
        }

        return QuestionTimerOutcome.None;
    }

    public QuizRoundSnapshot AdvanceToNextRound()
    {
        EnsureRunning();

        if (!IsCurrentRoundComplete)
        {
            throw new GameRuleViolationException(
                "The current round must be completed before advancing.");
        }

        if (!HasNextRound)
        {
            throw new GameRuleViolationException(
                "The game does not have another round.");
        }

        GamePlayerId? nextActivePlayerId = _players.Count > 0
            ? GetWeakestCurrentRoundPlayerId()
            : null;

        CurrentRoundIndex++;
        CaptureRoundStartScores();
        ActivePlayerId = nextActivePlayerId;

        return CurrentRound;
    }

    public void ForceCompleteCurrentRound()
    {
        EnsureRunning();

        if (!HasNextRound)
        {
            throw new GameRuleViolationException(
                "The game does not have another round.");
        }

        foreach (var question in Board.Questions.Where(question =>
                     question.SourceRoundId == CurrentRound.SourceRoundId &&
                     question.Status != RuntimeQuestionStatus.Resolved))
        {
            question.ForceResolve();
        }

        Timer.Stop();
        AnswerTimer.Stop();
    }

    public IReadOnlyList<GameResultStanding> GetCurrentRoundStandings()
    {
        EnsureRunning();

        if (!IsCurrentRoundComplete)
        {
            throw new GameRuleViolationException(
                "Round standings are only available after the current round is complete.");
        }

        return CreateStandings(CreateCurrentResultMetrics());
    }

    public FinalQuestion StartFinalQuestion()
    {
        EnsureRunning();

        if (HasNextRound || !IsCurrentRoundComplete)
        {
            throw new GameRuleViolationException(
                "The final question can only start after the last round is complete.");
        }

        var definition = Quiz.FinalQuestion
            ?? throw new GameRuleViolationException(
                "This quiz does not contain a final question.");

        var eligiblePlayers = Settings.AllowNegativeScoreFinalPlayers
            ? _players.ToArray()
            : _players.Where(player => player.Score >= 0).ToArray();

        if (eligiblePlayers.Length == 0)
        {
            throw new GameRuleViolationException(
                "No players are eligible for the final question.");
        }

        FinalQuestion = new FinalQuestion(definition, eligiblePlayers);
        Status = GameSessionStatus.FinalWagering;
        return FinalQuestion;
    }

    public FinalPlayerSubmission SubmitFinalWager(
        GamePlayerId playerId,
        int amount)
    {
        EnsureFinalStatus(GameSessionStatus.FinalWagering);
        return FinalQuestion!.SubmitWager(
            FindPlayer(playerId).Id,
            amount,
            _timeProvider.GetUtcNow());
    }

    public void LockFinalWagers()
    {
        EnsureFinalStatus(GameSessionStatus.FinalWagering);
        FinalQuestion!.LockWagers();
        Status = GameSessionStatus.FinalAnswering;
    }

    public FinalPlayerSubmission SubmitFinalAnswer(
        GamePlayerId playerId,
        string answer)
    {
        EnsureFinalStatus(GameSessionStatus.FinalAnswering);
        return FinalQuestion!.SubmitAnswer(
            FindPlayer(playerId).Id,
            answer,
            _timeProvider.GetUtcNow());
    }

    public void LockFinalAnswers()
    {
        EnsureFinalStatus(GameSessionStatus.FinalAnswering);
        FinalQuestion!.LockAnswers();
        Status = GameSessionStatus.FinalJudging;
    }

    public FinalPlayerSubmission JudgeFinalAnswer(
        GamePlayerId playerId,
        bool isCorrect)
    {
        EnsureFinalStatus(GameSessionStatus.FinalJudging);
        var player = FindPlayer(playerId);
        var submission = FinalQuestion!.Judge(
            player.Id,
            isCorrect,
            _timeProvider.GetUtcNow());
        var wager = submission.Wager!.Amount;
        player.ApplyScore(isCorrect ? wager : -wager);

        if (FinalQuestion.Status == FinalQuestionStatus.Completed)
        {
            Status = GameSessionStatus.Completed;
        }

        return submission;
    }

    public IReadOnlyList<GameResultStanding> GetFinalStandings()
    {
        if (Status is not GameSessionStatus.Running and
            not GameSessionStatus.Completed)
        {
            throw new GameRuleViolationException(
                "Final standings are not available in the current game phase.");
        }

        if (Quiz.FinalQuestion is not null &&
            Status != GameSessionStatus.Completed)
        {
            throw new GameRuleViolationException(
                "Final standings are available after final judging is complete.");
        }

        if (HasNextRound || !IsCurrentRoundComplete)
        {
            throw new GameRuleViolationException(
                "Final standings are only available after the last round is complete.");
        }

        return CreateStandings(CreateCurrentResultMetrics());
    }

    public GamePlayer AdjustPlayerScore(
        GamePlayerId playerId,
        int points)
    {
        var player = FindPlayer(playerId);

        player.ApplyScore(points);
        return player;
    }

    public GamePlayer SetActivePlayer(GamePlayerId playerId)
    {
        EnsureActivePlayerChangeAllowed();

        var player = FindPlayer(playerId);

        ActivePlayerId = player.Id;
        return player;
    }

    public GamePlayer SelectRandomActivePlayer()
    {
        EnsureActivePlayerChangeAllowed();

        if (_players.Count == 0)
        {
            throw new GameRuleViolationException(
                "An active player cannot be selected before a player joins.");
        }

        return SetActivePlayer(_players[Random.Shared.Next(_players.Count)].Id);
    }

    private void CaptureRoundStartScores()
    {
        _currentRoundStartScores.Clear();

        foreach (var player in _players)
        {
            _currentRoundStartScores[player.Id] = player.Score;
        }
    }

    private void ApplyHistoricalScoreCorrection(
        RuntimeQuestion question,
        GamePlayerId playerId,
        int scoreDelta)
    {
        if (question.SourceRoundId == CurrentRound.SourceRoundId)
        {
            return;
        }

        _currentRoundStartScores[playerId] =
            checked(_currentRoundStartScores.GetValueOrDefault(playerId) + scoreDelta);
    }

    private void EnsureHistoryEditingAllowed()
    {
        if (Status == GameSessionStatus.Lobby)
        {
            throw new GameRuleViolationException(
                "Answer history cannot be edited before the game starts.");
        }
    }

    private void EnsureQuestionCanHaveHistoryAdded(RuntimeQuestion question)
    {
        var availableRoundIds = Quiz.Rounds
            .OrderBy(round => round.SortOrder)
            .Take(CurrentRoundIndex + 1)
            .Select(round => round.SourceRoundId);

        if (question.Status == RuntimeQuestionStatus.Available &&
            !availableRoundIds.Contains(question.SourceRoundId))
        {
            throw new GameRuleViolationException(
                "Answer history cannot be added to a question that has not been played.");
        }
    }

    private static void EnsureQuestionHasBeenPlayed(RuntimeQuestion question)
    {
        if (question.Status == RuntimeQuestionStatus.Available)
        {
            throw new GameRuleViolationException(
                "Answer history cannot be edited for a question that has not been played.");
        }
    }

    private List<ResultMetrics> CreateCurrentResultMetrics()
    {
        var roundIdsDescending = Quiz.Rounds
            .OrderBy(round => round.SortOrder)
            .Take(CurrentRoundIndex + 1)
            .Reverse()
            .Select(round => round.SourceRoundId)
            .ToArray();

        return _players
            .Select(player => CreateResultMetrics(player, roundIdsDescending))
            .ToList();
    }

    private GamePlayerId GetWeakestCurrentRoundPlayerId()
    {
        var metrics = CreateCurrentResultMetrics();
        metrics.Sort((left, right) => CompareResultMetrics(right, left));

        var weakest = metrics[0];

        return metrics
            .Where(item => CompareResultMetrics(item, weakest) == 0)
            .OrderBy(item => item.Player.JoinedAtUtc)
            .First()
            .Player
            .Id;
    }

    private static IReadOnlyList<GameResultStanding> CreateStandings(
        List<ResultMetrics> metrics)
    {
        metrics.Sort(CompareResultMetrics);

        var standings = new List<GameResultStanding>(metrics.Count);

        for (var index = 0; index < metrics.Count; index++)
        {
            var position = index == 0 ||
                CompareResultMetrics(metrics[index - 1], metrics[index]) != 0
                    ? index + 1
                    : standings[index - 1].Position;
            var item = metrics[index];

            standings.Add(new GameResultStanding(
                position,
                item.Player.Id,
                item.Player.Name,
                item.Player.Score,
                item.ScoreGain,
                item.TotalCorrectAnswers,
                item.TotalAttempts,
                position == 1));
        }

        return standings;
    }

    private ResultMetrics CreateResultMetrics(
        GamePlayer player,
        IReadOnlyList<int> roundIdsDescending)
    {
        var attemptsByRound = roundIdsDescending
            .Select(roundId => Board.Questions
                .Where(question => question.SourceRoundId == roundId)
                .SelectMany(question => question.AnswerAttempts)
                .Where(attempt => attempt.PlayerId == player.Id)
                .ToArray())
            .ToArray();
        var correctByRound = attemptsByRound
            .Select(attempts => attempts.Count(attempt => attempt.IsCorrect))
            .ToArray();
        var totalAttemptsByRound = attemptsByRound
            .Select(attempts => attempts.Length)
            .ToArray();

        return new ResultMetrics(
            player,
            player.Score - _currentRoundStartScores.GetValueOrDefault(player.Id),
            correctByRound.Sum(),
            correctByRound,
            totalAttemptsByRound.Sum(),
            totalAttemptsByRound);
    }

    private static int CompareResultMetrics(ResultMetrics left, ResultMetrics right)
    {
        var comparison = right.Player.Score.CompareTo(left.Player.Score);

        if (comparison != 0)
        {
            return comparison;
        }

        comparison = right.ScoreGain.CompareTo(left.ScoreGain);

        if (comparison != 0)
        {
            return comparison;
        }

        comparison = right.TotalCorrectAnswers.CompareTo(left.TotalCorrectAnswers);

        if (comparison != 0)
        {
            return comparison;
        }

        comparison = CompareRoundCounts(
            left.CorrectAnswersByRound,
            right.CorrectAnswersByRound);

        if (comparison != 0)
        {
            return comparison;
        }

        comparison = right.TotalAttempts.CompareTo(left.TotalAttempts);

        return comparison != 0
            ? comparison
            : CompareRoundCounts(left.AttemptsByRound, right.AttemptsByRound);
    }

    private static int CompareRoundCounts(
        IReadOnlyList<int> left,
        IReadOnlyList<int> right)
    {
        for (var index = 0; index < left.Count; index++)
        {
            var comparison = right[index].CompareTo(left[index]);

            if (comparison != 0)
            {
                return comparison;
            }
        }

        return 0;
    }

    private sealed record ResultMetrics(
        GamePlayer Player,
        int ScoreGain,
        int TotalCorrectAnswers,
        IReadOnlyList<int> CorrectAnswersByRound,
        int TotalAttempts,
        IReadOnlyList<int> AttemptsByRound);

    private void EnsureActivePlayerChangeAllowed()
    {
        if (IsActivePlayerChangeLocked)
        {
            throw new GameRuleViolationException(
                "The active player cannot change while a wager question is in progress.");
        }
    }

    private GamePlayer FindPlayer(GamePlayerId playerId)
    {
        return _players.SingleOrDefault(player => player.Id == playerId)
            ?? throw new GameRuleViolationException(
                "The selected player does not belong to this game.");
    }

    private GamePlayer FindAnyPlayer(GamePlayerId playerId)
    {
        return _players
            .Concat(_removedPlayers)
            .SingleOrDefault(player => player.Id == playerId)
            ?? throw new GameRuleViolationException(
                "The selected player does not belong to this game.");
    }

    private void EnsureFinalStatus(GameSessionStatus expected)
    {
        if (Status != expected || FinalQuestion is null)
        {
            throw new GameRuleViolationException(
                $"The game must be in the {expected} phase.");
        }
    }

    private void EnsureRunning()
    {
        if (Status != GameSessionStatus.Running)
        {
            throw new GameRuleViolationException(
                "Questions can only be judged while the game is running.");
        }
    }

    private RuntimeQuestion FindQuestion(int sourceQuestionId)
    {
        return Board.Questions.SingleOrDefault(
            item => item.SourceQuestionId == sourceQuestionId)
            ?? throw new GameRuleViolationException(
                $"Question {sourceQuestionId} does not belong to this game.");
    }

    private void EnsureAcceptingPlayers()
    {
        if (Status is not GameSessionStatus.Lobby and not GameSessionStatus.Running)
        {
            throw new GameRuleViolationException(
                "Players cannot join a completed game.");
        }
    }

    private void EnsureLobby()
    {
        if (Status != GameSessionStatus.Lobby)
        {
            throw new GameRuleViolationException("This operation is only available in the lobby.");
        }
    }
}

public enum GameSessionStatus
{
    Lobby = 1,
    Running = 2,
    FinalWagering = 3,
    FinalAnswering = 4,
    FinalJudging = 5,
    Completed = 6
}


public enum QuestionTimerOutcome
{
    None = 0,
    BuzzerExpired = 1,
    AnswerExpired = 2,
    ClueRevealed = 3
}


public sealed record GameResultStanding(
    int Position,
    GamePlayerId PlayerId,
    string PlayerName,
    int Score,
    int ScoreGain,
    int TotalCorrectAnswers,
    int TotalAttempts,
    bool IsWinner);
