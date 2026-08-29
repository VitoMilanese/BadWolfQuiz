using BadWolfQuiz.Game.Runtime;

namespace BadWolfQuiz.Game.Tests;

public sealed class AnonymousSharedWagerLifecycleTests
{
    [Fact]
    public void Start_excludes_answering_player_and_late_join_does_not_change_participants()
    {
        var answering = GamePlayerId.New();
        var funder1 = GamePlayerId.New();
        var funder2 = GamePlayerId.New();

        var state = AnonymousSharedWagerLifecycle.Start(
            10,
            answering,
            [answering, funder1, funder2],
            DateTimeOffset.UtcNow);

        Assert.Equal([funder1, funder2], state.ParticipantIds);
        Assert.DoesNotContain(answering, state.ParticipantIds);
    }

    [Fact]
    public void Submit_locks_player_choice()
    {
        var answering = GamePlayerId.New();
        var funder = GamePlayerId.New();
        var state = AnonymousSharedWagerLifecycle.Start(
            10,
            answering,
            [answering, funder],
            DateTimeOffset.UtcNow);

        state = AnonymousSharedWagerLifecycle.Submit(
            state,
            funder,
            75,
            DateTimeOffset.UtcNow);

        Assert.True(state.HasSubmitted(funder));
        Assert.Throws<GameRuleViolationException>(() =>
            AnonymousSharedWagerLifecycle.Submit(
                state,
                funder,
                25,
                DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Force_missing_submission_uses_full_stake()
    {
        var answering = GamePlayerId.New();
        var funder = GamePlayerId.New();
        var state = AnonymousSharedWagerLifecycle.Start(
            10,
            answering,
            [answering, funder],
            DateTimeOffset.UtcNow);

        state = AnonymousSharedWagerLifecycle.ForceMissingAsFullStake(
            state,
            funder,
            DateTimeOffset.UtcNow);

        Assert.True(state.IsComplete);
        Assert.Equal(100, state.Choices[0].Percentage);
        Assert.True(state.Choices[0].IsForced);
    }

    [Fact]
    public void Complete_preserves_captured_participant_order_and_issue_example()
    {
        var now = DateTimeOffset.UtcNow;
        var answering = GamePlayerId.New();
        var funders = Enumerable.Range(0, 4)
            .Select(_ => GamePlayerId.New())
            .ToArray();
        var state = AnonymousSharedWagerLifecycle.Start(
            10,
            answering,
            [answering, .. funders],
            now);

        state = AnonymousSharedWagerLifecycle.Submit(state, funders[2], 0, now);
        state = AnonymousSharedWagerLifecycle.Submit(state, funders[0], 100, now);
        state = AnonymousSharedWagerLifecycle.Submit(state, funders[3], 75, now);
        state = AnonymousSharedWagerLifecycle.Submit(state, funders[1], 50, now);

        var result = AnonymousSharedWagerLifecycle.Complete(state, 200);

        Assert.Equal(113, result.CombinedWager);
        Assert.Equal(funders, result.Contributions.Select(item => item.PlayerId));
        Assert.Equal([50, 25, 0, 38], result.Contributions.Select(item => item.Amount));
    }

    [Fact]
    public void Empty_participant_set_completes_immediately_with_zero_wager()
    {
        var answering = GamePlayerId.New();
        var state = AnonymousSharedWagerLifecycle.Start(
            10,
            answering,
            [answering],
            DateTimeOffset.UtcNow);

        Assert.True(state.IsComplete);
        Assert.Equal(0, AnonymousSharedWagerLifecycle.Complete(state, 200).CombinedWager);
    }
}
