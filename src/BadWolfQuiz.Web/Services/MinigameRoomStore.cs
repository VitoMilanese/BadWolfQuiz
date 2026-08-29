using System.Security.Cryptography;

namespace BadWolfQuiz.Web.Services;

public sealed class MinigameRoomStore
{
    public static readonly TimeSpan InactivityTimeout = TimeSpan.FromHours(1);
    public static readonly TimeSpan FirstTurnDuration = TimeSpan.FromMinutes(3);
    public static readonly TimeSpan StandardTurnDuration = TimeSpan.FromSeconds(90);
    public const int MinimumGameCardCount = 10;

    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int CodeLength = 6;
    private const int MaximumCodeAttempts = 100;

    private readonly object _sync = new();
    private readonly Dictionary<string, MinigameRoomState> _rooms =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly TimeProvider _timeProvider;

    public MinigameRoomStore(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public int Count
    {
        get
        {
            lock (_sync)
            {
                RemoveExpiredCore(_timeProvider.GetUtcNow());
                return _rooms.Count;
            }
        }
    }

    public MinigameRoomConnection CreateRoom()
    {
        lock (_sync)
        {
            var now = _timeProvider.GetUtcNow();
            RemoveExpiredCore(now);

            string? roomCode = null;
            for (var attempt = 0; attempt < MaximumCodeAttempts; attempt++)
            {
                var candidate = GenerateCode();
                if (!_rooms.ContainsKey(candidate))
                {
                    roomCode = candidate;
                    break;
                }
            }

            if (roomCode is null)
            {
                throw new InvalidOperationException(
                    "Could not allocate a unique minigame room code.");
            }

            var playerToken = CreatePlayerToken();
            var room = new MinigameRoomState(roomCode, playerToken, now);
            _rooms.Add(roomCode, room);

            return CreateConnection(room, playerToken, playerNumber: 1);
        }
    }

    public MinigameRoomConnection JoinRoom(string? roomCode)
    {
        lock (_sync)
        {
            var now = _timeProvider.GetUtcNow();
            var room = GetActiveRoom(roomCode, now);

            if (room.Player2Token is not null)
            {
                throw new MinigameRoomException(MinigameRoomError.RoomFull);
            }

            var playerToken = CreatePlayerToken();
            room.Player2Token = playerToken;
            room.LastActivityUtc = now;
            room.Version++;

            return CreateConnection(room, playerToken, playerNumber: 2);
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
            var room = GetActiveRoom(roomCode, now);
            var playerNumber = GetPlayerNumber(room, playerToken);

            if (touchActivity)
            {
                room.LastActivityUtc = now;
            }

            return CreateSnapshot(room, playerNumber);
        }
    }

    public MinigameRoomSnapshot TouchRoom(
        string? roomCode,
        string? playerToken) =>
        GetState(roomCode, playerToken, touchActivity: true);

    public MinigameRoomSnapshot StartNewGame(
        string? roomCode,
        string? playerToken,
        IReadOnlyList<MinigameCardDescriptor> cards,
        bool questionCardsEnabled = false,
        IReadOnlyList<string>? questions = null)
    {
        ArgumentNullException.ThrowIfNull(cards);
        if (cards.Count < MinimumGameCardCount ||
            cards.Select(card => card.FileName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() != cards.Count)
        {
            throw new MinigameRoomException(MinigameRoomError.InvalidCardCount);
        }

        lock (_sync)
        {
            var now = _timeProvider.GetUtcNow();
            var room = GetActiveRoom(roomCode, now);
            var playerNumber = GetPlayerNumber(room, playerToken);

            room.Cards = cards.ToArray();
            room.Player1Excluded.Clear();
            room.Player2Excluded.Clear();
            room.Player1SecretFileName = null;
            room.Player2SecretFileName = null;
            room.CurrentPlayerNumber = null;
            room.TurnDeadlineUtc = null;
            room.Player1TurnsStarted = 0;
            room.Player2TurnsStarted = 0;
            room.WinnerPlayerNumber = null;
            room.QuestionGame = questionCardsEnabled
                ? MinigameQuestionGameState.Create(questions ?? [])
                : null;
            room.Phase = MinigameRoomPhase.ChoosingExclusions;
            room.GameNumber++;
            room.LastActivityUtc = now;
            room.Version++;

            return CreateSnapshot(room, playerNumber);
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
            var room = GetActiveRoom(roomCode, now);
            var playerNumber = GetPlayerNumber(room, playerToken);

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

            var own = playerNumber == 1
                ? room.Player1Excluded
                : room.Player2Excluded;
            var opponent = playerNumber == 1
                ? room.Player2Excluded
                : room.Player1Excluded;

            if (own.Remove(card.FileName))
            {
                room.LastActivityUtc = now;
                room.Version++;
                return CreateSnapshot(room, playerNumber);
            }

            if (opponent.Contains(card.FileName))
            {
                throw new MinigameRoomException(MinigameRoomError.CardAlreadyExcluded);
            }

            var required = GetRequiredExclusionCount(room);
            if (own.Count >= required)
            {
                throw new MinigameRoomException(MinigameRoomError.ExclusionLimitReached);
            }

            own.Add(card.FileName);
            room.LastActivityUtc = now;
            room.Version++;

            if (room.Player2Token is not null &&
                room.Player1Excluded.Count == required &&
                room.Player2Excluded.Count == required)
            {
                StartPlaying(room, now);
            }

            return CreateSnapshot(room, playerNumber);
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
            var room = GetActiveRoom(roomCode, now);
            var playerNumber = GetPlayerNumber(room, playerToken);
            RequireCurrentTurn(room, playerNumber);

            if (room.QuestionGame is null)
            {
                throw new MinigameRoomException(MinigameRoomError.InvalidPhase);
            }

            room.QuestionGame.SelectQuestion(playerNumber, optionIndex);
            room.LastActivityUtc = now;
            room.Version++;
            return CreateSnapshot(room, playerNumber);
        }
    }

    public MinigameRoomSnapshot EndTurn(
        string? roomCode,
        string? playerToken)
    {
        lock (_sync)
        {
            var now = _timeProvider.GetUtcNow();
            var room = GetActiveRoom(roomCode, now);
            var playerNumber = GetPlayerNumber(room, playerToken);
            RequireCurrentTurn(room, playerNumber);

            room.QuestionGame?.RecordTurnEnded(playerNumber);
            room.LastActivityUtc = now;
            AdvanceTurn(room, now);
            room.Version++;
            return CreateSnapshot(room, playerNumber);
        }
    }

    public MinigameRoomSnapshot ExpireTurn(
        string? roomCode,
        string? playerToken)
    {
        lock (_sync)
        {
            var now = _timeProvider.GetUtcNow();
            var room = GetActiveRoom(roomCode, now);
            var playerNumber = GetPlayerNumber(room, playerToken);

            if (room.Phase != MinigameRoomPhase.Playing ||
                room.TurnDeadlineUtc is null ||
                now < room.TurnDeadlineUtc.Value)
            {
                return CreateSnapshot(room, playerNumber);
            }

            if (room.QuestionGame is not null &&
                room.CurrentPlayerNumber is int currentPlayer)
            {
                room.QuestionGame.RecordTurnTimedOut(currentPlayer);
            }

            AdvanceTurn(room, now);
            room.Version++;
            return CreateSnapshot(room, playerNumber);
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
            var room = GetActiveRoom(roomCode, now);
            var playerNumber = GetPlayerNumber(room, playerToken);
            RequireCurrentTurn(room, playerNumber);

            var activeCards = GetActiveCards(room);
            var guessedCard = activeCards.FirstOrDefault(card =>
                string.Equals(card.FileName, fileName, StringComparison.Ordinal));
            if (guessedCard is null)
            {
                throw new MinigameRoomException(MinigameRoomError.CardNotFound);
            }

            var opponentSecret = playerNumber == 1
                ? room.Player2SecretFileName
                : room.Player1SecretFileName;
            if (string.IsNullOrWhiteSpace(opponentSecret))
            {
                throw new MinigameRoomException(MinigameRoomError.InvalidPhase);
            }

            var isCorrect = string.Equals(
                guessedCard.FileName,
                opponentSecret,
                StringComparison.Ordinal);
            room.QuestionGame?.RecordGuess(
                playerNumber,
                guessedCard.DisplayName,
                isCorrect);
            room.LastActivityUtc = now;
            if (isCorrect)
            {
                room.WinnerPlayerNumber = playerNumber;
                room.Phase = MinigameRoomPhase.Finished;
                room.TurnDeadlineUtc = null;
            }
            else
            {
                AdvanceTurn(room, now);
            }

            room.Version++;
            return CreateSnapshot(room, playerNumber);
        }
    }

    public IReadOnlyList<string> RemoveExpired()
    {
        lock (_sync)
        {
            return RemoveExpiredCore(_timeProvider.GetUtcNow());
        }
    }

    public static string GetSignalRGroupName(string roomCode) =>
        $"minigame-room-{NormalizeRoomCode(roomCode)}";

    private MinigameRoomState GetActiveRoom(
        string? roomCode,
        DateTimeOffset now)
    {
        var normalized = NormalizeRoomCode(roomCode);
        if (!_rooms.TryGetValue(normalized, out var room))
        {
            throw new MinigameRoomException(MinigameRoomError.RoomNotFound);
        }

        if (IsExpired(room, now))
        {
            _rooms.Remove(normalized);
            throw new MinigameRoomException(MinigameRoomError.RoomExpired);
        }

        return room;
    }

    private static int GetPlayerNumber(
        MinigameRoomState room,
        string? playerToken)
    {
        if (!string.IsNullOrWhiteSpace(playerToken) &&
            string.Equals(room.Player1Token, playerToken, StringComparison.Ordinal))
        {
            return 1;
        }

        if (!string.IsNullOrWhiteSpace(playerToken) &&
            room.Player2Token is not null &&
            string.Equals(room.Player2Token, playerToken, StringComparison.Ordinal))
        {
            return 2;
        }

        throw new MinigameRoomException(MinigameRoomError.InvalidPlayer);
    }

    private static void RequireCurrentTurn(
        MinigameRoomState room,
        int playerNumber)
    {
        if (room.Phase != MinigameRoomPhase.Playing)
        {
            throw new MinigameRoomException(MinigameRoomError.InvalidPhase);
        }

        if (room.CurrentPlayerNumber != playerNumber)
        {
            throw new MinigameRoomException(MinigameRoomError.NotYourTurn);
        }
    }

    private static void StartPlaying(
        MinigameRoomState room,
        DateTimeOffset now)
    {
        var active = GetActiveCards(room);
        if (active.Length < 2)
        {
            throw new InvalidOperationException(
                "A minigame requires at least two active cards.");
        }

        var player1Index = RandomNumberGenerator.GetInt32(active.Length);
        var player2Index = RandomNumberGenerator.GetInt32(active.Length - 1);
        if (player2Index >= player1Index)
        {
            player2Index++;
        }

        room.Player1SecretFileName = active[player1Index].FileName;
        room.Player2SecretFileName = active[player2Index].FileName;
        room.Phase = MinigameRoomPhase.Playing;
        room.CurrentPlayerNumber = null;
        room.TurnDeadlineUtc = null;
        room.Player1TurnsStarted = 0;
        room.Player2TurnsStarted = 0;
        room.WinnerPlayerNumber = null;
        BeginTurn(room, playerNumber: 1, now);
    }

    private static MinigameCardDescriptor[] GetActiveCards(MinigameRoomState room)
    {
        var excluded = room.Player1Excluded
            .Concat(room.Player2Excluded)
            .ToHashSet(StringComparer.Ordinal);
        return room.Cards
            .Where(card => !excluded.Contains(card.FileName))
            .ToArray();
    }

    private static void AdvanceTurn(
        MinigameRoomState room,
        DateTimeOffset now)
    {
        var nextPlayer = room.CurrentPlayerNumber == 1 ? 2 : 1;
        BeginTurn(room, nextPlayer, now);
    }

    private static void BeginTurn(
        MinigameRoomState room,
        int playerNumber,
        DateTimeOffset now)
    {
        var turnsAlreadyStarted = playerNumber == 1
            ? room.Player1TurnsStarted
            : room.Player2TurnsStarted;
        var duration = turnsAlreadyStarted == 0
            ? FirstTurnDuration
            : StandardTurnDuration;

        if (playerNumber == 1)
        {
            room.Player1TurnsStarted++;
        }
        else
        {
            room.Player2TurnsStarted++;
        }

        room.QuestionGame?.BeginTurn();
        room.CurrentPlayerNumber = playerNumber;
        room.TurnDeadlineUtc = now + duration;
    }

    private static int GetRequiredExclusionCount(MinigameRoomState room) =>
        room.Cards.Count / 10;

    private static MinigameRoomConnection CreateConnection(
        MinigameRoomState room,
        string playerToken,
        int playerNumber) =>
        new(
            room.Code,
            playerToken,
            playerNumber,
            CreateSnapshot(room, playerNumber));

    private static MinigameRoomSnapshot CreateSnapshot(
        MinigameRoomState room,
        int playerNumber)
    {
        var myExcluded = playerNumber == 1
            ? room.Player1Excluded
            : room.Player2Excluded;
        var opponentExcluded = playerNumber == 1
            ? room.Player2Excluded
            : room.Player1Excluded;
        var visibleCards = room.Phase is
                MinigameRoomPhase.Playing or MinigameRoomPhase.Finished
            ? GetActiveCards(room)
            : room.Cards.ToArray();
        var secret = room.Phase is
                MinigameRoomPhase.Playing or MinigameRoomPhase.Finished
            ? playerNumber == 1
                ? room.Player1SecretFileName
                : room.Player2SecretFileName
            : null;

        return new MinigameRoomSnapshot(
            room.Code,
            playerNumber,
            room.Player2Token is null ? 1 : 2,
            room.Version,
            room.GameNumber,
            room.Phase,
            visibleCards,
            GetRequiredExclusionCount(room),
            myExcluded.Order(StringComparer.Ordinal).ToArray(),
            opponentExcluded.Order(StringComparer.Ordinal).ToArray(),
            secret,
            room.CurrentPlayerNumber,
            room.TurnDeadlineUtc,
            room.WinnerPlayerNumber,
            room.QuestionGame is not null,
            room.QuestionGame?.GetAvailableQuestions(playerNumber).ToArray() ?? [],
            room.QuestionGame?.HasSelectedQuestionThisTurn ?? false,
            room.QuestionGame?.History.ToArray() ?? [],
            room.CreatedAtUtc,
            room.LastActivityUtc,
            room.LastActivityUtc + InactivityTimeout);
    }

    private IReadOnlyList<string> RemoveExpiredCore(DateTimeOffset now)
    {
        var expiredCodes = _rooms.Values
            .Where(room => IsExpired(room, now))
            .Select(room => room.Code)
            .ToArray();

        foreach (var code in expiredCodes)
        {
            _rooms.Remove(code);
        }

        return expiredCodes;
    }

    private static bool IsExpired(
        MinigameRoomState room,
        DateTimeOffset now) =>
        now - room.LastActivityUtc >= InactivityTimeout;

    private static string NormalizeRoomCode(string? roomCode)
    {
        var normalized = roomCode?.Trim().ToUpperInvariant() ?? string.Empty;
        if (normalized.Length != CodeLength ||
            normalized.Any(character => !CodeAlphabet.Contains(character)))
        {
            throw new MinigameRoomException(MinigameRoomError.RoomNotFound);
        }

        return normalized;
    }

    private static string GenerateCode()
    {
        Span<char> code = stackalloc char[CodeLength];
        for (var index = 0; index < code.Length; index++)
        {
            code[index] = CodeAlphabet[
                RandomNumberGenerator.GetInt32(CodeAlphabet.Length)];
        }

        return new string(code);
    }

    private static string CreatePlayerToken() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(24));

    private sealed class MinigameRoomState(
        string code,
        string player1Token,
        DateTimeOffset createdAtUtc)
    {
        public string Code { get; } = code;
        public string Player1Token { get; } = player1Token;
        public string? Player2Token { get; set; }
        public DateTimeOffset CreatedAtUtc { get; } = createdAtUtc;
        public DateTimeOffset LastActivityUtc { get; set; } = createdAtUtc;
        public long Version { get; set; } = 1;
        public long GameNumber { get; set; }
        public MinigameRoomPhase Phase { get; set; } = MinigameRoomPhase.WaitingForGame;
        public IReadOnlyList<MinigameCardDescriptor> Cards { get; set; } = [];
        public HashSet<string> Player1Excluded { get; } = new(StringComparer.Ordinal);
        public HashSet<string> Player2Excluded { get; } = new(StringComparer.Ordinal);
        public string? Player1SecretFileName { get; set; }
        public string? Player2SecretFileName { get; set; }
        public int? CurrentPlayerNumber { get; set; }
        public DateTimeOffset? TurnDeadlineUtc { get; set; }
        public int Player1TurnsStarted { get; set; }
        public int Player2TurnsStarted { get; set; }
        public int? WinnerPlayerNumber { get; set; }
        public MinigameQuestionGameState? QuestionGame { get; set; }
    }
}

public enum MinigameRoomPhase
{
    WaitingForGame = 0,
    ChoosingExclusions = 1,
    Playing = 2,
    Finished = 3
}

public enum MinigameRoomError
{
    RoomNotFound,
    RoomExpired,
    RoomFull,
    InvalidPlayer,
    InvalidCardCount,
    InvalidPhase,
    CardNotFound,
    CardAlreadyExcluded,
    ExclusionLimitReached,
    NotYourTurn,
    InvalidQuestion,
    QuestionAlreadySelected,
    QuestionRequired,
    QuestionsUnavailable
}

public sealed class MinigameRoomException(MinigameRoomError error)
    : InvalidOperationException(error.ToString())
{
    public MinigameRoomError Error { get; } = error;
}

public sealed record MinigameRoomConnection(
    string RoomCode,
    string PlayerToken,
    int PlayerNumber,
    MinigameRoomSnapshot State);

public sealed record MinigameRoomSnapshot(
    string RoomCode,
    int PlayerNumber,
    int PlayerCount,
    long Version,
    long GameNumber,
    MinigameRoomPhase Phase,
    IReadOnlyList<MinigameCardDescriptor> Cards,
    int RequiredExclusionsPerPlayer,
    IReadOnlyList<string> MyExcludedFiles,
    IReadOnlyList<string> OpponentExcludedFiles,
    string? MySecretCardFileName,
    int? CurrentPlayerNumber,
    DateTimeOffset? TurnDeadlineUtc,
    int? WinnerPlayerNumber,
    bool QuestionCardsEnabled,
    IReadOnlyList<string> MyAvailableQuestions,
    bool HasSelectedQuestionThisTurn,
    IReadOnlyList<MinigameQuestionHistoryEntry> QuestionHistory,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastActivityUtc,
    DateTimeOffset ExpiresAtUtc)
{
    public bool HasOpponent => PlayerCount == 2;
}
