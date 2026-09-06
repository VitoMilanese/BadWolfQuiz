using System.Security.Cryptography;

namespace BadWolfQuiz.Web.Services;

internal sealed class MinigameAiRoomStore
{
    private readonly object _sync = new();
    private readonly Dictionary<string, AiRoomState> _rooms =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly TimeProvider _timeProvider;

    public MinigameAiRoomStore(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public bool IsActive(string? roomCode)
    {
        lock (_sync)
        {
            RemoveExpiredCore(_timeProvider.GetUtcNow());
            return !string.IsNullOrWhiteSpace(roomCode) && _rooms.ContainsKey(roomCode);
        }
    }

    public MinigameRoomSnapshot Start(
        string roomCode,
        string playerToken,
        MinigameRoomSnapshot baseState,
        MinigameAiGameData gameData)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(playerToken);
        ArgumentNullException.ThrowIfNull(baseState);
        ArgumentNullException.ThrowIfNull(gameData);
        if (baseState.PlayerNumber != 1 ||
            gameData.Games.Count < MinigameRoomStore.MinimumGameCardCount)
        {
            throw new MinigameRoomException(MinigameRoomError.InvalidPlayer);
        }

        lock (_sync)
        {
            var now = _timeProvider.GetUtcNow();
            RemoveExpiredCore(now);
            var room = new AiRoomState(
                roomCode,
                playerToken,
                baseState.Version,
                baseState.GameNumber + 1,
                baseState.Theme,
                now,
                gameData.Games.Select(game => game.Card).ToArray(),
                MinigameQuestionGameState.Create(gameData.Questions),
                new MinigameAiOpponent(gameData.Games));
            ChooseAiExclusions(room);
            _rooms[roomCode] = room;
            return CreateSnapshot(room);
        }
    }

    public MinigameRoomSnapshot GetState(
        string? roomCode,
        string? playerToken,
        bool touchActivity = true)
    {
        lock (_sync)
        {
            var now = _timeProvider.GetUtcNow();
            var room = GetRoom(roomCode, playerToken, now);
            if (touchActivity)
            {
                room.LastActivityUtc = now;
            }
            return CreateSnapshot(room);
        }
    }

    public MinigameRoomSnapshot TouchRoom(string? roomCode, string? playerToken) =>
        GetState(roomCode, playerToken, touchActivity: true);

    public MinigameAiRoomStatus GetStatus(string? roomCode, string? playerToken)
    {
        lock (_sync)
        {
            var room = GetRoom(roomCode, playerToken, _timeProvider.GetUtcNow());
            return new MinigameAiRoomStatus(true, room.IsDraw);
        }
    }

    public void Remove(string? roomCode, string? playerToken)
    {
        lock (_sync)
        {
            var now = _timeProvider.GetUtcNow();
            var room = GetRoom(roomCode, playerToken, now);
            _rooms.Remove(room.Code);
        }
    }

    public MinigameRoomSnapshot ToggleExclusion(
        string? roomCode,
        string? playerToken,
        string? fileName)
    {
        lock (_sync)
        {
            var now = _timeProvider.GetUtcNow();
            var room = GetRoom(roomCode, playerToken, now);
            if (room.Phase != MinigameRoomPhase.ChoosingExclusions)
            {
                throw new MinigameRoomException(MinigameRoomError.InvalidPhase);
            }

            var card = room.Cards.FirstOrDefault(item =>
                string.Equals(item.FileName, fileName, StringComparison.Ordinal));
            if (card is null)
            {
                throw new MinigameRoomException(MinigameRoomError.CardNotFound);
            }

            if (room.Player1Excluded.Remove(card.FileName))
            {
                room.LastActivityUtc = now;
                return CreateSnapshot(room);
            }

            if (room.AiExcluded.Contains(card.FileName))
            {
                throw new MinigameRoomException(MinigameRoomError.CardAlreadyExcluded);
            }

            var required = RequiredExclusions(room);
            if (room.Player1Excluded.Count >= required)
            {
                throw new MinigameRoomException(MinigameRoomError.ExclusionLimitReached);
            }

            room.Player1Excluded.Add(card.FileName);
            room.LastActivityUtc = now;
            if (room.Player1Excluded.Count == required)
            {
                StartPlaying(room, now);
            }
            return CreateSnapshot(room);
        }
    }

    public MinigameRoomSnapshot SelectQuestion(
        string? roomCode,
        string? playerToken,
        int optionIndex)
    {
        lock (_sync)
        {
            var now = _timeProvider.GetUtcNow();
            var room = GetRoom(roomCode, playerToken, now);
            RequirePlayerTurn(room);
            var available = room.QuestionGame.GetAvailableQuestions(1);
            if (optionIndex < 0 || optionIndex >= available.Count)
            {
                throw new MinigameRoomException(MinigameRoomError.InvalidQuestion);
            }

            var question = available[optionIndex];
            room.QuestionGame.SelectQuestion(1, optionIndex);
            var answer = room.Opponent.GetAnswer(room.AiSecretFileName, question);
            room.QuestionGame.SubmitQuestionResponse(2, answer);
            room.LastActivityUtc = now;
            return CreateSnapshot(room);
        }
    }

