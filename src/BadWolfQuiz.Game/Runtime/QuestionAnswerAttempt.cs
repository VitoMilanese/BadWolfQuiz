namespace BadWolfQuiz.Game.Runtime;

public enum AnswerRewardModifier
{
    Normal = 0,
    Double = 1,
    Half = 2
}

public sealed record QuestionAnswerAttempt(
    GamePlayerId PlayerId,
    bool IsCorrect,
    int ScoreDelta,
    DateTimeOffset JudgedAtUtc,
    AnswerRewardModifier RewardModifier = AnswerRewardModifier.Normal)
{
    public Guid Id { get; init; } = Guid.NewGuid();
}
