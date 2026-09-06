using System.Security.Cryptography;

namespace BadWolfQuiz.Web.Services;

internal sealed class MinigameQuestionGameState
{
    private readonly string[] _sourceQuestions;
    private readonly MinigameQuestionSelectionMode _selectionMode;
    private readonly PlayerQuestionDeck _player1;
    private readonly PlayerQuestionDeck _player2;
    private readonly List<MinigameQuestionHistoryEntry> _history = [];

    private MinigameQuestionGameState(
        string[] sourceQuestions,
        MinigameQuestionSelectionMode selectionMode,
        PlayerQuestionDeck player1,
        PlayerQuestionDeck player2)
    {
        _sourceQuestions = sourceQuestions;
        _selectionMode = selectionMode;
        _player1 = player1;
        _player2 = player2;
    }

    public bool HasSelectedQuestionThisTurn { get; private set; }

    public string? PendingQuestion { get; private set; }

    public int? PendingResponsePlayerNumber { get; private set; }

    public MinigameQuestionSelectionMode SelectionMode => _selectionMode;

    public IReadOnlyList<MinigameQuestionHistoryEntry> History => _history;

    public static MinigameQuestionGameState Create(
        IReadOnlyList<string> questions,
        MinigameQuestionSelectionMode selectionMode = MinigameQuestionSelectionMode.Cards)
    {
        ArgumentNullException.ThrowIfNull(questions);
        if (!Enum.IsDefined(selectionMode))
        {
            throw new ArgumentOutOfRangeException(nameof(selectionMode));
        }

        var normalized = questions
            .Select(question => question.Trim())
            .Where(question => !string.IsNullOrWhiteSpace(question))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (normalized.Length < MinigameQuestionStore.MinimumQuestionCount)
        {
            throw new MinigameRoomException(MinigameRoomError.QuestionsUnavailable);
        }

        return new MinigameQuestionGameState(
            normalized,
            selectionMode,
            PlayerQuestionDeck.Create(normalized, selectionMode),
            PlayerQuestionDeck.Create(normalized, selectionMode));
    }

    public MinigameQuestionGameState Restart() => Create(_sourceQuestions, _selectionMode);

    public IReadOnlyList<string> GetAvailableQuestions(int playerNumber) =>
        GetDeck(playerNumber).AvailableQuestions;

    public IReadOnlyList<string> GetRemainingSearchQuestions(int playerNumber) =>
        GetDeck(playerNumber).RemainingSearchQuestions;

    public void BeginTurn() => HasSelectedQuestionThisTurn = false;

    public void SelectQuestion(int playerNumber, int optionIndex)
    {
        if (_selectionMode != MinigameQuestionSelectionMode.Cards)
        {
            throw new MinigameRoomException(MinigameRoomError.InvalidQuestion);
        }

        RecordQuestion(playerNumber, GetDeck(playerNumber).Select(optionIndex));
    }

    public void SelectQuestion(int playerNumber, string question)
    {
        if (_selectionMode != MinigameQuestionSelectionMode.Search)
        {
            throw new MinigameRoomException(MinigameRoomError.InvalidQuestion);
        }

        RecordQuestion(playerNumber, GetDeck(playerNumber).Select(question));
    }

    public void SubmitQuestionResponse(int playerNumber, bool answerYes)
    {
        if (PendingResponsePlayerNumber != playerNumber ||
            string.IsNullOrWhiteSpace(PendingQuestion))
        {
            throw new MinigameRoomException(MinigameRoomError.QuestionResponseNotPending);
        }

        _history.Add(new MinigameQuestionHistoryEntry(
            playerNumber,
            MinigameQuestionHistoryKind.Answer,
            AnswerYes: answerYes));
        PendingQuestion = null;
        PendingResponsePlayerNumber = null;
    }

