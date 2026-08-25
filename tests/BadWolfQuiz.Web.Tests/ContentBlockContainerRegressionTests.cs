using System.Globalization;
using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Web.Localization;
using BadWolfQuiz.Web.Models;
using BadWolfQuiz.Web.Services;

namespace BadWolfQuiz.Web.Tests;

public sealed class ContentBlockContainerRegressionTests
{
    [Theory]
    [InlineData(null, 0)]
    [InlineData("", 0)]
    [InlineData("0", 0)]
    [InlineData("-1", 0)]
    [InlineData("3", 3)]
    public void Container_contract_parses_child_counts(
        string? value,
        int expected)
    {
        Assert.Equal(
            expected,
            ContentBlockContainerContract.ParseChildCount(value));
    }

    [Fact]
    public void Container_contract_round_trips_runtime_marker()
    {
        var marker = ContentBlockContainerContract.CreateRuntimeMarker("3");

        Assert.Equal("__badwolf_container:3__", marker);
        Assert.True(
            ContentBlockContainerContract.TryParseRuntimeMarker(
                marker,
                out var childCount));
        Assert.Equal(3, childCount);
    }

    [Fact]
    public void Snapshot_flattens_container_marker_before_supported_children()
    {
        var quiz = CreateQuizWithContainer();

        var snapshot = new QuizSnapshotFactory().Create(quiz);
        var blocks = snapshot.Rounds.Single()
            .Questions.Single()
            .QuestionBlocks;

        Assert.Equal(6, blocks.Count);
        Assert.Equal(ContentBlockKind.Text, blocks[0].Kind);
        Assert.True(
            ContentBlockContainerContract.TryParseRuntimeMarker(
                blocks[0].TextContent,
                out var childCount));
        Assert.Equal(4, childCount);
        Assert.Equal(ContentBlockKind.Image, blocks[1].Kind);
        Assert.Equal(ContentBlockKind.YouTube, blocks[2].Kind);
        Assert.Equal(ContentBlockKind.Audio, blocks[3].Kind);
        Assert.Equal(ContentBlockKind.Text, blocks[4].Kind);
        Assert.Equal("Inside text", blocks[4].TextContent);
        Assert.Equal(ContentBlockKind.Image, blocks[5].Kind);
        Assert.Equal("outside.png", blocks[5].FileName);
    }

