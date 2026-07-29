using BadWolfQuiz.Web.Models;
using BadWolfQuiz.Web.Services;

namespace BadWolfQuiz.Web.Tests;

public sealed class QuizSnapshotFactoryTests
{
    private readonly QuizSnapshotFactory _factory = new();

    [Fact]
    public void Create_maps_rounds_questions_and_row_points_in_play_order()
    {
        var quiz = CreateQuiz();

        var snapshot = _factory.Create(quiz);

        Assert.Equal(10, snapshot.SourceQuizId);
        Assert.Equal("Test Quiz", snapshot.Title);

        var round = Assert.Single(snapshot.Rounds);
        Assert.Equal(20, round.SourceRoundId);
        Assert.Equal(
            new[] { 101, 100 },
            round.Questions.Select(question => question.SourceQuestionId));
        Assert.Equal(
            new[] { 200, 100 },
            round.Questions.Select(question => question.Points));
        Assert.True(round.Questions[0].IsSpecial);
    }

    [Fact]
    public void Create_returns_snapshot_independent_from_editor_entities()
    {
        var quiz = CreateQuiz();

        var snapshot = _factory.Create(quiz);
        quiz.Title = "Changed title";
        quiz.Rounds.Clear();

        Assert.Equal("Test Quiz", snapshot.Title);
        Assert.Single(snapshot.Rounds);
        Assert.Equal(2, snapshot.Rounds[0].Questions.Count);
    }

    [Fact]
    public void Create_rejects_question_without_matching_point_row()
    {
        var quiz = CreateQuiz();
        quiz.Rounds.Single().Rows.Clear();

        var exception = Assert.Throws<InvalidOperationException>(
            () => _factory.Create(quiz));

        Assert.Contains("references missing row", exception.Message);
    }

    private static Quiz CreateQuiz()
    {
        var quiz = new Quiz
        {
            Id = 10,
            Title = "Test Quiz"
        };

        var round = new QuizRound
        {
            Id = 20,
            QuizId = quiz.Id,
            Title = "Round 1",
            SortOrder = 0,
            Quiz = quiz,
            Rows =
            [
                new QuizRoundRow { Id = 1, RowIndex = 0, Points = 100 },
                new QuizRoundRow { Id = 2, RowIndex = 1, Points = 200 }
            ]
        };

        var secondCategory = new QuizCategory
        {
            Id = 31,
            QuizRoundId = round.Id,
            Title = "Second",
            SortOrder = 1,
            Round = round
        };
        secondCategory.Questions.Add(new QuizQuestion
        {
            Id = 100,
            QuizCategoryId = secondCategory.Id,
            Category = secondCategory,
            RowIndex = 0
        });

        var firstCategory = new QuizCategory
        {
            Id = 30,
            QuizRoundId = round.Id,
            Title = "First",
            SortOrder = 0,
            Round = round
        };
        firstCategory.Questions.Add(new QuizQuestion
        {
            Id = 101,
            QuizCategoryId = firstCategory.Id,
            Category = firstCategory,
            RowIndex = 1,
            IsSpecial = true
        });

        round.Categories.Add(secondCategory);
        round.Categories.Add(firstCategory);
        quiz.Rounds.Add(round);

        return quiz;
    }
}
