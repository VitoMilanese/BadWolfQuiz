using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Web.Models;

namespace BadWolfQuiz.Web.Services;

public sealed record QuizQuestionAnomaly(
    string Code,
    string Detail);

public static class QuizQuestionAnomalyDetector
{
    public static QuizQuestionAnomaly? Detect(
        QuizQuestion question,
        IEnumerable<QuizRoundRow>? roundRows = null)
    {
        ArgumentNullException.ThrowIfNull(question);

        if (roundRows is not null &&
            !roundRows.Any(row => row.RowIndex == question.RowIndex))
        {
            return new(
                "missing-row",
                $"Question references missing point row {question.RowIndex}.");
        }

        var presentationType =
            AllPlayerQuestionCompatibility.ResolveStoredPresentationType(question);

        if (presentationType == QuestionPresentationType.FourClues &&
            question.QuestionBlocks.Count != 4)
        {
            return new(
                "four-clues-count",
                $"Four-clue question contains {question.QuestionBlocks.Count} question blocks instead of 4.");
        }

        if (presentationType is
            QuestionPresentationType.AllPlayerMultipleChoice or
            QuestionPresentationType.AllPlayerMultipleChoiceOnDemand)
        {
            if (question.QuestionBlocks.Any(block =>
                    block.BlockType is not ContentBlockType.Text and
                        not ContentBlockType.Image and
                        not ContentBlockType.Container))
            {
                return new(
                    "all-player-question-block",
                    "All-player multiple choice contains an unsupported question content block.");
            }

            var layout = GetAnswerLayout(question, presentationType);
            if (!layout.IsStructurallyValid)
            {
                return new(
                    "answer-options-structure",
                    "The all-player multiple-choice answer-options structure is invalid.");
            }

            if (layout.Options.Count is < 2 or > 4)
            {
                return new(
                    "answer-option-count",
                    $"All-player multiple choice contains {layout.Options.Count} answer option(s); 2 to 4 are required.");
            }

            if (layout.Options.Any(option => !IsValidAllPlayerOption(option)))
            {
                return new(
                    "answer-option-content",
                    "All-player multiple choice contains an empty, unsupported, or overlong answer option.");
            }

            var textOptions = layout.Options
                .Where(option => option.BlockType == ContentBlockType.Text)
                .Select(option => option.TextContent!.Trim())
                .ToArray();
            if (textOptions.Distinct(StringComparer.OrdinalIgnoreCase).Count() !=
                textOptions.Length)
            {
                return new(
                    "duplicate-answer-options",
                    "All-player multiple choice contains duplicate text answer options.");
            }
        }

        if (presentationType == QuestionPresentationType.HostMultipleChoice)
        {
            var layout = GetAnswerLayout(question, presentationType);
            if (!layout.IsStructurallyValid)
            {
                return new(
                    "answer-options-structure",
                    "The host multiple-choice answer-options structure is invalid.");
            }

            if (layout.Options.Count is < 4 or > 10)
            {
                return new(
                    "answer-option-count",
                    $"Host multiple choice contains {layout.Options.Count} answer option(s); 4 to 10 are required.");
            }

            if (layout.Options.Any(option =>
                    option.BlockType != ContentBlockType.Text ||
                    string.IsNullOrWhiteSpace(option.TextContent) ||
                    option.TextContent.Trim().Length > 30))
            {
                return new(
                    "answer-option-content",
                    "Host multiple choice contains an empty, unsupported, or overlong answer option.");
            }

            var choices = layout.Options
                .Select(option => option.TextContent!.Trim())
                .ToArray();
            if (choices.Distinct(StringComparer.OrdinalIgnoreCase).Count() !=
                choices.Length)
            {
                return new(
                    "duplicate-answer-options",
                    "Host multiple choice contains duplicate answer options.");
            }
        }

        return null;
    }

    private static QuestionAnswerLayout GetAnswerLayout(
        QuizQuestion question,
        QuestionPresentationType presentationType)
    {
        var ordered = question.AnswerBlocks
            .OrderBy(block => block.SortOrder)
            .ThenBy(block => block.Id)
            .ToArray();

        if (ordered.Length == 0 ||
            ordered[0].BlockType != ContentBlockType.AnswerOptions)
        {
            return new(ordered, IsStructurallyValid: true);
        }

        var optionCount = AnswerOptionsBlockContract.ParseOptionCount(
            ordered[0].TextContent);
        var availableCount = ordered.Length - 1;
        if (optionCount <= 0 || optionCount > availableCount)
        {
            return new([], IsStructurallyValid: false);
        }

        if (presentationType != QuestionPresentationType.HostMultipleChoice)
        {
            var correctIndexes =
                AnswerOptionsBlockContract.ParseCorrectOptionIndexes(
                    ordered[0].TextContent,
                    optionCount);
            if (correctIndexes.Count == 0 ||
                correctIndexes.Any(index => index < 0 || index >= optionCount))
            {
                return new([], IsStructurallyValid: false);
            }
        }

        return new(
            ordered.Skip(1).Take(optionCount).ToArray(),
            IsStructurallyValid: true);
    }

    private static bool IsValidAllPlayerOption(AnswerContentBlock block) =>
        block.BlockType switch
        {
            ContentBlockType.Text =>
                !string.IsNullOrWhiteSpace(block.TextContent) &&
                block.TextContent.Trim().Length <= 30,
            ContentBlockType.Image =>
                block.FileData is { Length: > 0 } &&
                !string.IsNullOrWhiteSpace(block.FileContentType),
            _ => false
        };

    private sealed record QuestionAnswerLayout(
        IReadOnlyList<AnswerContentBlock> Options,
        bool IsStructurallyValid);
}
