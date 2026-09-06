namespace BadWolfQuiz.Web.Services;

public sealed partial class MinigameRoomStore
{
    private readonly Dictionary<string, string> _soloOpponentTokens =
        new(StringComparer.OrdinalIgnoreCase);

    public MinigameRoomConnection EnsureSoloOpponent(
        string? roomCode,
        string? playerToken)
    {
        lock (_sync)
        {
            var now = _timeProvider.GetUtcNow();
            var room = GetActiveRoom(roomCode, now);
            var playerNumber = GetPlayerNumber(room, playerToken);
            if (playerNumber != 1)
            {
                throw new MinigameRoomException(MinigameRoomError.InvalidPlayer);
            }

            if (room.Player2Token is not null)
            {
                if (_soloOpponentTokens.TryGetValue(room.Code, out var aiToken) &&
                    string.Equals(aiToken, room.Player2Token, StringComparison.Ordinal))
                {
                    return CreateConnection(room, room.Player2Token, playerNumber: 2);
                }

                throw new MinigameRoomException(MinigameRoomError.RoomFull);
            }

            var token = CreatePlayerToken();
            room.Player2Token = token;
            _soloOpponentTokens[room.Code] = token;
            room.LastActivityUtc = now;
            room.Version++;
            return CreateConnection(room, token, playerNumber: 2);
        }
    }

    public MinigameRoomSnapshot RemoveSoloOpponent(
        string? roomCode,
        string? playerToken)
    {
        lock (_sync)
        {
            var now = _timeProvider.GetUtcNow();
            var room = GetActiveRoom(roomCode, now);
            var playerNumber = GetPlayerNumber(room, playerToken);
            if (playerNumber != 1)
            {
                throw new MinigameRoomException(MinigameRoomError.InvalidPlayer);
            }

            if (_soloOpponentTokens.TryGetValue(room.Code, out var aiToken) &&
                string.Equals(aiToken, room.Player2Token, StringComparison.Ordinal))
            {
                _soloOpponentTokens.Remove(room.Code);
                room.Player2Token = null;
                room.Player2Excluded.Clear();
                room.Player2SecretFileName = null;
                room.LastActivityUtc = now;
                room.Version++;
            }

            return CreateSnapshot(room, playerNumber);
        }
    }

    public bool IsSoloGame(
        string? roomCode,
        string? playerToken)
    {
        lock (_sync)
        {
            var now = _timeProvider.GetUtcNow();
            var room = GetActiveRoom(roomCode, now);
            _ = GetPlayerNumber(room, playerToken);
            return room.Player2Token is not null &&
                _soloOpponentTokens.TryGetValue(room.Code, out var aiToken) &&
                string.Equals(aiToken, room.Player2Token, StringComparison.Ordinal);
        }
    }
}