    public MinigameRoomSnapshot SubmitQuestionResponse(
        string? roomCode,
        string? playerToken,
        bool? answerYes)
    {
        lock (_sync)
        {
            var now = _timeProvider.GetUtcNow();
            var room = GetRoom(roomCode, playerToken, now);
            if (room.Phase != MinigameRoomPhase.Playing ||
                room.CurrentPlayerNumber != 2)
            {
                throw new MinigameRoomException(MinigameRoomError.InvalidPhase);
            }

            var question = room.QuestionGame.PendingQuestion;
            room.QuestionGame.SubmitQuestionResponse(1, answerYes);
            if (!string.IsNullOrWhiteSpace(question))
            {
                room.Opponent.ApplyAnswer(question, answerYes);
            }
            room.LastActivityUtc = now;

            if (room.Opponent.CandidateCount == 0)
            {
                FinishDraw(room);
            }
            else if (room.Opponent.CandidateCount == 1)
            {
                var action = room.Opponent.Decide([]);
                SubmitAiGuess(room, action.GuessFileName, now);
            }
            else
            {
                BeginTurn(room, 1, now);
            }
            return CreateSnapshot(room);
        }
    }

    public MinigameRoomSnapshot EndTurn(string? roomCode, string? playerToken)
    {
        lock (_sync)
        {
            var now = _timeProvider.GetUtcNow();
            var room = GetRoom(roomCode, playerToken, now);
            RequirePlayerTurn(room);
            room.QuestionGame.RecordTurnEnded(1);
            room.LastActivityUtc = now;
            BeginTurn(room, 2, now);
            RunAiTurn(room, now);
            return CreateSnapshot(room);
        }
    }

    public MinigameRoomSnapshot ExpireTurn(string? roomCode, string? playerToken)
    {
        lock (_sync)
        {
            var now = _timeProvider.GetUtcNow();
            var room = GetRoom(roomCode, playerToken, now);
            if (room.Phase != MinigameRoomPhase.Playing ||
                room.TurnDeadlineUtc is null ||
                now < room.TurnDeadlineUtc.Value)
            {
                return CreateSnapshot(room);
            }

            var current = room.CurrentPlayerNumber ?? 1;
            if (current == 2)
            {
                room.QuestionGame.CancelPendingResponse();
            }
            room.QuestionGame.RecordTurnTimedOut(current);
            BeginTurn(room, current == 1 ? 2 : 1, now);
            if (room.CurrentPlayerNumber == 2)
            {
                RunAiTurn(room, now);
            }
            return CreateSnapshot(room);
        }
    }

    public MinigameRoomSnapshot SubmitGuess(
        string? roomCode,
        string? playerToken,
        string? fileName)
    {
        lock (_sync)
        {
            var now = _timeProvider.GetUtcNow();
            var room = GetRoom(roomCode, playerToken, now);
            RequirePlayerTurn(room);
            var card = GetActiveCards(room).FirstOrDefault(item =>
                string.Equals(item.FileName, fileName, StringComparison.Ordinal));
            if (card is null)
            {
                throw new MinigameRoomException(MinigameRoomError.CardNotFound);
            }

            var correct = string.Equals(
                card.FileName,
                room.AiSecretFileName,
                StringComparison.Ordinal);
            room.QuestionGame.RecordGuess(1, card.DisplayName, correct);
            room.LastActivityUtc = now;
            if (correct)
            {
                FinishWithWinner(room, 1);
            }
            else
            {
                BeginTurn(room, 2, now);
                RunAiTurn(room, now);
            }
            return CreateSnapshot(room);
        }
    }

    public MinigameRoomSnapshot Restart(string? roomCode, string? playerToken)
    {
        lock (_sync)
        {
            var now = _timeProvider.GetUtcNow();
            var room = GetRoom(roomCode, playerToken, now);
            if (room.Phase is not (MinigameRoomPhase.Playing or MinigameRoomPhase.Finished))
            {
                throw new MinigameRoomException(MinigameRoomError.InvalidPhase);
            }

            var active = GetActiveCards(room);
            var replacements = active.Where(card =>
                    !string.Equals(card.FileName, room.Player1SecretFileName, StringComparison.Ordinal) &&
                    !string.Equals(card.FileName, room.AiSecretFileName, StringComparison.Ordinal))
                .ToArray();
            if (replacements.Length == 0)
            {
                throw new MinigameRoomException(MinigameRoomError.InvalidPhase);
            }

            room.Player1SecretFileName = replacements[
                RandomNumberGenerator.GetInt32(replacements.Length)].FileName;
            room.QuestionGame = room.QuestionGame.Restart();
            room.GameNumber++;
            room.WinnerPlayerNumber = null;
            room.IsDraw = false;
            room.Phase = MinigameRoomPhase.Playing;
            room.Player1TurnsStarted = 0;
            room.AiTurnsStarted = 0;
            room.Opponent.Reset(active, room.AiSecretFileName);
            room.LastActivityUtc = now;
            BeginTurn(room, 1, now);
            return CreateSnapshot(room);
        }
    }

