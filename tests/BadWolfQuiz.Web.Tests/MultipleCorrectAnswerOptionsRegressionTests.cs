using System.Text.Json;
using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Models;
using BadWolfQuiz.Web.Services;

namespace BadWolfQuiz.Web.Tests;

public sealed class MultipleCorrectAnswerOptionsRegressionTests
{
    [Fact]
    public void All_player_multiple_choice_supports_multiple_correct_options()
    {
        var question = CreateQuestion(
            QuestionPresentationType.AllPlayerMultipleChoice,
            [
                Marker(3, [0, 2]),
                TextBlock(101, "Alpha", 1),
                TextBlock(102, "Beta", 2),
                TextBlock(103, "Gamma", 3),
                TextBlock(104, "Explanation", 4)
            ]);

        Assert.Equal([101, 102, 103], question.AnswerBlocks
            .Select(block => block.SourceContentBlockId));
        Assert.Equal([101, 103, 104], question.RevealAnswerBlocks
            .Select(block => block.SourceContentBlockId));
        Assert.Equal([101, 103], question.GetCorrectAnswerOptionIds());
        Assert.True(MultipleChoiceAnswerContract.TryParseRuntimeMarker(
            question.StoredAnswerBlocks[0].TextContent,
            out var optionCount,
            out var correctIndexes));
        Assert.Equal(3, optionCount);
        Assert.Equal([0, 2], correctIndexes);
    }

    [Fact]
    public void Legacy_all_player_multiple_choice_keeps_first_option_correct()
    {
        var question = CreateQuestion(
            QuestionPresentationType.AllPlayerMultipleChoice,
            [
                TextBlock(201, "Alpha", 0),
                TextBlock(202, "Beta", 1)
            ]);

        Assert.Equal([201], question.GetCorrectAnswerOptionIds());
        Assert.Equal([201], question.RevealAnswerBlocks
            .Select(block => block.SourceContentBlockId));
    }

    [Fact]
    public void Host_multiple_choice_remains_first_option_only()
    {
        var question = CreateQuestion(
            QuestionPresentationType.HostMultipleChoice,
            [
                Marker(4, [0, 2]),
                TextBlock(301, "Alpha", 1),
                TextBlock(302, "Beta", 2),
                TextBlock(303, "Gamma", 3),
                TextBlock(304, "Delta", 4)
            ]);

        Assert.Equal([301], question.GetCorrectAnswerOptionIds());
        Assert.Equal([301], question.RevealAnswerBlocks
            .Select(block => block.SourceContentBlockId));
    }

    [Fact]
    public void Stored_editor_state_round_trips_correct_option_indexes()
    {
        var stored = AnswerOptionsBlockContract.StoreOptionState(4, [0, 2, 3]);

        Assert.Equal("4|0,2,3", stored);
        Assert.Equal(4, AnswerOptionsBlockContract.ParseOptionCount(stored));
        Assert.Equal(
            [0, 2, 3],
            AnswerOptionsBlockContract.ParseCorrectOptionIndexes(stored, 4));

        var runtime = AnswerOptionsBlockContract.CreateRuntimeMarker(stored);
        Assert.True(MultipleChoiceAnswerContract.TryParseRuntimeMarker(
            runtime,
            out var optionCount,
            out var correctIndexes));
        Assert.Equal(4, optionCount);
        Assert.Equal([0, 2, 3], correctIndexes);
    }

    [Fact]
    public void Active_game_snapshot_round_trip_preserves_multiple_correct_options()
    {
        var question = CreateQuestion(
            QuestionPresentationType.AllPlayerMultipleChoice,
            [
                Marker(3, [0, 2]),
                TextBlock(401, "Alpha", 1),
                TextBlock(402, "Beta", 2),
                TextBlock(403, "Gamma", 3),
                TextBlock(404, "Explanation", 4)
            ]);
        var quiz = new QuizSnapshot(
            42,
            "Recovery quiz",
            [new QuizRoundSnapshot(7, "Round 1", 0, [question])]);
        var session = BadWolfQuiz.Game.Runtime.GameSession.Create(quiz);
        session.AddPlayer("Rose");
        var snapshot = new ActiveGameSnapshot(
            "ABC123",
            "host-1",
            true,
            quiz,
            session.Settings,
            session.CaptureState());
        var options = new JsonSerializerOptions
        {
            Converters = { new QuizSnapshotJsonConverter() }
        };

        var json = JsonSerializer.Serialize(snapshot, options);
        var restored = JsonSerializer.Deserialize<ActiveGameSnapshot>(json, options);

        Assert.NotNull(restored);
        var restoredQuestion = restored.Quiz.Rounds.Single().Questions.Single();
        Assert.Equal([401, 403], restoredQuestion.GetCorrectAnswerOptionIds());
        Assert.Equal([401, 403, 404], restoredQuestion.RevealAnswerBlocks
            .Select(block => block.SourceContentBlockId));
    }

