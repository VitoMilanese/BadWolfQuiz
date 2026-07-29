namespace BadWolfQuiz.Game.Runtime;

public sealed record Wager(
    GamePlayerId PlayerId,
    int Amount,
    DateTimeOffset SubmittedAtUtc);

public readonly record struct WagerLimits(int Minimum, int Maximum)
{
    public bool Contains(int amount)
        => amount >= Minimum && amount <= Maximum;
}
