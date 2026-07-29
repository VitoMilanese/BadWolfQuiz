namespace BadWolfQuiz.Game.Runtime;

public sealed class GameRuleViolationException : InvalidOperationException
{
    public GameRuleViolationException(string message)
        : base(message)
    {
    }
}