    public void RecordGuess(int playerNumber, string gameName, bool isCorrect)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameName);
        _history.Add(new MinigameQuestionHistoryEntry(
            playerNumber,
            MinigameQuestionHistoryKind.Guess,
            gameName,
            isCorrect));
    }

    public void RecordTurnEnded(int playerNumber) =>
        _history.Add(new MinigameQuestionHistoryEntry(
            playerNumber,
            MinigameQuestionHistoryKind.TurnEnded));

    public void RecordTurnTimedOut(int playerNumber) =>
        _history.Add(new MinigameQuestionHistoryEntry(
            playerNumber,
            MinigameQuestionHistoryKind.TurnTimedOut));

    private void RecordQuestion(int playerNumber, string question)
    {
        if (HasSelectedQuestionThisTurn)
        {
            throw new MinigameRoomException(MinigameRoomError.QuestionAlreadySelected);
        }

        if (PendingResponsePlayerNumber is not null)
        {
            throw new MinigameRoomException(MinigameRoomError.QuestionResponsePending);
        }

        _history.Add(new MinigameQuestionHistoryEntry(
            playerNumber,
            MinigameQuestionHistoryKind.Question,
            question));
        HasSelectedQuestionThisTurn = true;
        PendingQuestion = question;
        PendingResponsePlayerNumber = playerNumber == 1 ? 2 : 1;
    }

    private PlayerQuestionDeck GetDeck(int playerNumber) =>
        playerNumber switch
        {
            1 => _player1,
            2 => _player2,
            _ => throw new ArgumentOutOfRangeException(nameof(playerNumber))
        };

    private sealed class PlayerQuestionDeck
    {
        private readonly MinigameQuestionSelectionMode _selectionMode;
        private readonly Queue<string> _remaining;
        private readonly List<string> _available;
        private readonly List<string> _searchRemaining;

        private PlayerQuestionDeck(
            MinigameQuestionSelectionMode selectionMode,
            Queue<string> remaining,
            List<string> available,
            List<string> searchRemaining)
        {
            _selectionMode = selectionMode;
            _remaining = remaining;
            _available = available;
            _searchRemaining = searchRemaining;
        }

        public IReadOnlyList<string> AvailableQuestions =>
            _selectionMode == MinigameQuestionSelectionMode.Cards
                ? _available
                : Array.Empty<string>();

        public IReadOnlyList<string> RemainingSearchQuestions =>
            _selectionMode == MinigameQuestionSelectionMode.Search
                ? _searchRemaining
                : Array.Empty<string>();

        public static PlayerQuestionDeck Create(
            IReadOnlyList<string> source,
            MinigameQuestionSelectionMode selectionMode)
        {
            if (selectionMode == MinigameQuestionSelectionMode.Search)
            {
                return new PlayerQuestionDeck(
                    selectionMode,
                    new Queue<string>(),
                    [],
                    source.ToList());
            }

            var shuffled = source.ToList();
            Shuffle(shuffled);
            var remaining = new Queue<string>(shuffled);
            var available = new List<string>(MinigameQuestionStore.MinimumQuestionCount);
            while (available.Count < MinigameQuestionStore.MinimumQuestionCount &&
                   remaining.TryDequeue(out var question))
            {
                available.Add(question);
            }

            return new PlayerQuestionDeck(selectionMode, remaining, available, []);
        }

        public string Select(int optionIndex)
        {
            if (_selectionMode != MinigameQuestionSelectionMode.Cards ||
                optionIndex < 0 || optionIndex >= _available.Count)
            {
                throw new MinigameRoomException(MinigameRoomError.InvalidQuestion);
            }

            var selected = _available[optionIndex];
            if (_remaining.TryDequeue(out var replacement))
            {
                _available[optionIndex] = replacement;
            }
            else
            {
                _available.RemoveAt(optionIndex);
            }

            return selected;
        }

        public string Select(string question)
        {
            if (_selectionMode != MinigameQuestionSelectionMode.Search ||
                string.IsNullOrWhiteSpace(question))
            {
                throw new MinigameRoomException(MinigameRoomError.InvalidQuestion);
            }

            var normalized = question.Trim();
            var index = _searchRemaining.FindIndex(item =>
                string.Equals(item, normalized, StringComparison.Ordinal));
            if (index < 0)
            {
                throw new MinigameRoomException(MinigameRoomError.InvalidQuestion);
            }

            var selected = _searchRemaining[index];
            _searchRemaining.RemoveAt(index);
            return selected;
        }

        private static void Shuffle<T>(IList<T> values)
        {
            for (var index = values.Count - 1; index > 0; index--)
            {
                var swapIndex = RandomNumberGenerator.GetInt32(index + 1);
                (values[index], values[swapIndex]) = (values[swapIndex], values[index]);
            }
        }
    }
}

public enum MinigameQuestionSelectionMode
{
    Cards = 0,
    Search = 1
}

public enum MinigameQuestionHistoryKind
{
    Question = 0,
    Guess = 1,
    TurnEnded = 2,
    TurnTimedOut = 3,
    Answer = 4
}

public sealed record MinigameQuestionHistoryEntry(
    int PlayerNumber,
    MinigameQuestionHistoryKind Kind,
    string? Value = null,
    bool? IsCorrect = null,
    bool? AnswerYes = null);