    [Fact]
    public void Editor_gameplay_and_shared_reveal_paths_use_multiple_correct_state()
    {
        var root = FindRepositoryRoot();
        var editor = Read(root,
            "src/BadWolfQuiz.Web/Pages/Admin/Quizzes/QuestionEditor.cshtml.cs");
        var editorCard = Read(root,
            "src/BadWolfQuiz.Web/Pages/Admin/Quizzes/Shared/_ContentBlockCard.cshtml");
        var editorScript = Read(root,
            "src/BadWolfQuiz.Web/wwwroot/js/multiple-correct-answer-options.js");
        var assets = Read(root,
            "src/BadWolfQuiz.Web/TagHelpers/MultipleChoiceAnswerOptionsAssetsTagHelper.cs");
        var allPlayer = Read(root,
            "src/BadWolfQuiz.Web/Pages/AllPlayerQuestion.cshtml.cs");
        var revealModel = Read(root,
            "src/BadWolfQuiz.Web/Pages/Admin/Games/MultipleChoiceRevealBlocksViewModel.cs");
        var reveal = Read(root,
            "src/BadWolfQuiz.Web/Pages/Admin/Games/_MultipleChoiceRevealBlocks.cshtml");
        var liveReveal = Read(root,
            "src/BadWolfQuiz.Web/TagHelpers/MultipleChoiceAnswerRevealTagHelper.cs");
        var resolvedPreview = Read(root,
            "src/BadWolfQuiz.Web/Pages/Admin/Games/_GameContentPreview.cshtml");
        var answerKey = Read(root,
            "src/BadWolfQuiz.Web/Pages/Admin/Games/AnswerKey.cshtml.cs");

        Assert.Contains("StoreOptionState", editor, StringComparison.Ordinal);
        Assert.Contains("ParseCorrectOptionIndexes", editor, StringComparison.Ordinal);
        Assert.Contains("at least one correct answer option", editor, StringComparison.Ordinal);
        Assert.Contains("data-answer-options-stored-state", editorCard, StringComparison.Ordinal);
        Assert.Contains("multiple-correct-answer-options.js", assets, StringComparison.Ordinal);
        Assert.Contains("serializeState", editorScript, StringComparison.Ordinal);
        Assert.Contains("multipleCorrectAnswerOption", editorScript, StringComparison.Ordinal);
        Assert.Contains("rebuildAnswerPreview", editorScript, StringComparison.Ordinal);

        Assert.Contains("GetCorrectAnswerOptionIds", allPlayer, StringComparison.Ordinal);
        Assert.Contains("Contains(selectedBlockId)", allPlayer, StringComparison.Ordinal);
        Assert.Contains("CorrectOptionIds", revealModel, StringComparison.Ordinal);
        Assert.Contains(
            "Model.CorrectOptionIds.Contains(block.SourceContentBlockId)",
            reveal,
            StringComparison.Ordinal);
        Assert.Contains("GetCorrectAnswerOptionIds", liveReveal, StringComparison.Ordinal);
        Assert.Contains("correctOptionIds", resolvedPreview, StringComparison.Ordinal);
        Assert.Contains("RevealAnswerBlocks", answerKey, StringComparison.Ordinal);
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

    private static ContentBlockSnapshot Marker(
        int optionCount,
        IEnumerable<int> correctOptionIndexes) => new(
            1000,
            ContentBlockKind.Text,
            MultipleChoiceAnswerContract.CreateRuntimeMarker(
                optionCount,
                correctOptionIndexes),
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