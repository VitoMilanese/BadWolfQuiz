namespace BadWolfQuiz.Game.Runtime;

public sealed record AnonymousSharedWagerState(
    int SourceQuestionId,
    GamePlayerId AnsweringPlayerId,
    IReadOnlyList<GamePlayerId> ParticipantIds,
    IReadOnlyList<AnonymousSharedWagerChoice> Choices,
    DateTimeOffset StartedAtUtc)
{
    public bool IsComplete => Choices.Count == ParticipantIds.Count;

    public bool HasSubmitted(GamePlayerId playerId) =>
        Choices.Any(choice => choice.PlayerId == playerId);
}

public static class AnonymousSharedWagerLifecycle
{
    public static AnonymousSharedWagerState Start(
        int sourceQuestionId,
        GamePlayerId answeringPlayerId,
        IEnumerable<GamePlayerId> currentPlayerIds,
        DateTimeOffset startedAtUtc)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceQuestionId);
        ArgumentNullException.ThrowIfNull(currentPlayerIds);

        var participants = currentPlayerIds
            .Where(playerId => playerId != answeringPlayerId)
            .Distinct()
            .ToArray();

        return new AnonymousSharedWagerState(
            sourceQuestionId,
            answeringPlayerId,
            participants,
            [],
            startedAtUtc);
    }

    public static AnonymousSharedWagerState Submit(
        AnonymousSharedWagerState state,
        GamePlayerId playerId,
        int percentage,
        DateTimeOffset submittedAtUtc,
        bool isForced = false)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (!state.ParticipantIds.Contains(playerId))
        {
            throw new GameRuleViolationException(
                "This player is not a participant in the current anonymous shared wager.");
        }

        if (state.HasSubmitted(playerId))
        {
            throw new GameRuleViolationException(
                "This player has already submitted an anonymous shared wager contribution.");
        }

        if (!AnonymousSharedWagerCalculator.AllowedPercentages.Contains(percentage))
        {
            throw new GameRuleViolationException(
                "An anonymous shared wager contribution must be 0%, 25%, 50%, 75%, or 100%.");
        }

        return state with
        {
            Choices = [
                .. state.Choices,
                new AnonymousSharedWagerChoice(
                    playerId,
                    percentage,
                    submittedAtUtc,
                    isForced)
            ]
        };
    }

    public static AnonymousSharedWagerState ForceMissingAsFullStake(
        AnonymousSharedWagerState state,
        GamePlayerId playerId,
        DateTimeOffset submittedAtUtc) =>
        Submit(state, playerId, 100, submittedAtUtc, isForced: true);

    public static AnonymousSharedWagerCalculation Complete(
        AnonymousSharedWagerState state,
        int questionValue)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (!state.IsComplete)
        {
            throw new GameRuleViolationException(
                "Every anonymous shared wager participant must submit or be resolved before the wager can complete.");
        }

        // Keep participant order stable so any rounding overflow is resolved
        // deterministically and recovery produces the same exact stakes.
        var choicesByPlayer = state.Choices.ToDictionary(choice => choice.PlayerId);
        var orderedChoices = state.ParticipantIds
            .Select(playerId => choicesByPlayer[playerId])
            .ToArray();

        return AnonymousSharedWagerCalculator.Calculate(questionValue, orderedChoices);
    }
}
