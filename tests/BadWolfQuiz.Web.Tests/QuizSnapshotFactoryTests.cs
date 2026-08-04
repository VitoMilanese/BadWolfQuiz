using BadWolfQuiz.Game.Definitions;
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
        Assert.True(round.UseRandomWagerQuestions);
        Assert.Equal(1, round.RandomWagerQuestionCount);
        Assert.True(round.Questions[0].ExcludeFromRandomWagerSelection);
        Assert.Equal(
            new[] { "First", "Second" },
            round.Questions.Select(question => question.CategoryTitle));
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
    public void Create_copies_question_and_answer_content()
    {
        var quiz = CreateQuiz();
        var question = quiz.Rounds
            .Single()
            .Categories
            .SelectMany(category => category.Questions)
            .Single(item => item.Id == 101);
        var fileData = new byte[] { 1, 2, 3 };

        question.QuestionBlocks.Add(new QuestionContentBlock
        {
            Id = 501,
            BlockType = ContentBlockType.Text,
            TextContent = "Question text",
            SortOrder = 0
        });
        question.AnswerBlocks.Add(new AnswerContentBlock
        {
            Id = 502,
            BlockType = ContentBlockType.Image,
            FileData = fileData,
            FileContentType = "image/png",
            FileName = "answer.png",
            SortOrder = 0
        });

        var snapshot = _factory.Create(quiz);
        fileData[0] = 9;
        question.QuestionBlocks.Clear();

        var snapshotQuestion = snapshot.Rounds.Single().Questions
            .Single(item => item.SourceQuestionId == 101);
        var questionBlock = Assert.Single(snapshotQuestion.QuestionBlocks);
        var answerBlock = Assert.Single(snapshotQuestion.AnswerBlocks);

        Assert.Equal("Question text", questionBlock.TextContent);
        Assert.Equal(501, questionBlock.SourceContentBlockId);
        Assert.Equal(new byte[] { 1, 2, 3 }, answerBlock.FileData);
        Assert.Equal("answer.png", answerBlock.FileName);
    }

    [Fact]
    public void Create_maps_final_question_and_answer_content()
    {
        var quiz = CreateQuiz();
        quiz.FinalQuestionBlocks.Add(new FinalQuestionContentBlock
        {
            Id = 601,
            BlockType = ContentBlockType.Text,
            TextContent = "Final question",
            SortOrder = 0
        });
        quiz.FinalAnswerBlocks.Add(new FinalAnswerContentBlock
        {
            Id = 602,
            BlockType = ContentBlockType.Text,
            TextContent = "Final answer",
            SortOrder = 0
        });

        var snapshot = _factory.Create(quiz);

        Assert.NotNull(snapshot.FinalQuestion);
        Assert.Equal(
            "Final question",
            Assert.Single(snapshot.FinalQuestion.QuestionBlocks).TextContent);
        Assert.Equal(
            "Final answer",
            Assert.Single(snapshot.FinalQuestion.AnswerBlocks).TextContent);
    }

    [Theory]
    [InlineData("https://youtu.be/abc123")]
    [InlineData("https://www.youtube.com/watch?v=abc123")]
    [InlineData("https://www.youtube-nocookie.com/embed/abc123")]
    public void Create_maps_legacy_video_blocks_with_youtube_urls_as_youtube(
        string externalUrl)
    {
        var quiz = CreateQuiz();
        var question = quiz.Rounds
            .Single()
            .Categories
            .SelectMany(category => category.Questions)
            .First();
        question.QuestionBlocks.Add(new QuestionContentBlock
        {
            Id = 503,
            BlockType = ContentBlockType.Video,
            ExternalUrl = externalUrl
        });

        var snapshot = _factory.Create(quiz);
        var block = snapshot.Rounds.Single().Questions
            .Single(item => item.SourceQuestionId == question.Id)
            .QuestionBlocks.Single();

        Assert.Equal(ContentBlockKind.YouTube, block.Kind);
    }

    [Fact]
    public void Create_keeps_direct_video_urls_as_video()
    {
        var quiz = CreateQuiz();
        var question = quiz.Rounds
            .Single()
            .Categories
            .SelectMany(category => category.Questions)
            .First();
        question.QuestionBlocks.Add(new QuestionContentBlock
        {
            Id = 504,
            BlockType = ContentBlockType.Video,
            ExternalUrl = "https://media.example.com/clue.mp4"
        });

        var snapshot = _factory.Create(quiz);
        var block = snapshot.Rounds.Single().Questions
            .Single(item => item.SourceQuestionId == question.Id)
            .QuestionBlocks.Single();

        Assert.Equal(ContentBlockKind.Video, block.Kind);
    }

    [Fact]
    public void Create_omits_final_question_when_no_final_content_exists()
    {
        var snapshot = _factory.Create(CreateQuiz());

        Assert.Null(snapshot.FinalQuestion);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Create_omits_incomplete_final_question(bool hasQuestionBlock)
    {
        var quiz = CreateQuiz();

        if (hasQuestionBlock)
        {
            quiz.FinalQuestionBlocks.Add(new FinalQuestionContentBlock
            {
                BlockType = ContentBlockType.Text,
                TextContent = "Final question"
            });
        }
        else
        {
            quiz.FinalAnswerBlocks.Add(new FinalAnswerContentBlock
            {
                BlockType = ContentBlockType.Text,
                TextContent = "Final answer"
            });
        }

        var snapshot = _factory.Create(quiz);

        Assert.Null(snapshot.FinalQuestion);
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
            UseRandomWagerQuestions = true,
            RandomWagerQuestionCount = 1,
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
            IsSpecial = true,
            ExcludeFromRandomWagerSelection = true
        });

        round.Categories.Add(secondCategory);
        round.Categories.Add(firstCategory);
        quiz.Rounds.Add(round);

        return quiz;
    }
}
