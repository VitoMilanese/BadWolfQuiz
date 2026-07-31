using BadWolfQuiz.Game.Runtime;

namespace BadWolfQuiz.Web.Services;

public sealed class GameSessionRegistration
{
    public GameSessionRegistration(string publicCode, GameSession session, string? hostId = null)
    {
        PublicCode = publicCode;
        Session = session;
        HostId = hostId;
    }

    public string PublicCode { get; }

    public GameSession Session { get; }
    public string? HostId { get; }

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
