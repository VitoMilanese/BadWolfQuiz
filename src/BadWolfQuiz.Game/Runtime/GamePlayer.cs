namespace BadWolfQuiz.Game.Runtime;

public sealed class GamePlayer
{
    internal GamePlayer(GamePlayerId id, string name, DateTimeOffset joinedAtUtc)
    {
        Id = id;
        Name = name;
        JoinedAtUtc = joinedAtUtc;
    }

    public GamePlayerId Id { get; }

    public string Name { get; }

    public int Score { get; private set; }

    public DateTimeOffset JoinedAtUtc { get; }

    internal void ApplyScore(int points)
    {
        Score = checked(Score + points);
    }
}
