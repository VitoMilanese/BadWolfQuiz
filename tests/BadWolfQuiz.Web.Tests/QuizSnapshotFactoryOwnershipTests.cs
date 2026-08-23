using BadWolfQuiz.Web.Models;
using BadWolfQuiz.Web.Services;

namespace BadWolfQuiz.Web.Tests;

public sealed class QuizSnapshotFactoryOwnershipTests
{
    private readonly QuizSnapshotFactory factory = new();

    [Fact]
    public void CreateFromDetachedQuiz_marks_stored_media_for_deferred_loading()
    {
        var fileData = new byte[] { 1, 2, 3, 4 };
        var quiz = CreateQuiz(fileData);

        var snapshot = factory.CreateFromDetachedQuiz(quiz);

        var answerBlock = snapshot.Rounds
            .Single()
            .Questions
            .Single()
            .AnswerBlocks
            .Single();

        Assert.NotSame(fileData, answerBlock.FileData);
        Assert.True(DeferredGameMediaStore.IsDeferred(answerBlock.FileData));
        Assert.Equal("image/png", answerBlock.FileContentType);
        Assert.Equal("answer.png", answerBlock.FileName);
    }

    [Fact]
    public void Create_keeps_copying_caller_owned_media_bytes()
    {
        var fileData = new byte[] { 1, 2, 3, 4 };
        var quiz = CreateQuiz(fileData);

        var snapshot = factory.Create(quiz);

        var answerBlock = snapshot.Rounds
            .Single()
            .Questions
            .Single()
            .AnswerBlocks
            .Single();

        Assert.NotSame(fileData, answerBlock.FileData);
        Assert.Equal(fileData, answerBlock.FileData);
        Assert.False(DeferredGameMediaStore.IsDeferred(answerBlock.FileData));
    }

    private static Quiz CreateQuiz(byte[] fileData)
    {
        var quiz = new Quiz
        {
            Id = 1,
            Title = "Media quiz"
        };
        var round = new QuizRound
        {
            Id = 2,
            QuizId = quiz.Id,
            Quiz = quiz,
            Title = "Round",
            SortOrder = 0
        };
        round.Rows.Add(new QuizRoundRow
        {
            Id = 3,
            QuizRoundId = round.Id,
            Round = round,
            RowIndex = 0,
            Points = 100
        });

        var category = new QuizCategory
        {
            Id = 4,
            QuizRoundId = round.Id,
            Round = round,
            Title = "Category",
            SortOrder = 0
        };
        var question = new QuizQuestion
        {
            Id = 5,
            QuizCategoryId = category.Id,
            Category = category,
            RowIndex = 0
        };
        question.QuestionBlocks.Add(new QuestionContentBlock
        {
            Id = 6,
            QuizQuestionId = question.Id,
            Question = question,
            BlockType = ContentBlockType.Text,
            TextContent = "Question",
            SortOrder = 0
        });
        question.AnswerBlocks.Add(new AnswerContentBlock
        {
            Id = 7,
            QuizQuestionId = question.Id,
            Question = question,
            BlockType = ContentBlockType.Image,
            FileData = fileData,
            FileContentType = "image/png",
            FileName = "answer.png",
            SortOrder = 0
        });

        category.Questions.Add(question);
        round.Categories.Add(category);
        quiz.Rounds.Add(round);
        return quiz;
    }
}
