using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Game.Runtime;

namespace BadWolfQuiz.Game.Tests;

public sealed class AnonymousSharedWagerRandomSelectionTests
{
    [Fact]
    public void Normal_and_anonymous_random_wagers_use_disjoint_question_sets()
    {
        var round = new QuizRoundSnapshot(
            1,
            "Round",
            0,
            Enumerable.Range(1, 4).Select(CreateStandardQuestion).ToArray(),
            useRandomWagerQuestions: true,
            randomWagerQuestionCount: 2,
            useRandomAnonymousSharedWagerQuestions: true,
            randomAnonymousSharedWagerQuestionCount: 2);

        var session = GameSession.Create(new QuizSnapshot(1, "Quiz", [round]));
        var special = session.Board.Questions.Where(question => question.IsSpecial).ToArray();

        Assert.Equal(4, special.Length);
        Assert.Equal(
            2,
            special.Count(question =>
                QuestionWagerModes.IsAnonymousShared(question.PresentationType)));
        Assert.Equal(
            2,
            special.Count(question =>
                !QuestionWagerModes.IsAnonymousShared(question.PresentationType)));
    }

    [Fact]
    public void Anonymous_random_wager_turns_a_standard_question_into_shared_wager()
    {
        var round = new QuizRoundSnapshot(
            1,
            "Round",
            0,
            [CreateStandardQuestion(1)],
            useRandomAnonymousSharedWagerQuestions: true,
            randomAnonymousSharedWagerQuestionCount: 1);

        var session = GameSession.Create(new QuizSnapshot(1, "Quiz", [round]));
        var question = Assert.Single(session.Board.Questions);

        Assert.True(question.IsSpecial);
        Assert.True(QuestionWagerModes.IsAnonymousShared(question.PresentationType));
    }

    [Fact]
    public void Combined_random_wager_counts_cannot_exceed_the_candidate_pool()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new QuizRoundSnapshot(
            1,
            "Round",
            0,
            Enumerable.Range(1, 3).Select(CreateStandardQuestion).ToArray(),
            useRandomWagerQuestions: true,
            randomWagerQuestionCount: 2,
            useRandomAnonymousSharedWagerQuestions: true,
            randomAnonymousSharedWagerQuestionCount: 2));
    }

    [Fact]
    public void Manual_normal_wager_is_preserved_when_anonymous_random_wagers_are_enabled()
    {
        var manualWager = new QuizQuestionSnapshot(
            999, 99, 4, 200, isSpecial: true,
            categoryTitle: "Manual wager",
            excludeFromRandomWagerSelection: true);
        var round = new QuizRoundSnapshot(
            1,
            "Round",
            0,
            [
                manualWager,
                CreateStandardQuestion(1),
                CreateStandardQuestion(2),
                CreateStandardQuestion(3),
                CreateStandardQuestion(4)
            ],
            useRandomAnonymousSharedWagerQuestions: true,
            randomAnonymousSharedWagerQuestionCount: 4);

        var session = GameSession.Create(new QuizSnapshot(1, "Quiz", [round]));
        session.AddPlayer("Player");
        session.Start();

        var selected = session.SelectQuestion(manualWager.SourceQuestionId);

        Assert.True(selected.IsSpecial);
        Assert.Equal(QuestionPresentationType.Standard, selected.PresentationType);
        Assert.False(QuestionWagerModes.IsAnonymousShared(selected.PresentationType));
        Assert.Equal(RuntimeQuestionStatus.AwaitingWager, selected.Status);
        Assert.Equal(
            4,
            session.Board.Questions.Count(question =>
                question.SourceQuestionId != manualWager.SourceQuestionId &&
                question.IsSpecial &&
                QuestionWagerModes.IsAnonymousShared(question.PresentationType)));
    }

    [Fact]
    public void Manual_shared_wager_is_preserved_when_normal_random_wagers_are_enabled()
    {
        var manualWager = new QuizQuestionSnapshot(
            998, 98, 4, 200, isSpecial: true,
            categoryTitle: "Manual shared wager",
            excludeFromRandomWagerSelection: true,
            presentationType: QuestionWagerModes.AnonymousShared);
        var round = new QuizRoundSnapshot(
            1,
            "Round",
            0,
            [
                manualWager,
                CreateStandardQuestion(1),
                CreateStandardQuestion(2),
                CreateStandardQuestion(3),
                CreateStandardQuestion(4)
            ],
            useRandomWagerQuestions: true,
            randomWagerQuestionCount: 4);

        var session = GameSession.Create(new QuizSnapshot(1, "Quiz", [round]));
        session.AddPlayer("Player");
        session.Start();

        var selected = session.SelectQuestion(manualWager.SourceQuestionId);

        Assert.True(selected.IsSpecial);
        Assert.True(QuestionWagerModes.IsAnonymousShared(selected.PresentationType));
        Assert.Equal(RuntimeQuestionStatus.AwaitingWager, selected.Status);
        Assert.Equal(
            4,
            session.Board.Questions.Count(question =>
                question.SourceQuestionId != manualWager.SourceQuestionId &&
                question.IsSpecial &&
                !QuestionWagerModes.IsAnonymousShared(question.PresentationType)));
    }

    private static QuizQuestionSnapshot CreateStandardQuestion(int index) => new(
        100 + index,
        10 + index,
        index - 1,
        200,
        isSpecial: false,
        categoryTitle: $"Category {index}",
        questionBlocks:
        [
            new ContentBlockSnapshot(
                1000 + index,
                ContentBlockKind.Text,
                $"Question {index}",
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                0,
                false)
        ]);
}
