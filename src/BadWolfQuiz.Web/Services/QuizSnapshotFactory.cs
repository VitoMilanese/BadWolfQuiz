using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Web.Models;

namespace BadWolfQuiz.Web.Services;

public sealed class QuizSnapshotFactory
{
    public QuizSnapshot Create(Quiz quiz) =>
        Create(quiz, copyFileData: true);

    public QuizSnapshot CreateFromDetachedQuiz(Quiz quiz) =>
        Create(quiz, copyFileData: false);

    private static QuizSnapshot Create(Quiz quiz, bool copyFileData)
    {
        ArgumentNullException.ThrowIfNull(quiz);

        var rounds = quiz.Rounds
            .OrderBy(round => round.SortOrder)
            .Select(round => CreateRound(round, copyFileData))
            .ToList();

        var finalQuestion = quiz.FinalQuestionBlocks.Count == 0 ||
            quiz.FinalAnswerBlocks.Count == 0
                ? null
                : new FinalQuestionSnapshot(
                    quiz.FinalQuestionBlocks.Select(block =>
                        CreateContentBlock(block, copyFileData)),
                    quiz.FinalAnswerBlocks.Select(block =>
                        CreateContentBlock(block, copyFileData)),
                    quiz.FinalDescriptionBlocks.Select(block =>
                        CreateContentBlock(block, copyFileData)));

        return new QuizSnapshot(quiz.Id, quiz.Title, rounds, finalQuestion);
    }

    private static QuizRoundSnapshot CreateRound(
        QuizRound round,
        bool copyFileData)
    {
        var pointsByRow = round.Rows.ToDictionary(row => row.RowIndex, row => row.Points);

        var categories = round.Categories
            .OrderBy(category => category.SortOrder)
            .ToList();

        var questions = categories
            .SelectMany(category => category.Questions
                .OrderBy(question => question.RowIndex)
                .Select(question => CreateQuestion(
                    question,
                    pointsByRow,
                    round.DefaultBuzzMode,
                    copyFileData)))
            .ToList();

        var categoryIntros = categories
            .Select(category => new QuizCategoryIntroSnapshot(
                category.Id,
                category.Title,
                category.SortOrder,
                category.DescriptionBlocks.Select(block =>
                    CreateContentBlock(block, copyFileData))))
            .ToList();

        return new QuizRoundSnapshot(
            round.Id,
            round.Title,
            round.SortOrder,
            questions,
            round.UseRandomWagerQuestions,
            round.RandomWagerQuestionCount,
            round.DescriptionBlocks.Select(block =>
                CreateContentBlock(block, copyFileData)),
            categoryIntros);
    }

    private static QuizQuestionSnapshot CreateQuestion(
        QuizQuestion question,
        IReadOnlyDictionary<int, int> pointsByRow,
        BuzzActivationMode roundDefaultBuzzMode,
        bool copyFileData)
    {
        if (!pointsByRow.TryGetValue(question.RowIndex, out var points))
        {
            throw new InvalidOperationException(
                $"Question {question.Id} references missing row {question.RowIndex}.");
        }

        var presentationType =
            AllPlayerQuestionCompatibility.ResolveStoredPresentationType(
                question);
        var buzzerMode = ResolveBuzzerMode(
            question.BuzzModeOverride,
            roundDefaultBuzzMode);

        return new QuizQuestionSnapshot(
            question.Id,
            question.QuizCategoryId,
            question.RowIndex,
            points,
            question.IsSpecial,
            question.Category.Title,
            question.ExcludeFromRandomWagerSelection,
            question.QuestionBlocks.Select(block =>
                CreateContentBlock(block, copyFileData)),
            question.AnswerBlocks.Select(block =>
                CreateContentBlock(block, copyFileData)),
            presentationType,
            buzzerMode,
            Math.Max(0, question.BuzzDelaySeconds));
    }

    private static QuestionBuzzerMode ResolveBuzzerMode(
        BuzzActivationMode questionMode,
        BuzzActivationMode roundMode)
    {
        // App-created rounds historically persisted Manual as a hidden default.
        // When the question itself inherits, preserve the game-level
        // Automatic/Manual setting instead of treating that legacy value as
        // an explicit per-question Manual choice.
        var effectiveMode = questionMode == BuzzActivationMode.UseRoundDefault
            ? roundMode == BuzzActivationMode.Manual
                ? BuzzActivationMode.UseRoundDefault
                : roundMode
            : questionMode;

        return effectiveMode switch
        {
            BuzzActivationMode.UseRoundDefault => QuestionBuzzerMode.UseGameSetting,
            BuzzActivationMode.Manual => QuestionBuzzerMode.Manual,
            BuzzActivationMode.Immediately => QuestionBuzzerMode.Immediately,
            BuzzActivationMode.AfterMedia => QuestionBuzzerMode.AfterMedia,
            BuzzActivationMode.AfterDelay => QuestionBuzzerMode.AfterDelay,
            BuzzActivationMode.Disabled => QuestionBuzzerMode.Disabled,
            _ => QuestionBuzzerMode.UseGameSetting
        };
    }

    private static ContentBlockSnapshot CreateContentBlock(
        ContentBlockBase block,
        bool copyFileData)
    {
        if (block.BlockType == ContentBlockType.AnswerOptions)
        {
            return new ContentBlockSnapshot(
                block.Id,
                ContentBlockKind.Text,
                AnswerOptionsBlockContract.CreateRuntimeMarker(block.TextContent),
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                block.SortOrder,
                false,
                false);
        }

        if (block.BlockType == ContentBlockType.Container)
        {
            return new ContentBlockSnapshot(
                block.Id,
                ContentBlockKind.Text,
                ContentBlockContainerContract.CreateRuntimeMarker(block.TextContent),
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                block.SortOrder,
                false,
                false);
        }

        byte[]? fileData;
        if (copyFileData)
        {
            fileData = block.FileData?.ToArray();
            if (fileData is { Length: > 0 } &&
                string.Equals(
                    block.FileContentType,
                    "image/gif",
                    StringComparison.OrdinalIgnoreCase))
            {
                fileData = MediaUploadProcessor.NormalizeAnimatedGifLoop(fileData);
            }
        }
        else
        {
            // The detached launch graph contains only a one-byte presence marker,
            // never the stored BLOB. Keep a distinct non-empty runtime marker so
            // existing content validation/rendering still knows the file exists.
            fileData = block.FileData is { Length: > 0 }
                ? DeferredGameMediaStore.CreateMarker()
                : null;
        }

        return new ContentBlockSnapshot(
            block.Id,
            ResolveContentBlockKind(block),
            block.TextContent,
            block.TopCaption,
            block.BottomCaption,
            block.MediaPath,
            block.ExternalUrl,
            fileData,
            block.FileContentType,
            block.FileName,
            block.SortOrder,
            block.AudioOnly,
            block.Autoplay);
    }

    private static ContentBlockKind ResolveContentBlockKind(
        ContentBlockBase block)
    {
        var kind = (ContentBlockKind)(int)block.BlockType;

        if (kind != ContentBlockKind.Video ||
            !Uri.TryCreate(block.ExternalUrl, UriKind.Absolute, out var uri))
        {
            return kind;
        }

        var host = uri.IdnHost.TrimEnd('.');
        var isYouTubeUrl =
            string.Equals(host, "youtu.be", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(host, "youtube.com", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".youtube.com", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                host,
                "youtube-nocookie.com",
                StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(
                ".youtube-nocookie.com",
                StringComparison.OrdinalIgnoreCase);

        return isYouTubeUrl
            ? ContentBlockKind.YouTube
            : kind;
    }
}
