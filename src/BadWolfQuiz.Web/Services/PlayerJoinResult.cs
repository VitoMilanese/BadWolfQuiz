using BadWolfQuiz.Game.Runtime;

namespace BadWolfQuiz.Web.Services;

public sealed record PlayerJoinResult(
    PlayerJoinStatus Status,
    GameSessionRegistration? Game = null,
    GamePlayer? Player = null,
    string? AccessToken = null)
{
    public static PlayerJoinResult Succeeded(
        GameSessionRegistration game,
        GamePlayer player,
        string accessToken) =>
        new(PlayerJoinStatus.Success, game, player, accessToken);

    public static PlayerJoinResult Failed(PlayerJoinStatus status) => new(status);
}

public enum PlayerJoinStatus
{
    Success = 1,
    GameNotFound = 2,
    NameAlreadyUsed = 3,
    GameAlreadyStarted = 4,
    PlayerBlocked = 5,
    GameClosed = 6
}