    private static void ChooseAiExclusions(AiRoomState room)
    {
        var required = RequiredExclusions(room);
        var candidates = room.Cards.Select(card => card.FileName).ToList();
        while (room.AiExcluded.Count < required)
        {
            var index = RandomNumberGenerator.GetInt32(candidates.Count);
            room.AiExcluded.Add(candidates[index]);
            candidates.RemoveAt(index);
        }
    }

    private static void StartPlaying(AiRoomState room, DateTimeOffset now)
    {
        var active = GetActiveCards(room);
        var playerIndex = RandomNumberGenerator.GetInt32(active.Length);
        var aiIndex = RandomNumberGenerator.GetInt32(active.Length - 1);
        if (aiIndex >= playerIndex)
        {
            aiIndex++;
        }
        room.Player1SecretFileName = active[playerIndex].FileName;
        room.AiSecretFileName = active[aiIndex].FileName;
        room.Opponent.Reset(active, room.AiSecretFileName);
        room.Phase = MinigameRoomPhase.Playing;
        room.Player1TurnsStarted = 0;
        room.AiTurnsStarted = 0;
        BeginTurn(room, 1, now);
    }

    private static void RunAiTurn(AiRoomState room, DateTimeOffset now)
    {
        if (room.Phase != MinigameRoomPhase.Playing || room.CurrentPlayerNumber != 2)
        {
            return;
        }

        var action = room.Opponent.Decide(room.QuestionGame.GetAvailableQuestions(2));
        switch (action.Kind)
        {
            case MinigameAiActionKind.Draw:
                FinishDraw(room);
                break;
            case MinigameAiActionKind.Guess:
                SubmitAiGuess(room, action.GuessFileName, now);
                break;
            case MinigameAiActionKind.AskQuestion:
                room.QuestionGame.SelectQuestion(2, action.QuestionOptionIndex);
                break;
            case MinigameAiActionKind.Pass:
                room.QuestionGame.RecordTurnEnded(2);
                BeginTurn(room, 1, now);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private static void SubmitAiGuess(
        AiRoomState room,
        string? fileName,
        DateTimeOffset now)
    {
        var card = GetActiveCards(room).FirstOrDefault(item =>
            string.Equals(item.FileName, fileName, StringComparison.Ordinal));
        if (card is null)
        {
            FinishDraw(room);
            return;
        }

        var correct = string.Equals(
            card.FileName,
            room.Player1SecretFileName,
            StringComparison.Ordinal);
        room.QuestionGame.RecordGuess(2, card.DisplayName, correct);
        if (correct)
        {
            FinishWithWinner(room, 2);
        }
        else
        {
            BeginTurn(room, 1, now);
        }
    }

    private static void FinishWithWinner(AiRoomState room, int playerNumber)
    {
        room.WinnerPlayerNumber = playerNumber;
        room.IsDraw = false;
        room.Phase = MinigameRoomPhase.Finished;
        room.TurnDeadlineUtc = null;
    }

    private static void FinishDraw(AiRoomState room)
    {
        room.WinnerPlayerNumber = null;
        room.IsDraw = true;
        room.Phase = MinigameRoomPhase.Finished;
        room.TurnDeadlineUtc = null;
        room.QuestionGame.CancelPendingResponse();
    }

    private static void RequirePlayerTurn(AiRoomState room)
    {
        if (room.Phase != MinigameRoomPhase.Playing || room.CurrentPlayerNumber != 1)
        {
            throw new MinigameRoomException(MinigameRoomError.NotYourTurn);
        }
    }

    private static void BeginTurn(AiRoomState room, int playerNumber, DateTimeOffset now)
    {
        var turns = playerNumber == 1 ? room.Player1TurnsStarted : room.AiTurnsStarted;
        var duration = turns == 0
            ? MinigameRoomStore.FirstTurnDuration
            : MinigameRoomStore.StandardTurnDuration;
        if (playerNumber == 1)
        {
            room.Player1TurnsStarted++;
        }
        else
        {
            room.AiTurnsStarted++;
        }
        room.QuestionGame.BeginTurn();
        room.CurrentPlayerNumber = playerNumber;
        room.TurnDeadlineUtc = now + duration;
    }

    private static int RequiredExclusions(AiRoomState room) => room.Cards.Count / 10;

    private static MinigameCardDescriptor[] GetActiveCards(AiRoomState room)
    {
        var excluded = room.Player1Excluded
            .Concat(room.AiExcluded)
            .ToHashSet(StringComparer.Ordinal);
        return room.Cards.Where(card => !excluded.Contains(card.FileName)).ToArray();
    }

    private AiRoomState GetRoom(
        string? roomCode,
        string? playerToken,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(roomCode) || !_rooms.TryGetValue(roomCode, out var room))
        {
            throw new MinigameRoomException(MinigameRoomError.RoomNotFound);
        }
        if (now - room.LastActivityUtc >= MinigameRoomStore.InactivityTimeout)
        {
            _rooms.Remove(room.Code);
            throw new MinigameRoomException(MinigameRoomError.RoomExpired);
        }
        if (!string.Equals(room.PlayerToken, playerToken, StringComparison.Ordinal))
        {
            throw new MinigameRoomException(MinigameRoomError.InvalidPlayer);
        }
        return room;
    }

    private void RemoveExpiredCore(DateTimeOffset now)
    {
        foreach (var code in _rooms.Values
                     .Where(room => now - room.LastActivityUtc >= MinigameRoomStore.InactivityTimeout)
                     .Select(room => room.Code)
                     .ToArray())
        {
            _rooms.Remove(code);
        }
    }

    private static MinigameRoomSnapshot CreateSnapshot(AiRoomState room)
    {
        var visibleCards = room.Phase is MinigameRoomPhase.Playing or MinigameRoomPhase.Finished
            ? GetActiveCards(room)
            : room.Cards.ToArray();
        var secret = room.Phase is MinigameRoomPhase.Playing or MinigameRoomPhase.Finished
            ? room.Player1SecretFileName
            : null;
        return new MinigameRoomSnapshot(
            room.Code,
            1,
            2,
            room.BaseVersion,
            room.GameNumber,
            room.Phase,
            visibleCards,
            RequiredExclusions(room),
            room.Player1Excluded.Order(StringComparer.Ordinal).ToArray(),
            room.AiExcluded.Order(StringComparer.Ordinal).ToArray(),
            secret,
            room.CurrentPlayerNumber,
            room.TurnDeadlineUtc,
            room.WinnerPlayerNumber,
            true,
            room.QuestionGame.GetAvailableQuestions(1).ToArray(),
            room.QuestionGame.HasSelectedQuestionThisTurn,
            room.QuestionGame.PendingQuestion,
            room.QuestionGame.PendingResponsePlayerNumber,
            room.QuestionGame.History.ToArray(),
            room.CreatedAtUtc,
            room.LastActivityUtc,
            room.LastActivityUtc + MinigameRoomStore.InactivityTimeout,
            room.Theme);
    }

    private sealed class AiRoomState(
        string code,
        string playerToken,
        long baseVersion,
        long gameNumber,
        MinigameThemeSnapshot theme,
        DateTimeOffset createdAtUtc,
        IReadOnlyList<MinigameCardDescriptor> cards,
        MinigameQuestionGameState questionGame,
        MinigameAiOpponent opponent)
    {
        public string Code { get; } = code;
        public string PlayerToken { get; } = playerToken;
        public long BaseVersion { get; } = baseVersion;
        public long GameNumber { get; set; } = gameNumber;
        public MinigameThemeSnapshot Theme { get; } = theme;
        public DateTimeOffset CreatedAtUtc { get; } = createdAtUtc;
        public DateTimeOffset LastActivityUtc { get; set; } = createdAtUtc;
        public IReadOnlyList<MinigameCardDescriptor> Cards { get; } = cards;
        public HashSet<string> Player1Excluded { get; } = new(StringComparer.Ordinal);
        public HashSet<string> AiExcluded { get; } = new(StringComparer.Ordinal);
        public string? Player1SecretFileName { get; set; }
        public string? AiSecretFileName { get; set; }
        public MinigameRoomPhase Phase { get; set; } = MinigameRoomPhase.ChoosingExclusions;
        public int? CurrentPlayerNumber { get; set; }
        public DateTimeOffset? TurnDeadlineUtc { get; set; }
        public int Player1TurnsStarted { get; set; }
        public int AiTurnsStarted { get; set; }
        public int? WinnerPlayerNumber { get; set; }
        public bool IsDraw { get; set; }
        public MinigameQuestionGameState QuestionGame { get; set; } = questionGame;
        public MinigameAiOpponent Opponent { get; } = opponent;
    }
}

public sealed record MinigameAiRoomStatus(bool IsAiOpponent, bool IsDraw);
