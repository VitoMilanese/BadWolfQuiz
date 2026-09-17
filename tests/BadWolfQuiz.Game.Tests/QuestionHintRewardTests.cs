using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Game.Runtime;

namespace BadWolfQuiz.Game.Tests;

public sealed class QuestionHintRewardTests
{
    public static TheoryData<int, int, int, int> RewardCases => new()
    {
        { 4, 1, 12, 88 },
        { 4, 2, 25, 75 },
        { 4, 3, 37, 63 },
        { 4, 4, 50, 50 },
        { 3, 1, 16, 84 },
        { 3, 2, 33, 67 },
        { 3, 3, 50, 50 },
        { 2, 1, 25, 75 },
        { 2, 2, 50, 50 },
        { 1, 1, 50, 50 }
    };

    [Theory]
    [MemberData(nameof(RewardCases))]
    public void Revealed_hints_reduce_reward_from_original_value(
        int totalHintCount,
        int revealedHintCount,
        int expectedDiscount,
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
        Assert.Equal(25, restoredQuestion.HintRewardDiscountPercentage);
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
