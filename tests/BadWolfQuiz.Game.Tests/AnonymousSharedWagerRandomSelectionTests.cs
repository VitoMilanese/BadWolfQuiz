using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Game.Runtime;

namespace BadWolfQuiz.Game.Tests;

public sealed class AnonymousSharedWagerRandomSelectionTests
{
    [Fact]
    public void Shared_wager_mode_can_be_selected_by_existing_random_wager_round_logic()
    {
        var question = new QuizQuestionSnapshot(
            100,
            10,
            0,
            200,
            isSpecial: false,
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
            presentationType: QuestionWagerModes.AnonymousShared);
        var round = new QuizRoundSnapshot(
            1,
            "Round",
            0,
            [question],
            useRandomWagerQuestions: true,
            randomWagerQuestionCount: 1);

        var session = GameSession.Create(new QuizSnapshot(
            1,
            "Quiz",
            [round]));

        var runtimeQuestion = Assert.Single(session.Board.Questions);
        Assert.True(runtimeQuestion.IsSpecial);
        Assert.True(QuestionWagerModes.IsAnonymousShared(runtimeQuestion.PresentationType));
    }

    [Fact]
    public void Shared_wager_mode_remains_a_normal_question_when_not_explicit_or_randomly_selected()
    {
        var question = new QuizQuestionSnapshot(
            100,
            10,
            0,
            200,
            isSpecial: false,
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
            presentationType: QuestionWagerModes.AnonymousShared);

        var session = GameSession.Create(new QuizSnapshot(
            1,
            "Quiz",
            [new QuizRoundSnapshot(1, "Round", 0, [question])]));

        var runtimeQuestion = Assert.Single(session.Board.Questions);
        Assert.False(runtimeQuestion.IsSpecial);
        Assert.True(QuestionWagerModes.IsAnonymousShared(runtimeQuestion.PresentationType));
    }
}
