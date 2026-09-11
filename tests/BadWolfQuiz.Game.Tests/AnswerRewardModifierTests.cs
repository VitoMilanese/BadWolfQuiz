using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Game.Runtime;

namespace BadWolfQuiz.Game.Tests;

public sealed class AnswerRewardModifierTests
{
    [Theory]
    [InlineData(100, AnswerRewardModifier.Double, 200)]
    [InlineData(100, AnswerRewardModifier.Half, 50)]
    [InlineData(101, AnswerRewardModifier.Half, 51)]
    [InlineData(1, AnswerRewardModifier.Half, 1)]
    public void Enabled_correct_answer_modifier_is_explicit_and_survives_recovery(
        int points,
        AnswerRewardModifier modifier,
        int expectedScore)
    {
        var session = CreateSession(points, allowAnswerRewardModifiers: true);
        var player = session.AddPlayer("Wolf");
        StartAnswering(session, player);

        var attempt = session.JudgeQuestionAnswer(100, player.Id, true, modifier);
        var restored = GameSession.Restore(
            session.Quiz,
            session.Settings,
            session.CaptureState());
        var restoredQuestion = restored.Board.Questions.Single();
        var restoredAttempt = restoredQuestion.AnswerAttempts.Single();

        Assert.Equal(expectedScore, attempt.ScoreDelta);
        Assert.Equal(expectedScore, player.Score);
        Assert.Equal(points, session.Board.Questions.Single().Points);
        Assert.Equal(modifier, attempt.RewardModifier);
        Assert.Equal(modifier, restoredAttempt.RewardModifier);
        Assert.True(restoredQuestion.AllowAnswerRewardModifiers);
    }

    [Fact]
    public void Disabled_question_rejects_non_normal_correct_reward_modifier_without_score_change()
    {
        var session = CreateSession(100, allowAnswerRewardModifiers: false);
        var player = session.AddPlayer("Wolf");
        StartAnswering(session, player);

        Assert.Throws<GameRuleViolationException>(() =>
            session.JudgeQuestionAnswer(
                100,
                player.Id,
                true,
                AnswerRewardModifier.Double));

        Assert.Equal(0, player.Score);
        Assert.Empty(session.Board.Questions.Single().AnswerAttempts);
    }

    [Fact]
    public void Incorrect_answer_ignores_reward_modifier_and_keeps_normal_penalty()
    {
        var session = CreateSession(101, allowAnswerRewardModifiers: true);
        var player = session.AddPlayer("Wolf");
        StartAnswering(session, player);

        var attempt = session.JudgeQuestionAnswer(
            100,
            player.Id,
            false,
            AnswerRewardModifier.Double);

        Assert.Equal(-101, attempt.ScoreDelta);
        Assert.Equal(-101, player.Score);
        Assert.Equal(AnswerRewardModifier.Normal, attempt.RewardModifier);
    }

    [Fact]
    public void Modified_reward_cannot_be_applied_twice_to_the_same_answer()
    {
        var session = CreateSession(100, allowAnswerRewardModifiers: true);
        var player = session.AddPlayer("Wolf");
        StartAnswering(session, player);

        session.JudgeQuestionAnswer(
            100,
            player.Id,
            true,
            AnswerRewardModifier.Double);

        Assert.Throws<GameRuleViolationException>(() =>
            session.JudgeQuestionAnswer(
                100,
                player.Id,
                true,
                AnswerRewardModifier.Double));
        Assert.Equal(200, player.Score);
        Assert.Single(session.Board.Questions.Single().AnswerAttempts);
    }

    private static GameSession CreateSession(
        int points,
        bool allowAnswerRewardModifiers)
    {
        var clue = new ContentBlockSnapshot(
            1, ContentBlockKind.Text, "Question", null, null, null,
            null, null, null, null, 0, false);
        var answer = new ContentBlockSnapshot(
            2, ContentBlockKind.Text, "Answer", null, null, null,
            null, null, null, null, 0, false);
        var question = new QuizQuestionSnapshot(
            100, 10, 0, points, false, "General", false,
            [clue], [answer], QuestionPresentationType.Standard,
            allowAnswerRewardModifiers: allowAnswerRewardModifiers);
        var quiz = new QuizSnapshot(
            1,
            "Reward modifiers",
            [new QuizRoundSnapshot(1, "Round 1", 0, [question])]);
        return GameSession.Create(quiz);
    }

    private static void StartAnswering(GameSession session, GamePlayer player)
    {
        session.Start();
        session.SelectQuestion(100);
        session.ActivateQuestionBuzzer(100);
        session.ClaimQuestionBuzzer(100, player.Id);
    }
}
