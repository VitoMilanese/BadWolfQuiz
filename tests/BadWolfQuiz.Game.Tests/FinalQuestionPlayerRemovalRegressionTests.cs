using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Game.Runtime;

namespace BadWolfQuiz.Game.Tests;

public sealed class FinalQuestionPlayerRemovalRegressionTests
{
    [Fact]
    public void Removing_player_during_final_wagering_removes_required_submission()
    {
        var session = CreateFinalSession(out var rose, out var clara);
        session.SubmitFinalWager(rose.Id, 100);

        session.RemovePlayer(clara.Id);
        session.LockFinalWagers();

        Assert.Equal(GameSessionStatus.FinalAnswering, session.Status);
        Assert.DoesNotContain(
            session.FinalQuestion!.Submissions,
            submission => submission.PlayerId == clara.Id);
        Assert.Contains(clara, session.RemovedPlayers);
    }

    [Fact]
    public void Removing_player_during_final_answering_removes_required_submission()
    {
        var session = CreateFinalSession(out var rose, out var clara);
        session.SubmitFinalWager(rose.Id, 100);
        session.SubmitFinalWager(clara.Id, 100);
        session.LockFinalWagers();
        session.SubmitFinalAnswer(rose.Id, "Bad Wolf");

        session.RemovePlayer(clara.Id);
        session.LockFinalAnswers();

        Assert.Equal(GameSessionStatus.FinalJudging, session.Status);
        Assert.DoesNotContain(
            session.FinalQuestion!.Submissions,
            submission => submission.PlayerId == clara.Id);
    }

    [Fact]
    public void Removing_last_unresolved_player_during_final_judging_completes_game()
    {
        var session = CreateFinalSession(out var rose, out var clara);
        foreach (var player in new[] { rose, clara })
        {
            session.SubmitFinalWager(player.Id, 100);
        }
        session.LockFinalWagers();
        session.SubmitFinalAnswer(rose.Id, "Bad Wolf");
        session.SubmitFinalAnswer(clara.Id, "Bad Wolf");
        session.LockFinalAnswers();
        session.JudgeFinalAnswer(rose.Id, true);

        session.RemovePlayer(clara.Id);

        Assert.Equal(GameSessionStatus.Completed, session.Status);
        Assert.Equal(FinalQuestionStatus.Completed, session.FinalQuestion!.Status);
        Assert.Equal(100, rose.Score);
    }

    private static GameSession CreateFinalSession(
        out GamePlayer rose,
        out GamePlayer clara)
    {
        var session = GameSession.Create(CreateQuiz());
        rose = session.AddPlayer("Rose");
        clara = session.AddPlayer("Clara");
        session.Start();
        session.ForceAdvanceToFinalQuestion();
        return session;
    }

    private static QuizSnapshot CreateQuiz() => new(
        1,
        "Final Player Controls Regression",
        [
            new QuizRoundSnapshot(
                1,
                "Round 1",
                0,
                [new QuizQuestionSnapshot(100, 10, 0, 100, false, "Science")])
        ],
        new FinalQuestionSnapshot(
            [new ContentBlockSnapshot(
                900,
                ContentBlockKind.Text,
                "Final?",
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                0,
                false)],
            [new ContentBlockSnapshot(
                901,
                ContentBlockKind.Text,
                "Final!",
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                0,
                false)]));
}
