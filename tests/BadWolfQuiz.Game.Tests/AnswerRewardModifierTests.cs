using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Game.Runtime;

namespace BadWolfQuiz.Game.Tests;

public sealed class AnswerRewardModifierTests
{
    [Theory]
    [InlineData(AnswerRewardModifier.Double, 200)]
    [InlineData(AnswerRewardModifier.Half, 50)]
    public void Correct_answer_reward_modifier_is_explicit_and_survives_recovery(
        AnswerRewardModifier modifier,
        int expectedScore)
    {
        var clue = new ContentBlockSnapshot(
            1, ContentBlockKind.Text, "Question", null, null, null,
            null, null, null, null, 0, false);
        var answer = new ContentBlockSnapshot(
            2, ContentBlockKind.Text, "Answer", null, null, null,
            null, null, null, null, 0, false);
        var question = new QuizQuestionSnapshot(
            100, 10, 0, 100, false, "General", false,
            [clue], [answer], QuestionPresentationType.Standard);
        var quiz = new QuizSnapshot(
            1,
            "Reward modifiers",
            [new QuizRoundSnapshot(1, "Round 1", 0, [question])]);
        var session = GameSession.Create(quiz);
        var player = session.AddPlayer("Wolf");
        session.Start();
        session.SelectQuestion(100);
        session.ActivateQuestionBuzzer(100);
        session.ClaimQuestionBuzzer(100, player.Id);

        var attempt = session.JudgeQuestionAnswer(100, player.Id, true, modifier);
        var restored = GameSession.Restore(
            session.Quiz,
            session.Settings,
            session.CaptureState());
        var restoredAttempt = restored.Board.Questions.Single().AnswerAttempts.Single();

        Assert.Equal(expectedScore, attempt.ScoreDelta);
        Assert.Equal(expectedScore, player.Score);
        Assert.Equal(modifier, attempt.RewardModifier);
        Assert.Equal(modifier, restoredAttempt.RewardModifier);
    }
}
