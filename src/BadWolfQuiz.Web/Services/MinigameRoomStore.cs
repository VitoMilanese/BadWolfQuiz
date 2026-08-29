using System.Security.Cryptography;

namespace BadWolfQuiz.Web.Services;

public sealed class MinigameRoomStore
{
    public static readonly TimeSpan InactivityTimeout = TimeSpan.FromHours(1);

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
            var room = new MinigameRoomState(
                roomCode,
                playerToken,
                now);
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
            string.Equals(
                room.Player1Token,
                playerToken,
                StringComparison.Ordinal))
        {
            return 1;
        }

        if (!string.IsNullOrWhiteSpace(playerToken) &&
            room.Player2Token is not null &&
            string.Equals(
                room.Player2Token,
                playerToken,
                StringComparison.Ordinal))
        {
            return 2;
        }

        throw new MinigameRoomException(MinigameRoomError.InvalidPlayer);
    }

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
        int playerNumber) =>
        new(
            room.Code,
            playerNumber,
            room.Player2Token is null ? 1 : 2,
            room.Version,
            room.CreatedAtUtc,
            room.LastActivityUtc,
            room.LastActivityUtc + InactivityTimeout);

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
    }
}

public enum MinigameRoomError
{
    RoomNotFound,
    RoomExpired,
    RoomFull,
    InvalidPlayer
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
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastActivityUtc,
    DateTimeOffset ExpiresAtUtc)
{
    public bool HasOpponent => PlayerCount == 2;
}
