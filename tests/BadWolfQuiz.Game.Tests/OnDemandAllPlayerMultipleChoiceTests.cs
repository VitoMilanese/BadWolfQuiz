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
    public void Question_starts_with_regular_buzzer_and_can_switch_to_all_player_choice()
    {
        var session = CreateSession();
        session.AddPlayer("Rose");
        session.Start();

        var question = session.SelectQuestion(100);

        Assert.True(question.RevealAnswerOptionsOnDemand);
        Assert.False(question.AreAllPlayerChoiceOptionsRevealed);
        Assert.False(question.IsAllPlayerQuestion);
        Assert.True(question.CanRevealAllPlayerChoiceOptions);

        if (question.BuzzerStatus == QuestionBuzzerStatus.Inactive)
        {
            session.ActivateQuestionBuzzer(100);
        }
        Assert.Equal(QuestionBuzzerStatus.Open, question.BuzzerStatus);

        var revealed = session.RevealAllPlayerChoiceOptions(100);

        Assert.Same(question, revealed);
        Assert.True(question.AreAllPlayerChoiceOptionsRevealed);
        Assert.True(question.IsAllPlayerQuestion);
        Assert.False(question.CanRevealAllPlayerChoiceOptions);
        Assert.Equal(QuestionBuzzerStatus.Closed, question.BuzzerStatus);
        Assert.Null(question.AnsweringPlayerId);
        Assert.Equal(RuntimeQuestionStatus.Active, question.Status);
        Assert.Equal(GameTimerStatus.Running, session.Timer.Status);
        Assert.Equal(GameTimerStatus.Stopped, session.AnswerTimer.Status);
    }

    [Fact]
    public void First_buzzer_claim_permanently_blocks_option_reveal()
    {
        var session = CreateSession();
        var rose = session.AddPlayer("Rose");
        session.Start();
        var question = session.SelectQuestion(100);
        if (question.BuzzerStatus == QuestionBuzzerStatus.Inactive)
        {
            session.ActivateQuestionBuzzer(100);
        }

        session.ClaimQuestionBuzzer(100, rose.Id);

        Assert.True(question.AllPlayerChoiceRevealLocked);
        Assert.False(question.CanRevealAllPlayerChoiceOptions);
        Assert.Throws<GameRuleViolationException>(() =>
            session.RevealAllPlayerChoiceOptions(100));
    }

    [Fact]
    public void Revealed_phase_survives_session_state_restore()
    {
        var session = CreateSession();
        session.AddPlayer("Rose");
        session.Start();
        session.SelectQuestion(100);
        session.RevealAllPlayerChoiceOptions(100);

        var restored = GameSession.Restore(
            session.Quiz,
            session.Settings,
            session.CaptureState());
        var question = Assert.Single(restored.Board.Questions);

        Assert.True(question.RevealAnswerOptionsOnDemand);
        Assert.True(question.AreAllPlayerChoiceOptionsRevealed);
        Assert.True(question.IsAllPlayerQuestion);
        Assert.Equal(QuestionBuzzerStatus.Closed, question.BuzzerStatus);
    }

    [Fact]
    public void Buzzer_claim_lock_survives_session_state_restore()
    {
        var session = CreateSession();
        var rose = session.AddPlayer("Rose");
        session.Start();
        var question = session.SelectQuestion(100);
        if (question.BuzzerStatus == QuestionBuzzerStatus.Inactive)
        {
            session.ActivateQuestionBuzzer(100);
        }
        session.ClaimQuestionBuzzer(100, rose.Id);

        var restored = GameSession.Restore(
            session.Quiz,
            session.Settings,
            session.CaptureState());
        var restoredQuestion = Assert.Single(restored.Board.Questions);

        Assert.True(restoredQuestion.AllPlayerChoiceRevealLocked);
        Assert.False(restoredQuestion.CanRevealAllPlayerChoiceOptions);
    }

    private static GameSession CreateSession()
    {
        var quiz = new QuizSnapshot(
            1,
            "On-demand choice",
            [new QuizRoundSnapshot(1, "Round", 0, [CreateQuestion()])]);
        return GameSession.Create(quiz);
    }

    private static QuizQuestionSnapshot CreateQuestion(bool isSpecial = false) => new(
        100,
        10,
        0,
        200,
        isSpecial,
        "Category",
        excludeFromRandomWagerSelection: false,
        questionBlocks: [TextBlock(1, "Question")],
        answerBlocks: [TextBlock(10, "A"), TextBlock(11, "B")],
        presentationType: QuestionPresentationType.AllPlayerMultipleChoiceOnDemand);

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
