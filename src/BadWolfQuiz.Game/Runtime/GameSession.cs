using System.Collections.ObjectModel;
using BadWolfQuiz.Game.Definitions;

namespace BadWolfQuiz.Game.Runtime;

public sealed class GameSession
{
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
        EnsureLobby();
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

        question.Select();
        return question;
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
