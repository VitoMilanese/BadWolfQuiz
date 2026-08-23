using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Game.Runtime;

namespace BadWolfQuiz.Game.Tests;

public sealed class FinalQuestionCompletionRegressionTests
{
    [Theory]
    [InlineData(true, 100)]
    [InlineData(false, -100)]
    public void Forced_final_with_unfinished_regular_questions_can_complete_and_show_standings(
        bool isCorrect,
        int expectedScore)
    {
        var session = GameSession.Create(CreateQuiz());
        var player = session.AddPlayer("Rose");
        session.Start();

        session.ForceAdvanceToFinalQuestion();
        session.SubmitFinalWager(player.Id, 100);
        session.LockFinalWagers();
        session.SubmitFinalAnswer(player.Id, "Bad Wolf");
        session.LockFinalAnswers();
        session.JudgeFinalAnswer(player.Id, isCorrect);

        Assert.Equal(GameSessionStatus.FinalJudging, session.Status);
        Assert.Equal(
            FinalQuestionStatus.AnswerPresentation,
            session.FinalQuestion!.Status);
        Assert.Throws<GameRuleViolationException>(() => session.GetFinalStandings());
        Assert.True(session.HasAnyUnfinishedRegularRound);
        Assert.Equal(expectedScore, player.Score);

        session.CompleteFinalQuestion();
        var standings = session.GetFinalStandings();

        Assert.Equal(GameSessionStatus.Completed, session.Status);
        Assert.Equal(FinalQuestionStatus.Completed, session.FinalQuestion.Status);
        Assert.Equal(player.Id, Assert.Single(standings).PlayerId);
    }

    [Fact]
    public void Final_answer_presentation_survives_snapshot_restore()
    {
        var session = GameSession.Create(CreateQuiz());
        var player = session.AddPlayer("Rose");
        session.Start();
        session.ForceAdvanceToFinalQuestion();
        session.SubmitFinalWager(player.Id, 100);
        session.LockFinalWagers();
        session.SubmitFinalAnswer(player.Id, "Bad Wolf");
        session.LockFinalAnswers();
        session.JudgeFinalAnswer(player.Id, true);

        var restored = GameSession.Restore(
            session.Quiz,
            session.Settings,
            session.CaptureState());

        Assert.Equal(GameSessionStatus.FinalJudging, restored.Status);
        Assert.Equal(
            FinalQuestionStatus.AnswerPresentation,
            restored.FinalQuestion!.Status);

        restored.CompleteFinalQuestion();

        Assert.Equal(GameSessionStatus.Completed, restored.Status);
        Assert.Equal(FinalQuestionStatus.Completed, restored.FinalQuestion.Status);
    }

    private static QuizSnapshot CreateQuiz() => new(
        1,
        "Final Question Regression",
        [
            new QuizRoundSnapshot(
                1,
                "Round 1",
                0,
                [new QuizQuestionSnapshot(100, 10, 0, 100, false, "Science")]),
            new QuizRoundSnapshot(
                2,
                "Round 2",
                1,
                [new QuizQuestionSnapshot(200, 20, 0, 200, false, "History")])
        ],
        new FinalQuestionSnapshot(
            [
                new ContentBlockSnapshot(
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
                    false)
            ],
            [
                new ContentBlockSnapshot(
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
                    false)
            ]));
}
