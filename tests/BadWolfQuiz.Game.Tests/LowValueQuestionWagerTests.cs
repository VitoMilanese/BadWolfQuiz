using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Game.Runtime;

namespace BadWolfQuiz.Game.Tests;

public sealed class LowValueQuestionWagerTests
{
    [Theory]
    [InlineData(1, 0, 1, 1)]
    [InlineData(3, 0, 1, 3)]
    [InlineData(3, 3, 1, 3)]
    [InlineData(9, 0, 1, 9)]
    [InlineData(3, 50, 1, 50)]
    [InlineData(10, 0, 5, 10)]
    public void Question_wager_limits_use_low_value_minimum(
        int questionPoints,
        int playerScore,
        int expectedMinimum,
        int expectedMaximum)
    {
        var session = CreateSession(
            questionPoints,
            QuestionPresentationType.Standard);
        var player = session.AddPlayer("Rose");
        session.AdjustPlayerScore(player.Id, playerScore);
        session.Start();
        session.SelectQuestion(100);

        var limits = session.GetQuestionWagerLimits(100);

        Assert.Equal(expectedMinimum, limits.Minimum);
        Assert.Equal(expectedMaximum, limits.Maximum);
    }

    [Fact]
    public void Zero_score_player_can_submit_one_point_wager_for_low_value_question()
    {
        var session = CreateSession(3, QuestionPresentationType.Standard);
        var player = session.AddPlayer("Rose");
        session.Start();
        session.SelectQuestion(100);

        var question = session.SubmitQuestionWager(100, 1);

        Assert.Equal(1, question.Wager!.Amount);
        Assert.Equal(player.Id, question.Wager.PlayerId);
    }

    [Fact]
    public void Zero_score_player_can_submit_one_point_all_player_wager()
    {
        var session = CreateSession(
            3,
            QuestionPresentationType.AllPlayerText);
        var player = session.AddPlayer("Rose");
        session.Start();
        session.SelectQuestion(100);

        var limits = session.GetAllPlayerQuestionWagerLimits(100, player.Id);
        session.SubmitAllPlayerQuestionWager(100, player.Id, 1);

        Assert.Equal(1, limits.Minimum);
        Assert.Equal(3, limits.Maximum);
        Assert.Equal(
            1,
            Assert.Single(session.Board.Questions.Single().AllPlayerWagers).Amount);
    }

    [Fact]
    public void Ten_point_question_keeps_five_point_minimum()
    {
        var session = CreateSession(10, QuestionPresentationType.Standard);
        session.AddPlayer("Rose");
        session.Start();
        session.SelectQuestion(100);

        var limits = session.GetQuestionWagerLimits(100);

        Assert.Equal(5, limits.Minimum);
        Assert.Throws<GameRuleViolationException>(
            () => session.SubmitQuestionWager(100, 1));
        session.SubmitQuestionWager(100, 5);
    }

    private static GameSession CreateSession(
        int points,
        QuestionPresentationType presentationType)
    {
        var answers = presentationType == QuestionPresentationType.AllPlayerText
            ? new[]
            {
                new ContentBlockSnapshot(
                    10,
                    ContentBlockKind.Text,
                    "Answer",
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    0,
                    false)
            }
            : Array.Empty<ContentBlockSnapshot>();

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
            answerBlocks: answers,
            presentationType: presentationType);

        var quiz = new QuizSnapshot(
            1,
            "Low wager",
            [new QuizRoundSnapshot(1, "Round", 0, [question])]);

        return GameSession.Create(quiz);
    }
}
