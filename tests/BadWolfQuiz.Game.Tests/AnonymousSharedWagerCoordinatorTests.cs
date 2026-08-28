using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Game.Runtime;

namespace BadWolfQuiz.Game.Tests;

public sealed class AnonymousSharedWagerCoordinatorTests
{
    [Theory]
    [InlineData(true, 113, -50, -25, 0, -38)]
    [InlineData(false, -113, 50, 25, 0, 38)]
    public void Settlement_is_zero_sum_for_correct_and_incorrect_answers(
        bool isCorrect,
        int answeringDelta,
        int funder1Delta,
        int funder2Delta,
        int funder3Delta,
        int funder4Delta)
    {
        var session = CreateSession(200);
        var answering = session.AddPlayer("Answering");
        var funders = new[]
        {
            session.AddPlayer("Funder 1"),
            session.AddPlayer("Funder 2"),
            session.AddPlayer("Funder 3"),
            session.AddPlayer("Funder 4")
        };
        foreach (var player in session.Players)
        {
            session.AdjustPlayerScore(player.Id, 100);
        }

        session.Start();
        session.ChangeActivePlayer(answering.Id);
        session.SelectQuestion(100);

        var now = DateTimeOffset.UtcNow;
        var state = AnonymousSharedWagerCoordinator.Start(session, 100, now);
        state = AnonymousSharedWagerLifecycle.Submit(state, funders[0].Id, 100, now);
        state = AnonymousSharedWagerLifecycle.Submit(state, funders[1].Id, 50, now);
        state = AnonymousSharedWagerLifecycle.Submit(state, funders[2].Id, 0, now);
        state = AnonymousSharedWagerLifecycle.Submit(state, funders[3].Id, 75, now);
        var calculation = AnonymousSharedWagerCoordinator.ActivateQuestion(
            session,
            state,
            now);

        Assert.Equal(113, calculation.CombinedWager);
        Assert.Equal(113, session.Board.Questions.Single().Wager!.Amount);

        var before = session.Players.ToDictionary(player => player.Id, player => player.Score);
        var attempt = AnonymousSharedWagerCoordinator.SettleAnswer(
            session,
            state,
            isCorrect);

        Assert.Equal(answeringDelta, attempt.ScoreDelta);
        Assert.Equal(answeringDelta, answering.Score - before[answering.Id]);
        Assert.Equal(funder1Delta, funders[0].Score - before[funders[0].Id]);
        Assert.Equal(funder2Delta, funders[1].Score - before[funders[1].Id]);
        Assert.Equal(funder3Delta, funders[2].Score - before[funders[2].Id]);
        Assert.Equal(funder4Delta, funders[3].Score - before[funders[3].Id]);
        Assert.Equal(0, session.Players.Sum(player => player.Score) - before.Values.Sum());
    }

    [Fact]
    public void Removed_missing_funder_is_forced_to_full_stake_and_still_settles()
    {
        var session = CreateSession(200);
        var answering = session.AddPlayer("Answering");
        var funder = session.AddPlayer("Funder");
        session.AdjustPlayerScore(funder.Id, 100);
        session.Start();
        session.ChangeActivePlayer(answering.Id);
        session.SelectQuestion(100);

        var now = DateTimeOffset.UtcNow;
        var state = AnonymousSharedWagerCoordinator.Start(session, 100, now);
        session.RemovePlayer(funder.Id);
        state = AnonymousSharedWagerCoordinator.ResolveRemovedParticipants(
            session,
            state,
            now);

        Assert.True(state.IsComplete);
        Assert.True(state.Choices.Single().IsForced);
        Assert.Equal(100, state.Choices.Single().Percentage);

        AnonymousSharedWagerCoordinator.ActivateQuestion(session, state, now);
        AnonymousSharedWagerCoordinator.SettleAnswer(session, state, isCorrect: false);

        Assert.Equal(-200, answering.Score);
        Assert.Equal(300, session.RemovedPlayers.Single().Score);
    }

    [Fact]
    public void Single_player_game_activates_with_zero_shared_wager()
    {
        var session = CreateSession(200);
        var answering = session.AddPlayer("Solo");
        session.Start();
        session.ChangeActivePlayer(answering.Id);
        session.SelectQuestion(100);

        var state = AnonymousSharedWagerCoordinator.Start(
            session,
            100,
            DateTimeOffset.UtcNow);
        var result = AnonymousSharedWagerCoordinator.ActivateQuestion(
            session,
            state,
            DateTimeOffset.UtcNow);

        Assert.Equal(0, result.CombinedWager);
        Assert.Equal(RuntimeQuestionStatus.Active, session.Board.Questions.Single().Status);
    }

    private static GameSession CreateSession(int points)
    {
        var question = new QuizQuestionSnapshot(
            100,
            10,
            0,
            points,
            isSpecial: true,
            categoryTitle: "Category",
            questionBlocks:
            [
                new ContentBlockSnapshot(
                    1,
                    ContentBlockKind.Text,
                    "Question",
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    0,
                    false)
            ],
            presentationType: QuestionWagerModes.AnonymousShared);

        return GameSession.Create(new QuizSnapshot(
            1,
            "Anonymous shared wager",
            [new QuizRoundSnapshot(1, "Round", 0, [question])]));
    }
}
