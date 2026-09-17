using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Game.Runtime;

namespace BadWolfQuiz.Game.Tests;

public sealed class OnDemandAllPlayerMultipleChoiceTests
{
    [Fact]
    public void Snapshot_forces_on_demand_choice_out_of_every_wager_mode()
    {
        var question = CreateQuestion(isSpecial: true);

        Assert.Equal(
            QuestionPresentationType.AllPlayerMultipleChoiceOnDemand,
            question.PresentationType);
        Assert.False(question.IsSpecial);
        Assert.True(question.ExcludeFromRandomWagerSelection);
        Assert.False(question.IsEligibleForRandomWagerSelection);
        Assert.False(question.IsEligibleForRandomAnonymousSharedWagerSelection);
    }

    [Fact]
    public void Snapshot_preserves_reward_modifiers_for_on_demand_choice()
    {
        var question = CreateQuestion(allowAnswerRewardModifiers: true);

        Assert.True(question.AllowAnswerRewardModifiers);
    }

    [Fact]
    public void Question_starts_with_regular_buzzer_and_reveal_halves_value()
    {
        var session = CreateSession();
        session.AddPlayer("Rose");
        session.Start();

        var question = session.SelectQuestion(100);

        Assert.True(question.RevealAnswerOptionsOnDemand);
        Assert.False(question.AreAllPlayerChoiceOptionsRevealed);
        Assert.False(question.IsAllPlayerQuestion);
        Assert.True(question.CanRevealAllPlayerChoiceOptions);
        Assert.Equal(200, question.CorrectAnswerValue);

        if (question.BuzzerStatus == QuestionBuzzerStatus.Inactive)
        {
            session.ActivateQuestionBuzzer(100);
        }

        session.RevealAllPlayerChoiceOptions(100);

        Assert.True(question.AreAllPlayerChoiceOptionsRevealed);
        Assert.True(question.IsAllPlayerQuestion);
        Assert.False(question.CanRevealAllPlayerChoiceOptions);
        Assert.Empty(question.AllPlayerChoiceExcludedPlayerIds);
        Assert.Equal(100, question.CorrectAnswerValue);
        Assert.Equal(QuestionBuzzerStatus.Closed, question.BuzzerStatus);
        Assert.Equal(GameTimerStatus.Running, session.Timer.Status);
        Assert.Equal(GameTimerStatus.Stopped, session.AnswerTimer.Status);
    }

    [Fact]
    public void Current_buzzer_claim_does_not_block_reveal_and_claimant_remains_eligible()
    {
        var session = CreateSession();
        var rose = session.AddPlayer("Rose");
        session.AddPlayer("Jack");
        session.Start();
        var question = session.SelectQuestion(100);
        if (question.BuzzerStatus == QuestionBuzzerStatus.Inactive)
        {
            session.ActivateQuestionBuzzer(100);
        }

        session.ClaimQuestionBuzzer(100, rose.Id);

        Assert.True(question.CanRevealAllPlayerChoiceOptions);
        session.RevealAllPlayerChoiceOptions(100);

        Assert.DoesNotContain(rose.Id, question.AllPlayerChoiceExcludedPlayerIds);
        Assert.Null(question.AnsweringPlayerId);
        Assert.Equal(QuestionBuzzerStatus.Closed, question.BuzzerStatus);
        Assert.Equal(100, question.CorrectAnswerValue);
    }

    [Fact]
    public void Earlier_buzzer_attempts_are_excluded_when_options_are_revealed()
    {
        var session = CreateSession();
        var rose = session.AddPlayer("Rose");
        var jack = session.AddPlayer("Jack");
        session.Start();
        var question = session.SelectQuestion(100);
        if (question.BuzzerStatus == QuestionBuzzerStatus.Inactive)
        {
            session.ActivateQuestionBuzzer(100);
        }

        session.ClaimQuestionBuzzer(100, rose.Id);
        session.JudgeQuestionAnswer(100, rose.Id, false);

        Assert.True(question.CanRevealAllPlayerChoiceOptions);
        session.RevealAllPlayerChoiceOptions(100);

        Assert.Contains(rose.Id, question.AllPlayerChoiceExcludedPlayerIds);
        Assert.DoesNotContain(jack.Id, question.AllPlayerChoiceExcludedPlayerIds);
    }

    [Fact]
    public void Revealed_phase_and_exclusions_survive_session_state_restore()
    {
        var session = CreateSession();
        var rose = session.AddPlayer("Rose");
        session.AddPlayer("Jack");
        session.Start();
        var question = session.SelectQuestion(100);
        if (question.BuzzerStatus == QuestionBuzzerStatus.Inactive)
        {
            session.ActivateQuestionBuzzer(100);
        }
        session.ClaimQuestionBuzzer(100, rose.Id);
        session.JudgeQuestionAnswer(100, rose.Id, false);
        session.RevealAllPlayerChoiceOptions(100);

        var restored = GameSession.Restore(
            session.Quiz,
            session.Settings,
            session.CaptureState());
        var restoredQuestion = Assert.Single(restored.Board.Questions);

        Assert.True(restoredQuestion.RevealAnswerOptionsOnDemand);
        Assert.True(restoredQuestion.AreAllPlayerChoiceOptionsRevealed);
        Assert.True(restoredQuestion.IsAllPlayerQuestion);
        Assert.Contains(rose.Id, restoredQuestion.AllPlayerChoiceExcludedPlayerIds);
        Assert.Equal(100, restoredQuestion.CorrectAnswerValue);
        Assert.Equal(QuestionBuzzerStatus.Closed, restoredQuestion.BuzzerStatus);
    }

    private static GameSession CreateSession()
    {
        var quiz = new QuizSnapshot(
            1,
            "On-demand choice",
            [new QuizRoundSnapshot(1, "Round", 0, [CreateQuestion()])]);
        return GameSession.Create(quiz);
    }

    private static QuizQuestionSnapshot CreateQuestion(
        bool isSpecial = false,
        bool allowAnswerRewardModifiers = false) => new(
        100,
        10,
        0,
        200,
        isSpecial,
        "Category",
        excludeFromRandomWagerSelection: false,
        questionBlocks: [TextBlock(1, "Question")],
        answerBlocks: [TextBlock(10, "A"), TextBlock(11, "B")],
        presentationType: QuestionPresentationType.AllPlayerMultipleChoiceOnDemand,
        allowAnswerRewardModifiers: allowAnswerRewardModifiers);

    private static ContentBlockSnapshot TextBlock(int id, string text) => new(
        id,
        ContentBlockKind.Text,
        text,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        id,
        false);
}
