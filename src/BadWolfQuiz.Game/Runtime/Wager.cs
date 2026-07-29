namespace BadWolfQuiz.Game.Runtime;

public sealed record Wager(
    GamePlayerId PlayerId,
    int Amount,
    DateTimeOffset SubmittedAtUtc);
