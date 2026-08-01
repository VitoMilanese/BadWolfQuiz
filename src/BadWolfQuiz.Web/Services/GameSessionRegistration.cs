using BadWolfQuiz.Game.Runtime;

namespace BadWolfQuiz.Web.Services;

public sealed class GameSessionRegistration
{
    public GameSessionRegistration(
        string publicCode,
        GameSession session,
        string? hostId = null,
        bool isRecovered = false)
    {
        PublicCode = publicCode;
        Session = session;
        HostId = hostId;
        IsRecovered = isRecovered;
        RecoveredPlayerIdsAwaitingReconnect = isRecovered
            ? session.Players.Select(player => player.Id).ToHashSet()
            : [];
    }

    public string PublicCode { get; }

    public GameSession Session { get; }
    public string? HostId { get; }

    public bool IsRecovered { get; }

    internal HashSet<GamePlayerId> RecoveredPlayerIdsAwaitingReconnect { get; }

    public bool AllowsNewPlayers { get; internal set; } = true;

    public BuzzerRaceSnapshot? BuzzerRace { get; internal set; }
}

public sealed record BuzzerRaceSnapshot(
    int SourceQuestionId,
    DateTimeOffset WinnerPressedAt,
    GamePlayerId WinnerPlayerId,
    string WinnerPlayerName,
    IReadOnlyList<BuzzerRaceLatePlayer> LatePlayers);

public sealed record BuzzerRaceLatePlayer(
    GamePlayerId PlayerId,
    string PlayerName,
    int DelayMilliseconds);
