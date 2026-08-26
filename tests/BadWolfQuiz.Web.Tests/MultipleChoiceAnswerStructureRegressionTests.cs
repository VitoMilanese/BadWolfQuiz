using BadWolfQuiz.Game.Definitions;

namespace BadWolfQuiz.Web.Tests;

public sealed class MultipleChoiceAnswerStructureRegressionTests
{
    [Fact]
    public void All_player_multiple_choice_separates_options_from_reveal_content()
    {
        var correct = TextBlock(101, "Correct", 1);
        var imageOption = new ContentBlockSnapshot(
            102,
            ContentBlockKind.Image,
            null,
            null,
            null,
            null,
            null,
            [1],
            "image/png",
            "option.png",
            2,
            false);
        var explanation = TextBlock(103, "Explanation", 3);
        var audio = new ContentBlockSnapshot(
            104,
            ContentBlockKind.Audio,
            null,
            null,
            null,
            null,
            null,
            [2],
            "audio/mpeg",
            "explanation.mp3",
            4,
            false);

        var question = CreateQuestion(
            QuestionPresentationType.AllPlayerMultipleChoice,
            [
                Marker(2),
                correct,
                imageOption,
                explanation,
                audio
            ]);

        Assert.Equal([101, 102], question.AnswerBlocks
            .Select(block => block.SourceContentBlockId));
        Assert.Equal([101, 103, 104], question.RevealAnswerBlocks
            .Select(block => block.SourceContentBlockId));
        Assert.Equal(5, question.StoredAnswerBlocks.Count);
        Assert.True(MultipleChoiceAnswerContract.TryParseRuntimeMarker(
            question.StoredAnswerBlocks[0].TextContent,
            out var optionCount));
        Assert.Equal(2, optionCount);
    }

    [Fact]
    public void Host_multiple_choice_keeps_additional_content_out_of_options()
    {
        var answerBlocks = new List<ContentBlockSnapshot>
        {
            Marker(4),
            TextBlock(201, "Alpha", 1),
            TextBlock(202, "Beta", 2),
            TextBlock(203, "Gamma", 3),
            TextBlock(204, "Delta", 4),
            new(
                205,
                ContentBlockKind.Image,
                null,
                "Why",
                null,
                null,
                null,
                [9],
                "image/png",
                "explanation.png",
                5,
                false)
        };

        var question = CreateQuestion(
            QuestionPresentationType.HostMultipleChoice,
            answerBlocks);

        Assert.Equal([201, 202, 203, 204], question.AnswerBlocks
            .Select(block => block.SourceContentBlockId));
        Assert.Equal([201, 205], question.RevealAnswerBlocks
            .Select(block => block.SourceContentBlockId));
        Assert.All(question.AnswerBlocks, block =>
            Assert.Equal(ContentBlockKind.Text, block.Kind));
    }

    [Fact]
    public void Legacy_multiple_choice_without_marker_still_uses_all_blocks_as_options()
    {
        var question = CreateQuestion(
            QuestionPresentationType.AllPlayerMultipleChoice,
            [
                TextBlock(301, "Correct", 0),
                TextBlock(302, "Wrong", 1)
            ]);

        Assert.Equal([301, 302], question.AnswerBlocks
            .Select(block => block.SourceContentBlockId));
        Assert.Equal([301], question.RevealAnswerBlocks
            .Select(block => block.SourceContentBlockId));
        Assert.Equal([301, 302], question.StoredAnswerBlocks
            .Select(block => block.SourceContentBlockId));
    }

    [Fact]
    public void Editor_and_all_reveal_surfaces_use_the_shared_answer_options_contract()
    {
        var root = FindRepositoryRoot();
        var editorModel = Read(root,
            "src/BadWolfQuiz.Web/Pages/Admin/Quizzes/QuestionEditor.cshtml.cs");
        var editorCard = Read(root,
            "src/BadWolfQuiz.Web/Pages/Admin/Quizzes/Shared/_ContentBlockCard.cshtml");
        var editorScript = Read(root,
            "src/BadWolfQuiz.Web/wwwroot/js/multiple-choice-answer-options.js");
        var liveReveal = Read(root,
            "src/BadWolfQuiz.Web/TagHelpers/MultipleChoiceAnswerRevealTagHelper.cs");
        var resolvedPreview = Read(root,
            "src/BadWolfQuiz.Web/Pages/Admin/Games/_GameContentPreview.cshtml");
        var answerKey = Read(root,
            "src/BadWolfQuiz.Web/Pages/Admin/Games/AnswerKey.cshtml.cs");
        var revealPartial = Read(root,
            "src/BadWolfQuiz.Web/Pages/Admin/Games/_MultipleChoiceRevealBlocks.cshtml");

        Assert.Contains("ContentBlockType.AnswerOptions", editorModel);
        Assert.Contains("NormalizeAnswerOptionsStructure", editorModel);
        Assert.Contains("ValidateAllPlayerMultipleChoiceAnswerOptions", editorModel);
        Assert.Contains("ValidateHostMultipleChoiceAnswerOptions", editorModel);
        Assert.Contains("data-answer-options-block", editorCard);
        Assert.Contains("data-answer-options-children", editorCard);
        Assert.Contains("data-answer-option-add-block-type=\"Text\"", editorCard);
        Assert.Contains("data-answer-option-add-block-type=\"Image\"", editorCard);
        Assert.Contains("minimumOptions", editorScript);
        Assert.Contains("maximumOptions", editorScript);
        Assert.Contains("getAdditionalCards", editorScript);
        Assert.Contains("rebuildMultipleChoiceAnswerPreview", editorScript);

        Assert.Contains("RevealAnswerBlocks", liveReveal);
        Assert.Contains("_MultipleChoiceRevealBlocks", liveReveal);
        Assert.Contains("RevealAnswerBlocks", resolvedPreview);
        Assert.Contains("_MultipleChoiceRevealBlocks", resolvedPreview);
        Assert.Contains("RevealAnswerBlocks", answerKey);
        Assert.Contains("MultipleChoiceAnswerMedia", revealPartial);
        Assert.DoesNotContain("all-player-answer-option-incorrect", revealPartial);
    }

    private static QuizQuestionSnapshot CreateQuestion(
        QuestionPresentationType presentationType,
        IEnumerable<ContentBlockSnapshot> answerBlocks) => new(
            sourceQuestionId: 1,
            sourceCategoryId: 1,
            rowIndex: 0,
            points: 100,
            isSpecial: false,
            questionBlocks: [TextBlock(1, "Question", 0)],
            answerBlocks: answerBlocks,
            presentationType: presentationType);

    private static ContentBlockSnapshot Marker(int optionCount) => new(
        1000,
        ContentBlockKind.Text,
        MultipleChoiceAnswerContract.CreateRuntimeMarker(optionCount),
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        0,
        false);

    private static ContentBlockSnapshot TextBlock(
        int id,
        string text,
        int sortOrder) => new(
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
            sortOrder,
            false);

    private static string Read(string root, string relativePath) =>
        File.ReadAllText(Path.Combine(
            root,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));

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
