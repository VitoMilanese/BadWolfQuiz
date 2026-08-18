using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Game.Runtime;

namespace BadWolfQuiz.Game.Tests;

public sealed class AllPlayerQuestionTests
{
    [Fact]
    public void Text_mode_requires_one_non_empty_text_answer()
    {
        var question = CreateQuestion(
            QuestionPresentationType.AllPlayerText,
            [TextBlock(10, "Kyiv")]);

        Assert.False(question.IsSpecial);
        Assert.True(question.ExcludeFromRandomWagerSelection);
        Assert.Single(question.AnswerBlocks);

        Assert.Throws<ArgumentException>(() => CreateQuestion(
            QuestionPresentationType.AllPlayerText,
            [TextBlock(10, "Kyiv"), TextBlock(11, "Lviv")]));
    }

    [Fact]
    public void Multiple_choice_requires_two_to_four_distinct_text_options()
    {
        var question = CreateQuestion(
            QuestionPresentationType.AllPlayerMultipleChoice,
            [TextBlock(10, "Red"), TextBlock(11, "Blue")]);

        Assert.False(question.IsSpecial);
        Assert.True(question.ExcludeFromRandomWagerSelection);
        Assert.Equal(2, question.AnswerBlocks.Count);
        Assert.Equal("Red", question.AnswerBlocks[0].TextContent);

        Assert.Throws<ArgumentException>(() => CreateQuestion(
            QuestionPresentationType.AllPlayerMultipleChoice,
            [TextBlock(10, "Red")]));
        Assert.Throws<ArgumentException>(() => CreateQuestion(
            QuestionPresentationType.AllPlayerMultipleChoice,
            [TextBlock(10, "Red"), TextBlock(11, "red")]));
    }

    [Fact]
    public void Selecting_all_player_question_does_not_open_buzzer()
    {
        var session = CreateSession(QuestionPresentationType.AllPlayerText);
        session.AddPlayer("Rose");
        session.Start();

        var question = session.SelectQuestion(100);

        Assert.True(question.IsAllPlayerQuestion);
        Assert.Equal(RuntimeQuestionStatus.Active, question.Status);
        Assert.Equal(QuestionBuzzerStatus.Closed, question.BuzzerStatus);
        Assert.Null(question.AnsweringPlayerId);
    }

    [Fact]
    public void Wrong_answer_can_be_recorded_for_zero_points()
    {
        var session = CreateSession(QuestionPresentationType.AllPlayerText);
        var rose = session.AddPlayer("Rose");
        var mickey = session.AddPlayer("Mickey");
        session.Start();
        session.SelectQuestion(100);

        var wrong = session.AddQuestionAnswerHistoryEntry(
            100,
            rose.Id,
            isCorrect: false,
            value: 0,
            resolveQuestionIfAvailable: false);
        var correct = session.AddQuestionAnswerHistoryEntry(
            100,
            mickey.Id,
            isCorrect: true,
            value: 200,
            resolveQuestionIfAvailable: false);

        Assert.Equal(0, wrong.ScoreDelta);
        Assert.Equal(0, rose.Score);
        Assert.Equal(200, correct.ScoreDelta);
        Assert.Equal(200, mickey.Score);
    }

    private static GameSession CreateSession(QuestionPresentationType type)
    {
        var answers = type == QuestionPresentationType.AllPlayerMultipleChoice
            ? new[] { TextBlock(10, "A"), TextBlock(11, "B") }
            : new[] { TextBlock(10, "Answer") };
        var question = CreateQuestion(type, answers);
        var quiz = new QuizSnapshot(
            1,
            "All players",
            [new QuizRoundSnapshot(1, "Round", 0, [question])]);
        return GameSession.Create(quiz);
    }

    private static QuizQuestionSnapshot CreateQuestion(
        QuestionPresentationType type,
        IReadOnlyList<ContentBlockSnapshot> answers) => new(
            100,
            10,
            0,
            200,
            true,
            "Category",
            false,
            [TextBlock(1, "Question")],
            answers,
            type);

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
