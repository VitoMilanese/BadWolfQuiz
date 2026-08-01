using BadWolfQuiz.Game.Runtime;

namespace BadWolfQuiz.Web.Services;

public sealed record PlayerLobbyEntry(
    GamePlayerId Id,
    string Name,
    int Score,
    string? AvatarId,
    string? UploadedImageDataUrl,
    bool UsesUploadedImage,
    bool IsWebcamEnabled,
    PlayerPresenceStatus Presence);

public sealed record PlayerConnectionResult(
    GameSessionRegistration Game,
    GamePlayer Player,
    bool RequiresApproval);

public sealed record PlayerRejoinApproval(
    GameSessionRegistration Game,
    IReadOnlyList<string> ConnectionIds);

public sealed record PlayerRemoval(
    GameSessionRegistration Game,
    GamePlayer Player,
    IReadOnlyList<string> ConnectionIds);

public enum PlayerPresenceStatus
{
    Disconnected = 1,
    RejoinPending = 2,
    Inactive = 3,
    Active = 4
}
