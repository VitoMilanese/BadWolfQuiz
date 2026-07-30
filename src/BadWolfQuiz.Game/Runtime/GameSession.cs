using System.Collections.ObjectModel;
using BadWolfQuiz.Game.Definitions;

namespace BadWolfQuiz.Game.Runtime;

public sealed class GameSession
{
    public const int MinimumQuestionWager = 5;
    private const int MaxPlayerNameLength = 60;
    private readonly List<GamePlayer> _players = [];
    private readonly ReadOnlyCollection<GamePlayer> _readOnlyPlayers;
    private readonly TimeProvider _timeProvider;

    private GameSession(QuizSnapshot quiz, TimeProvider timeProvider)
    {
        Id = GameSessionId.New();
        Quiz = quiz;
        Board = new GameBoard(quiz);
        Timer = new GameTimer(TimeSpan.FromSeconds(30), timeProvider);
        CreatedAtUtc = timeProvider.GetUtcNow();
        _timeProvider = timeProvider;
        _readOnlyPlayers = _players.AsReadOnly();
    }

    public GameSessionId Id { get; }

    public QuizSnapshot Quiz { get; }

    public GameSessionStatus Status { get; private set; } = GameSessionStatus.Lobby;

    public IReadOnlyList<GamePlayer> Players => _readOnlyPlayers;

    public GamePlayerId? ActivePlayerId { get; private set; }

    public GamePlayer? ActivePlayer => ActivePlayerId is { } activePlayerId
        ? _players.Single(player => player.Id == activePlayerId)
        : null;

    public bool IsActivePlayerChangeLocked => Board.Questions.Any(question =>
        question.IsSpecial &&
        question.Status is RuntimeQuestionStatus.AwaitingWager or
            RuntimeQuestionStatus.Active);

    public GameBoard Board { get; }

    public GameTimer Timer { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset? StartedAtUtc { get; private set; }

    public static GameSession Create(QuizSnapshot quiz, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(quiz);
        return new GameSession(quiz, timeProvider ?? TimeProvider.System);
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
        ActivePlayerId ??= player.Id;
        return player;
    }

    public void Start()
    {
        EnsureLobby();

        if (_players.Count == 0)
        {
            throw new GameRuleViolationException("A game cannot start without players.");
        }

        Status = GameSessionStatus.Running;
        StartedAtUtc = _timeProvider.GetUtcNow();
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

        var activePlayerId = ActivePlayerId
            ?? throw new GameRuleViolationException(
                "A question cannot be selected without an active player.");

        question.Select(activePlayerId);
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
        return question;
    }

    public RuntimeQuestion ActivateQuestionBuzzer(int sourceQuestionId)
    {
        EnsureRunning();

        var question = FindQuestion(sourceQuestionId);
        question.ActivateBuzzer();
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

        if (isCorrect)
        {
            ActivePlayerId = player.Id;
        }

        return attempt;
    }

    public RuntimeQuestion ResolveQuestionWithoutCorrectAnswer(
        int sourceQuestionId)
    {
        EnsureRunning();

        var question = FindQuestion(sourceQuestionId);
        question.ResolveWithoutCorrectAnswer();
        return question;
    }

    public RuntimeQuestion CloseQuestionAnswer(int sourceQuestionId)
    {
        EnsureRunning();

        var question = FindQuestion(sourceQuestionId);
        question.CloseAnswer();
        return question;
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
    Completed = 3
}
