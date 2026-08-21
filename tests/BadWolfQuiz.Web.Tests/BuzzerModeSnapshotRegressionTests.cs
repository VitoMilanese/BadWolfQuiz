using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Web.Models;
using BadWolfQuiz.Web.Services;

namespace BadWolfQuiz.Web.Tests;

public sealed class BuzzerModeSnapshotRegressionTests
{
    [Fact]
    public void Round_default_and_question_delay_reach_runtime_snapshot()
    {
        var quiz = CreateQuiz(
            BuzzActivationMode.AfterDelay,
            BuzzActivationMode.UseRoundDefault,
            7);

        var snapshot = new QuizSnapshotFactory().Create(quiz);
        var question = snapshot.Rounds.Single().Questions.Single();

        Assert.Equal(QuestionBuzzerMode.AfterDelay, question.BuzzerMode);
        Assert.Equal(7, question.BuzzDelaySeconds);
    }

    [Fact]
    public void Question_override_wins_over_round_default()
    {
        var quiz = CreateQuiz(
            BuzzActivationMode.Manual,
            BuzzActivationMode.Immediately,
            0);

        var snapshot = new QuizSnapshotFactory().Create(quiz);

        Assert.Equal(
            QuestionBuzzerMode.Immediately,
            snapshot.Rounds.Single().Questions.Single().BuzzerMode);
    }

    private static Quiz CreateQuiz(
        BuzzActivationMode roundMode,
        BuzzActivationMode questionMode,
        int delaySeconds)
    {
        var quiz = new Quiz { Id = 1, Title = "Quiz" };
        var round = new QuizRound
        {
            Id = 2,
            QuizId = 1,
            Quiz = quiz,
            Title = "Round",
            SortOrder = 0,
            DefaultBuzzMode = roundMode
        };
        round.Rows.Add(new QuizRoundRow
        {
            Id = 3,
            QuizRoundId = 2,
            Round = round,
            RowIndex = 0,
            Points = 100
        });
        var category = new QuizCategory
        {
            Id = 4,
            QuizRoundId = 2,
            Round = round,
            Title = "Category",
            SortOrder = 0
        };
        var question = new QuizQuestion
        {
            Id = 5,
            QuizCategoryId = 4,
            Category = category,
            RowIndex = 0,
            BuzzModeOverride = questionMode,
            BuzzDelaySeconds = delaySeconds
        };
        question.QuestionBlocks.Add(new QuestionContentBlock
        {
            Id = 6,
            QuizQuestionId = 5,
            Question = question,
            BlockType = ContentBlockType.Text,
            TextContent = "Question",
            SortOrder = 0
        });
        question.AnswerBlocks.Add(new AnswerContentBlock
        {
            Id = 7,
            QuizQuestionId = 5,
            Question = question,
            BlockType = ContentBlockType.Text,
            TextContent = "Answer",
            SortOrder = 0
        });
        category.Questions.Add(question);
        round.Categories.Add(category);
        quiz.Rounds.Add(round);
        return quiz;
    }
}
