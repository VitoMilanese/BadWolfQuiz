namespace BadWolfQuiz.Game.Runtime;

public sealed record QuestionAnswerAttempt(
    GamePlayerId PlayerId,
    bool IsCorrect,
    int ScoreDelta,
    DateTimeOffset JudgedAtUtc)
{
    public Guid Id { get; init; } = Guid.NewGuid();
}
