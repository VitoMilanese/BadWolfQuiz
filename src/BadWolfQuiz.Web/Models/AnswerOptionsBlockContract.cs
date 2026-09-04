using System.Globalization;
using BadWolfQuiz.Game.Definitions;

namespace BadWolfQuiz.Web.Models;

public static class AnswerOptionsBlockContract
{
    private const char CorrectOptionsSeparator = '|';

    public static int ParseOptionCount(string? storedValue)
    {
        var countText = storedValue?.Split(
            CorrectOptionsSeparator,
            count: 2,
            StringSplitOptions.None)[0];
        return int.TryParse(
                countText,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var optionCount) &&
            optionCount >= 0
                ? optionCount
                : 0;
    }

    public static IReadOnlyList<int> ParseCorrectOptionIndexes(
        string? storedValue,
        int optionCount)
    {
        if (optionCount <= 0)
        {
            return [];
        }

        var parts = storedValue?.Split(
            CorrectOptionsSeparator,
            count: 2,
            StringSplitOptions.None) ?? [];
        if (parts.Length < 2)
        {
            // Existing quizzes stored only the option count and implicitly used
            // the first option as the single correct answer.
            return [0];
        }

        if (string.IsNullOrWhiteSpace(parts[1]))
        {
            return [];
        }

        var indexes = new List<int>();
        foreach (var token in parts[1].Split(',', StringSplitOptions.None))
        {
            if (!int.TryParse(
                    token,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var index) ||
                index < 0 ||
                index >= optionCount)
            {
                return [];
            }

            indexes.Add(index);
        }

        return indexes
            .Distinct()
            .OrderBy(index => index)
            .ToArray();
    }

    public static string StoreOptionCount(int optionCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(optionCount);
        return optionCount.ToString(CultureInfo.InvariantCulture);
    }

    public static string StoreOptionState(
        int optionCount,
        IEnumerable<int>? correctOptionIndexes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(optionCount);

        var indexes = (correctOptionIndexes ?? [])
            .Distinct()
            .OrderBy(index => index)
            .ToArray();
        if (indexes.Any(index => index < 0 || index >= optionCount))
        {
            throw new ArgumentOutOfRangeException(
                nameof(correctOptionIndexes),
                "Correct answer-option indexes must reference an available option.");
        }

        if (indexes.Length == 1 && indexes[0] == 0)
        {
            return StoreOptionCount(optionCount);
        }

        return $"{StoreOptionCount(optionCount)}{CorrectOptionsSeparator}" +
            string.Join(',', indexes.Select(index =>
                index.ToString(CultureInfo.InvariantCulture)));
    }

    public static string CreateRuntimeMarker(string? storedValue)
    {
        var optionCount = ParseOptionCount(storedValue);
        return MultipleChoiceAnswerContract.CreateRuntimeMarker(
            optionCount,
            ParseCorrectOptionIndexes(storedValue, optionCount));
    }
}