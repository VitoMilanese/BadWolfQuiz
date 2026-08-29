using BadWolfQuiz.Game.Runtime;

namespace BadWolfQuiz.Game.Tests;

public sealed class AnonymousSharedWagerCalculatorTests
{
    [Fact]
    public void Calculate_matches_issue_example()
    {
        var now = DateTimeOffset.UtcNow;
        var players = Enumerable.Range(1, 4)
            .Select(_ => GamePlayerId.New())
            .ToArray();

        var result = AnonymousSharedWagerCalculator.Calculate(
            200,
            [
                new(players[0], 100, now),
                new(players[1], 50, now),
                new(players[2], 0, now),
                new(players[3], 75, now)
            ]);

        Assert.Equal(113, result.CombinedWager);
        Assert.Equal([50, 25, 0, 38], result.Contributions.Select(item => item.Amount));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(25)]
    [InlineData(50)]
    [InlineData(75)]
    [InlineData(100)]
    public void Calculate_accepts_supported_percentages(int percentage)
    {
        var result = AnonymousSharedWagerCalculator.Calculate(
            200,
            [new(GamePlayerId.New(), percentage, DateTimeOffset.UtcNow)]);

        Assert.Single(result.Contributions);
        Assert.Equal(percentage, result.Contributions[0].Percentage);
    }

    [Fact]
    public void Calculate_rejects_unsupported_percentage()
    {
        Assert.Throws<GameRuleViolationException>(() =>
            AnonymousSharedWagerCalculator.Calculate(
                200,
                [new(GamePlayerId.New(), 10, DateTimeOffset.UtcNow)]));
    }

    [Fact]
    public void Calculate_with_no_funders_produces_zero_wager()
    {
        var result = AnonymousSharedWagerCalculator.Calculate(
            200,
            Array.Empty<AnonymousSharedWagerChoice>());

        Assert.Equal(0, result.CombinedWager);
        Assert.Empty(result.Contributions);
    }

    [Fact]
    public void Calculate_caps_rounding_overflow_and_keeps_zero_sum_inputs()
    {
        var now = DateTimeOffset.UtcNow;
        var result = AnonymousSharedWagerCalculator.Calculate(
            2,
            [
                new(GamePlayerId.New(), 100, now),
                new(GamePlayerId.New(), 100, now),
                new(GamePlayerId.New(), 100, now)
            ]);

        Assert.Equal(2, result.CombinedWager);
        Assert.Equal(2, result.Contributions.Sum(item => item.Amount));
        Assert.All(result.Contributions, item => Assert.InRange(item.Amount, 0, 1));
    }

    [Fact]
    public void Calculate_marks_forced_afk_contribution_without_changing_amount()
    {
        var result = AnonymousSharedWagerCalculator.Calculate(
            200,
            [new(GamePlayerId.New(), 100, DateTimeOffset.UtcNow, IsForced: true)]);

        Assert.True(result.Contributions[0].IsForced);
        Assert.Equal(200, result.Contributions[0].Amount);
    }

    [Fact]
    public void Calculate_rejects_duplicate_participants()
    {
        var playerId = GamePlayerId.New();
        var now = DateTimeOffset.UtcNow;

        Assert.Throws<GameRuleViolationException>(() =>
            AnonymousSharedWagerCalculator.Calculate(
                200,
                [
                    new(playerId, 25, now),
                    new(playerId, 50, now)
                ]));
    }
}
