namespace BadWolfQuiz.Game.Runtime;

public static class AnonymousSharedWagerCalculator
{
    public static readonly IReadOnlyList<int> AllowedPercentages = [0, 25, 50, 75, 100];

    public static AnonymousSharedWagerCalculation Calculate(
        int questionValue,
        IReadOnlyList<AnonymousSharedWagerChoice> choices)
    {
        if (questionValue < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(questionValue));
        }

        ArgumentNullException.ThrowIfNull(choices);

        if (choices.Count == 0)
        {
            return new AnonymousSharedWagerCalculation(questionValue, 0, []);
        }

        if (choices.Select(choice => choice.PlayerId).Distinct().Count() != choices.Count)
        {
            throw new GameRuleViolationException(
                "Each anonymous shared wager participant can submit only one contribution.");
        }

        var share = (decimal)questionValue / choices.Count;
        var contributions = choices
            .Select(choice => new AnonymousSharedWagerContribution(
                choice.PlayerId,
                ValidatePercentage(choice.Percentage),
                CalculateRoundedContribution(share, choice.Percentage),
                choice.SubmittedAtUtc,
                choice.IsForced))
            .ToArray();

        ClampContributionsToQuestionValue(contributions, questionValue);

        return new AnonymousSharedWagerCalculation(
            questionValue,
            contributions.Sum(contribution => contribution.Amount),
            contributions);
    }

    public static int CalculateMaximumShare(int questionValue, int participantCount)
    {
        if (questionValue < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(questionValue));
        }

        if (participantCount <= 0)
        {
            return 0;
        }

        return CalculateRoundedContribution(
            (decimal)questionValue / participantCount,
            100);
    }

    private static int ValidatePercentage(int percentage)
    {
        if (!AllowedPercentages.Contains(percentage))
        {
            throw new GameRuleViolationException(
                "An anonymous shared wager contribution must be 0%, 25%, 50%, 75%, or 100%.");
        }

        return percentage;
    }

    private static int CalculateRoundedContribution(decimal share, int percentage) =>
        checked((int)Math.Round(
            share * percentage / 100m,
            0,
            MidpointRounding.AwayFromZero));

    private static void ClampContributionsToQuestionValue(
        AnonymousSharedWagerContribution[] contributions,
        int questionValue)
    {
        var overflow = contributions.Sum(contribution => contribution.Amount) - questionValue;
        if (overflow <= 0)
        {
            return;
        }

        // Rounding every participant independently can make the sum exceed the
        // question value by a few points. Trim the overflow deterministically
        // from the last positive contributions so settlement remains zero-sum.
        for (var index = contributions.Length - 1; index >= 0 && overflow > 0; index--)
        {
            var reduction = Math.Min(contributions[index].Amount, overflow);
            contributions[index] = contributions[index] with
            {
                Amount = contributions[index].Amount - reduction
            };
            overflow -= reduction;
        }
    }
}

public sealed record AnonymousSharedWagerChoice(
    GamePlayerId PlayerId,
    int Percentage,
    DateTimeOffset SubmittedAtUtc,
    bool IsForced = false);

public sealed record AnonymousSharedWagerContribution(
    GamePlayerId PlayerId,
    int Percentage,
    int Amount,
    DateTimeOffset SubmittedAtUtc,
    bool IsForced = false);

public sealed record AnonymousSharedWagerCalculation(
    int QuestionValue,
    int CombinedWager,
    IReadOnlyList<AnonymousSharedWagerContribution> Contributions);
