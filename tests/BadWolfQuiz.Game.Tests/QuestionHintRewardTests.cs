using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Game.Runtime;

namespace BadWolfQuiz.Game.Tests;

public sealed class QuestionHintRewardTests
{
    public static TheoryData<int, int, decimal, int> RewardCases => new()
    {
        { 4, 1, 12.5m, 88 },
        { 4, 2, 25m, 75 },
        { 4, 3, 37.5m, 63 },
        { 4, 4, 50m, 50 },
        { 3, 1, 16.67m, 83 },
        { 3, 2, 33.34m, 67 },
        { 3, 3, 50m, 50 },
        { 2, 1, 25m, 75 },
        { 2, 2, 50m, 50 },
        { 1, 1, 50m, 50 }
    };

    [Theory]
    [MemberData(nameof(RewardCases))]
    public void Revealed_hints_reduce_reward_from_original_value(
        int totalHintCount,
        int revealedHintCount,
        decimal expectedDiscount,
        int expectedReward)
    {
        var session = CreateSession();
        session.AddPlayer("Rose");
        session.Start();
        var question = session.SelectQuestion(100);

        for (var index = 0; index < revealedHintCount; index++)
        {
            question.RevealNextHint(totalHintCount);
        }

        Assert.Equal(totalHintCount, question.HintCount);
        Assert.Equal(revealedHintCount, question.RevealedHintCount);
        Assert.Equal(expectedDiscount, question.HintRewardDiscountPercentage);
        Assert.Equal(expectedReward, question.HintRewardValue);
        Assert.Equal(expectedReward, question.CorrectAnswerValue);
    }

    [Fact]
    public void Reveal_all_hints_applies_the_full_discount()
    {
        var session = CreateSession();
        session.AddPlayer("Rose");
        session.Start();
        var question = session.SelectQuestion(100);

        question.RevealNextHint(4);
        question.RevealAllHints(4);

        Assert.Equal(4, question.HintCount);
        Assert.Equal(4, question.RevealedHintCount);
        Assert.Equal(50m, question.HintRewardDiscountPercentage);
        Assert.Equal(50, question.CorrectAnswerValue);
    }

    [Fact]
    public void Revealing_hints_while_buzzer_is_claimed_keeps_the_player_answering()
    {
        var session = CreateSession();
        var rose = session.AddPlayer("Rose");
        session.Start();
        var question = session.SelectQuestion(100);
        session.ActivateQuestionBuzzer(100);
        session.ClaimQuestionBuzzer(100, rose.Id);

        question.RevealNextHint(4);

        Assert.Equal(QuestionBuzzerStatus.Claimed, question.BuzzerStatus);
        Assert.Equal(rose.Id, question.AnsweringPlayerId);
        Assert.Equal(RuntimeQuestionStatus.Active, question.Status);

        question.RevealAllHints(4);

        Assert.Equal(QuestionBuzzerStatus.Claimed, question.BuzzerStatus);
        Assert.Equal(rose.Id, question.AnsweringPlayerId);
        Assert.Equal(RuntimeQuestionStatus.Active, question.Status);
        Assert.Equal(4, question.RevealedHintCount);
    }

    [Fact]
    public void Hint_reveal_state_survives_session_restore()
    {
        var session = CreateSession();
        session.AddPlayer("Rose");
        session.Start();
        var question = session.SelectQuestion(100);
        question.RevealNextHint(4);
        question.RevealNextHint(4);

        var restored = GameSession.Restore(
            session.Quiz,
            session.Settings,
            session.CaptureState());
        var restoredQuestion = Assert.Single(restored.Board.Questions);

        Assert.Equal(4, restoredQuestion.HintCount);
        Assert.Equal(2, restoredQuestion.RevealedHintCount);
        Assert.Equal(25m, restoredQuestion.HintRewardDiscountPercentage);
        Assert.Equal(75, restoredQuestion.CorrectAnswerValue);
    }

    [Fact]
    public void Hints_cannot_be_revealed_for_wager_question()
    {
        var quiz = new QuizSnapshot(
            1,
            "Hints",
            [new QuizRoundSnapshot(1, "Round", 0, [CreateQuestion(isSpecial: true)])]);
        var session = GameSession.Create(quiz);
        session.AddPlayer("Rose");
        session.Start();
        var question = session.SelectQuestion(100);

        Assert.Throws<GameRuleViolationException>(() =>
            question.RevealNextHint(2));
        Assert.Throws<GameRuleViolationException>(() =>
            question.RevealAllHints(2));
    }

    private static GameSession CreateSession()
    {
        var quiz = new QuizSnapshot(
            1,
            "Hints",
            [new QuizRoundSnapshot(1, "Round", 0, [CreateQuestion()])]);
        return GameSession.Create(quiz);
    }

    private static QuizQuestionSnapshot CreateQuestion(bool isSpecial = false) => new(
        100,
        10,
        0,
        100,
        isSpecial,
        "Category",
        questionBlocks: [TextBlock(1, "Question")],
        answerBlocks: [TextBlock(2, "Answer")],
        presentationType: QuestionPresentationType.Standard);

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
