namespace BadWolfQuiz.Game.Definitions;

public static class MultipleChoiceAnswerContract
{
    public const string RuntimeMarkerPrefix = "__badwolf_answer_options:";
    public const string RuntimeMarkerSuffix = "__";

    public static bool IsMultipleChoice(QuestionPresentationType presentationType) =>
        presentationType is
            QuestionPresentationType.AllPlayerMultipleChoice or
            QuestionPresentationType.HostMultipleChoice;

    public static string CreateRuntimeMarker(int optionCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(optionCount);
        return $"{RuntimeMarkerPrefix}{optionCount}{RuntimeMarkerSuffix}";
    }

    public static bool TryParseRuntimeMarker(
        string? value,
        out int optionCount)
    {
        optionCount = 0;
        if (string.IsNullOrWhiteSpace(value) ||
            !value.StartsWith(RuntimeMarkerPrefix, StringComparison.Ordinal) ||
            !value.EndsWith(RuntimeMarkerSuffix, StringComparison.Ordinal))
        {
            return false;
        }

        var countText = value[
            RuntimeMarkerPrefix.Length..
            ^RuntimeMarkerSuffix.Length];
        return int.TryParse(countText, out optionCount) && optionCount >= 0;
    }

    public static MultipleChoiceAnswerLayout Split(
        QuestionPresentationType presentationType,
        IEnumerable<ContentBlockSnapshot>? answerBlocks)
    {
        var orderedBlocks = (answerBlocks ?? [])
            .OrderBy(block => block.SortOrder)
            .ToArray();

        if (!IsMultipleChoice(presentationType))
        {
            return new MultipleChoiceAnswerLayout(
                orderedBlocks,
                orderedBlocks,
                orderedBlocks,
                HasExplicitMarker: false,
                IsStructurallyValid: true);
        }

        if (orderedBlocks.Length > 0 &&
            orderedBlocks[0].Kind == ContentBlockKind.Text &&
            TryParseRuntimeMarker(
                orderedBlocks[0].TextContent,
                out var optionCount))
        {
            var availableBlocks = orderedBlocks.Length - 1;
            if (optionCount <= 0 || optionCount > availableBlocks)
            {
                return new MultipleChoiceAnswerLayout(
                    [],
                    orderedBlocks.Skip(1).ToArray(),
                    [],
                    HasExplicitMarker: true,
                    IsStructurallyValid: false);
            }

            var options = orderedBlocks
                .Skip(1)
                .Take(optionCount)
                .ToArray();
            var additionalBlocks = orderedBlocks
                .Skip(optionCount + 1)
                .ToArray();
            var revealBlocks = new[] { options[0] }
                .Concat(additionalBlocks)
                .ToArray();

            return new MultipleChoiceAnswerLayout(
                options,
                revealBlocks,
                orderedBlocks,
                HasExplicitMarker: true,
                IsStructurallyValid: true);
        }

        // Legacy snapshots stored only selectable options in AnswerBlocks.
        return new MultipleChoiceAnswerLayout(
            orderedBlocks,
            orderedBlocks.Take(1).ToArray(),
            orderedBlocks,
            HasExplicitMarker: false,
            IsStructurallyValid: true);
    }
}

public sealed record MultipleChoiceAnswerLayout(
    IReadOnlyList<ContentBlockSnapshot> OptionBlocks,
    IReadOnlyList<ContentBlockSnapshot> RevealBlocks,
    IReadOnlyList<ContentBlockSnapshot> StoredBlocks,
    bool HasExplicitMarker,
    bool IsStructurallyValid);
