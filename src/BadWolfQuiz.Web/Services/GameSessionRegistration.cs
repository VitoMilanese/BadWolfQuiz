using BadWolfQuiz.Game.Runtime;

namespace BadWolfQuiz.Web.Services;

public sealed class GameSessionRegistration
{
    public GameSessionRegistration(string publicCode, GameSession session)
    {
        PublicCode = publicCode;
        Session = session;
    }

    public string PublicCode { get; }

    public GameSession Session { get; }

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
