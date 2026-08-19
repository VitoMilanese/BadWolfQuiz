using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Web.Models;

namespace BadWolfQuiz.Web.Services;

public sealed class QuizSnapshotFactory
{
    public QuizSnapshot Create(Quiz quiz)
    {
        ArgumentNullException.ThrowIfNull(quiz);

        var rounds = quiz.Rounds
            .OrderBy(round => round.SortOrder)
            .Select(CreateRound)
            .ToList();

        var finalQuestion = quiz.FinalQuestionBlocks.Count == 0 ||
            quiz.FinalAnswerBlocks.Count == 0
                ? null
                : new FinalQuestionSnapshot(
                    quiz.FinalQuestionBlocks.Select(CreateContentBlock),
                    quiz.FinalAnswerBlocks.Select(CreateContentBlock));

        return new QuizSnapshot(quiz.Id, quiz.Title, rounds, finalQuestion);
    }

    private static QuizRoundSnapshot CreateRound(QuizRound round)
    {
        var pointsByRow = round.Rows.ToDictionary(row => row.RowIndex, row => row.Points);

        var categories = round.Categories
            .OrderBy(category => category.SortOrder)
            .ToList();

        var questions = categories
            .SelectMany(category => category.Questions
                .OrderBy(question => question.RowIndex)
                .Select(question => CreateQuestion(question, pointsByRow)))
            .ToList();

        var categoryIntros = categories
            .Select(category => new QuizCategoryIntroSnapshot(
                category.Id,
                category.Title,
                category.SortOrder,
                category.DescriptionBlocks.Select(CreateContentBlock)))
            .ToList();

        return new QuizRoundSnapshot(
            round.Id,
            round.Title,
            round.SortOrder,
            questions,
            round.UseRandomWagerQuestions,
            round.RandomWagerQuestionCount,
            round.DescriptionBlocks.Select(CreateContentBlock),
            categoryIntros);
    }

    private static QuizQuestionSnapshot CreateQuestion(
        QuizQuestion question,
        IReadOnlyDictionary<int, int> pointsByRow)
    {
        if (!pointsByRow.TryGetValue(question.RowIndex, out var points))
        {
            throw new InvalidOperationException(
                $"Question {question.Id} references missing row {question.RowIndex}.");
        }

        var presentationType =
            AllPlayerQuestionCompatibility.ResolveStoredPresentationType(
                question);

        return new QuizQuestionSnapshot(
            question.Id,
            question.QuizCategoryId,
            question.RowIndex,
            points,
            question.IsSpecial,
            question.Category.Title,
            question.ExcludeFromRandomWagerSelection,
            question.QuestionBlocks.Select(CreateContentBlock),
            question.AnswerBlocks.Select(CreateContentBlock),
            presentationType);
    }

    private static ContentBlockSnapshot CreateContentBlock(ContentBlockBase block)
    {
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

        return new ContentBlockSnapshot(
            block.Id,
            ResolveContentBlockKind(block),
            block.TextContent,
            block.TopCaption,
            block.BottomCaption,
            block.MediaPath,
            block.ExternalUrl,
            block.FileData?.ToArray(),
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
