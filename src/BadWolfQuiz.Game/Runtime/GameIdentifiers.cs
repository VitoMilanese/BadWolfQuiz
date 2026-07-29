namespace BadWolfQuiz.Game.Runtime;

public readonly record struct GameSessionId(Guid Value)
{
    public static GameSessionId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}

public readonly record struct GamePlayerId(Guid Value)
{
    public static GamePlayerId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