    [Theory]
    [InlineData("en-US", "Container", "Horizontal content", "Add text, images, YouTube, or audio")]
    [InlineData("uk-UA", "Контейнер", "Горизонтальний контент", "Додайте текст, зображення, YouTube або аудіо")]
    [InlineData("it-IT", "Contenitore", "Contenuto orizzontale", "Aggiungi testo, immagini, YouTube o audio")]
    [InlineData("ru-RU", "Контейнер", "Горизонтальный контент", "Добавьте текст, изображения, YouTube или аудио")]
    public void Container_editor_text_is_localized(
        string cultureName,
        string expectedTitle,
        string expectedHorizontalContent,
        string expectedEmptyHint)
    {
        var previousCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo(cultureName);

            Assert.Equal(expectedTitle, ContentBlockContainerText.Title);
            Assert.Equal(
                expectedHorizontalContent,
                ContentBlockContainerText.HorizontalContent);
            Assert.Equal(expectedEmptyHint, ContentBlockContainerText.EmptyHint);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousCulture;
        }
    }

    [Fact]
    public void Shared_editor_and_runtime_scripts_expose_container_behavior()
    {
        var root = FindRepositoryRoot();
        var collection = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "Quizzes",
            "Shared",
            "_ContentBlockCollection.cshtml"));
        var card = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "Quizzes",
            "Shared",
            "_ContentBlockCard.cshtml"));
        var script = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "content-block-containers.js"));
        var bootstrap = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "gameplay-escape-shortcuts.js"));
        var stylesheet = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "css",
            "content-block-containers.css"));

        Assert.Contains("ContentBlockType.Container", collection, StringComparison.Ordinal);
        Assert.Contains("ContentBlockContainerText.Title", collection, StringComparison.Ordinal);
        Assert.Contains("ContentBlockType.Container", card, StringComparison.Ordinal);
        Assert.Contains("ContentBlockContainerText.HorizontalContent", card, StringComparison.Ordinal);
        Assert.Contains("data-container-empty-label", card, StringComparison.Ordinal);
        Assert.Contains("data-container-add-block-type=\"Text\"", card, StringComparison.Ordinal);
        Assert.Contains("data-container-add-block-type=\"Image\"", card, StringComparison.Ordinal);
        Assert.Contains("data-container-add-block-type=\"YouTube\"", card, StringComparison.Ordinal);
        Assert.Contains("data-container-add-block-type=\"Audio\"", card, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "data-container-add-block-type=\"Video\"",
            card,
            StringComparison.Ordinal);
        Assert.Contains("content-block-container-count", card, StringComparison.Ordinal);

        Assert.Contains("__badwolf_container:", script, StringComparison.Ordinal);
        Assert.Contains("\"Text\",", script, StringComparison.Ordinal);
        Assert.Contains("[data-open-question-preview]", script, StringComparison.Ordinal);
        Assert.Contains(".game-content-blocks", script, StringComparison.Ordinal);
        Assert.Contains("var(--content-block-container-columns)", script, StringComparison.Ordinal);
        Assert.Contains("minmax(0, 1fr)", script, StringComparison.Ordinal);
        Assert.Contains("align-items: center", stylesheet, StringComparison.Ordinal);
        Assert.Contains(".game-content-text", stylesheet, StringComparison.Ordinal);
        Assert.Contains(
            "attr(data-container-empty-label)",
            stylesheet,
            StringComparison.Ordinal);
        Assert.Contains("content-block-containers.css", bootstrap, StringComparison.Ordinal);
        Assert.Contains("content-block-containers.js", bootstrap, StringComparison.Ordinal);
    }

    [Fact]
    public void Description_editor_keeps_its_existing_text_and_image_scope()
    {
        var root = FindRepositoryRoot();
        var collection = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "Quizzes",
            "Shared",
            "_ContentBlockCollection.cshtml"));

        Assert.Contains(
            "new[] { ContentBlockType.Text, ContentBlockType.Image }",
            collection,
            StringComparison.Ordinal);
    }

    private static Quiz CreateQuizWithContainer()
    {
        var quiz = new Quiz
        {
            Id = 1,
            Title = "Container test"
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
            Id = 10,
            BlockType = ContentBlockType.Container,
            TextContent = ContentBlockContainerContract.StoreChildCount(4),
            SortOrder = 0
        });
        question.QuestionBlocks.Add(new QuestionContentBlock
        {
            Id = 11,
            BlockType = ContentBlockType.Image,
            FileData = [1],
            FileContentType = "image/png",
            FileName = "inside.png",
            SortOrder = 1
        });
        question.QuestionBlocks.Add(new QuestionContentBlock
        {
            Id = 12,
            BlockType = ContentBlockType.YouTube,
            ExternalUrl = "https://www.youtube.com/watch?v=abc123",
            SortOrder = 2
        });
        question.QuestionBlocks.Add(new QuestionContentBlock
        {
            Id = 13,
            BlockType = ContentBlockType.Audio,
            FileData = [2],
            FileContentType = "audio/mpeg",
            FileName = "inside.mp3",
            SortOrder = 3
        });
        question.QuestionBlocks.Add(new QuestionContentBlock
        {
            Id = 14,
            BlockType = ContentBlockType.Text,
            TextContent = "Inside text",
            SortOrder = 4
        });
        question.QuestionBlocks.Add(new QuestionContentBlock
        {
            Id = 15,
            BlockType = ContentBlockType.Image,
            FileData = [3],
            FileContentType = "image/png",
            FileName = "outside.png",
            SortOrder = 5
        });
        question.AnswerBlocks.Add(new AnswerContentBlock
        {
            Id = 20,
            BlockType = ContentBlockType.Text,
            TextContent = "Answer",
            SortOrder = 0
        });

        category.Questions.Add(question);
        round.Categories.Add(category);
        quiz.Rounds.Add(round);
        return quiz;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src")) &&
                Directory.Exists(Path.Combine(directory.FullName, "tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
