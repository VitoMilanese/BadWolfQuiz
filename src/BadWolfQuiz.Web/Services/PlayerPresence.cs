using BadWolfQuiz.Game.Runtime;

namespace BadWolfQuiz.Web.Services;

public sealed record PlayerLobbyEntry(
    GamePlayerId Id,
    string Name,
    int Score,
    PlayerPresenceStatus Presence);

public sealed record PlayerConnectionResult(
    GameSessionRegistration Game,
    GamePlayer Player);

public enum PlayerPresenceStatus
{
    Disconnected = 1,
    Inactive = 2,
    Active = 3
}
