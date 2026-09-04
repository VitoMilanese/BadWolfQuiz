namespace BadWolfQuiz.Game.Definitions;

public static class MultipleChoiceAnswerContract
{
    public const string RuntimeMarkerPrefix = "__badwolf_answer_options:";
    public const string RuntimeMarkerSuffix = "__";

    public static bool IsMultipleChoice(QuestionPresentationType presentationType) =>
        presentationType is
            QuestionPresentationType.AllPlayerMultipleChoice or
            QuestionPresentationType.HostMultipleChoice;

    public static string CreateRuntimeMarker(int optionCount) =>
        CreateRuntimeMarker(
            optionCount,
            optionCount > 0 ? [0] : []);

    public static string CreateRuntimeMarker(
        int optionCount,
        IEnumerable<int>? correctOptionIndexes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(optionCount);

        var normalizedCorrectIndexes = (correctOptionIndexes ?? [])
            .Distinct()
            .OrderBy(index => index)
            .ToArray();
        if (normalizedCorrectIndexes.Any(index => index < 0 || index >= optionCount))
        {
            throw new ArgumentOutOfRangeException(
                nameof(correctOptionIndexes),
                "Correct answer-option indexes must reference an available option.");
        }

        var payload = normalizedCorrectIndexes.Length == 1 &&
            normalizedCorrectIndexes[0] == 0
                ? optionCount.ToString()
                : $"{optionCount}|{string.Join(',', normalizedCorrectIndexes)}";
        return $"{RuntimeMarkerPrefix}{payload}{RuntimeMarkerSuffix}";
    }

    public static bool TryParseRuntimeMarker(
        string? value,
        out int optionCount) =>
        TryParseRuntimeMarker(value, out optionCount, out _);

    public static bool TryParseRuntimeMarker(
        string? value,
        out int optionCount,
        out int[] correctOptionIndexes)
    {
        optionCount = 0;
        correctOptionIndexes = [];
        if (string.IsNullOrWhiteSpace(value) ||
            !value.StartsWith(RuntimeMarkerPrefix, StringComparison.Ordinal) ||
            !value.EndsWith(RuntimeMarkerSuffix, StringComparison.Ordinal))
        {
            return false;
        }

        var payload = value[
            RuntimeMarkerPrefix.Length..
            ^RuntimeMarkerSuffix.Length];
        var separatorIndex = payload.IndexOf('|');
        var countText = separatorIndex < 0
            ? payload
            : payload[..separatorIndex];
        if (!int.TryParse(countText, out optionCount) || optionCount < 0)
        {
            optionCount = 0;
            return false;
        }

        if (separatorIndex < 0)
        {
            correctOptionIndexes = optionCount > 0 ? [0] : [];
            return true;
        }

        var correctText = payload[(separatorIndex + 1)..];
        if (string.IsNullOrWhiteSpace(correctText))
        {
            return true;
        }

        var parsedIndexes = new List<int>();
        foreach (var token in correctText.Split(',', StringSplitOptions.None))
        {
            if (!int.TryParse(token, out var index) || index < 0)
            {
                optionCount = 0;
                correctOptionIndexes = [];
                return false;
            }

            parsedIndexes.Add(index);
        }

        correctOptionIndexes = parsedIndexes
            .Distinct()
            .OrderBy(index => index)
            .ToArray();
        return true;
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
                [],
                HasExplicitMarker: false,
                IsStructurallyValid: true);
        }

        if (orderedBlocks.Length > 0 &&
            orderedBlocks[0].Kind == ContentBlockKind.Text &&
            TryParseRuntimeMarker(
                orderedBlocks[0].TextContent,
                out var optionCount,
                out var parsedCorrectOptionIndexes))
        {
            var availableBlocks = orderedBlocks.Length - 1;
            if (optionCount <= 0 || optionCount > availableBlocks)
            {
                return new MultipleChoiceAnswerLayout(
                    [],
                    orderedBlocks.Skip(1).ToArray(),
                    orderedBlocks,
                    [],
                    HasExplicitMarker: true,
                    IsStructurallyValid: false);
            }

            var options = orderedBlocks
                .Skip(1)
                .Take(optionCount)
                .ToArray();
            var correctOptionIndexes = presentationType ==
                    QuestionPresentationType.HostMultipleChoice
                ? new[] { 0 }
                : parsedCorrectOptionIndexes;
            var correctIndexSet = correctOptionIndexes.ToHashSet();
            var correctIndexesAreValid =
                correctOptionIndexes.Length > 0 &&
                correctOptionIndexes.All(index =>
                    index >= 0 && index < options.Length) &&
                correctIndexSet.Count == correctOptionIndexes.Length;
            if (!correctIndexesAreValid)
            {
                return new MultipleChoiceAnswerLayout(
                    options,
                    [],
                    orderedBlocks,
                    [],
                    HasExplicitMarker: true,
                    IsStructurallyValid: false);
            }

            var correctOptions = options
                .Where((_, index) => correctIndexSet.Contains(index))
                .ToArray();
            var additionalBlocks = orderedBlocks
                .Skip(optionCount + 1)
                .ToArray();
            var revealBlocks = correctOptions
                .Concat(additionalBlocks)
                .ToArray();

            return new MultipleChoiceAnswerLayout(
                options,
                revealBlocks,
                orderedBlocks,
                correctOptions,
                HasExplicitMarker: true,
                IsStructurallyValid: true);
        }

        // Legacy snapshots stored only selectable options in AnswerBlocks and
        // implicitly treated the first option as the single correct answer.
        var legacyCorrectOptions = orderedBlocks.Take(1).ToArray();
        return new MultipleChoiceAnswerLayout(
            orderedBlocks,
            legacyCorrectOptions,
            orderedBlocks,
            legacyCorrectOptions,
            HasExplicitMarker: false,
            IsStructurallyValid: true);
    }
}

public sealed record MultipleChoiceAnswerLayout(
    IReadOnlyList<ContentBlockSnapshot> OptionBlocks,
    IReadOnlyList<ContentBlockSnapshot> RevealBlocks,
    IReadOnlyList<ContentBlockSnapshot> StoredBlocks,
    IReadOnlyList<ContentBlockSnapshot> CorrectOptionBlocks,
    bool HasExplicitMarker,
    bool IsStructurallyValid);