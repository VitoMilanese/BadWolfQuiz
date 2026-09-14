using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BadWolfQuiz.Web.Pages;

public sealed class WordRingsModel(IWebHostEnvironment environment) : PageModel
{
    private const int MaximumDisplayedWords = 10;
    private const int PuzzleRefreshAttempts = 24;

    public WordRingsPuzzle Puzzle { get; private set; } = null!;
    public IReadOnlyList<string> DisplayedWords { get; private set; } = [];
    public IReadOnlyDictionary<string, string> DisplayedExpected { get; private set; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public void OnGet(
        string? previousBlueRule,
        string? previousYellowRule,
        string? previousRedRule,
        string? previousWords)
    {
        var store = WordRingsRuleStore.Get(environment);
        Puzzle = PickFreshPuzzle(
            store,
            previousBlueRule,
            previousYellowRule,
            previousRedRule);

        var previousWordList = ParsePreviousWords(previousWords);
        DisplayedWords = BuildDisplayedWords(Puzzle, previousWordList);
        DisplayedExpected = Puzzle.Expected
            .Where(item => DisplayedWords.Contains(item.Key, StringComparer.Ordinal))
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
    }

    private static WordRingsPuzzle PickFreshPuzzle(
        WordRingsRuleStore store,
        string? previousBlueRule,
        string? previousYellowRule,
        string? previousRedRule)
    {
        var previousRules = new[]
        {
            previousBlueRule?.Trim(),
            previousYellowRule?.Trim(),
            previousRedRule?.Trim()
        };
        var requestedDifferences = previousRules.Count(rule => !string.IsNullOrWhiteSpace(rule));
        WordRingsPuzzle? best = null;
        var bestDifferenceCount = -1;

        for (var attempt = 0; attempt < PuzzleRefreshAttempts; attempt++)
        {
            var candidate = store.CreatePuzzle();
            var currentRules = new[]
            {
                candidate.BlueRuleText,
                candidate.YellowRuleText,
                candidate.RedRuleText
            };
            var differenceCount = 0;
            for (var index = 0; index < previousRules.Length; index++)
            {
                if (!string.IsNullOrWhiteSpace(previousRules[index]) &&
                    !string.Equals(
                        previousRules[index],
                        currentRules[index],
                        StringComparison.OrdinalIgnoreCase))
                {
                    differenceCount++;
                }
            }

            if (differenceCount > bestDifferenceCount)
            {
                best = candidate;
                bestDifferenceCount = differenceCount;
            }

            if (requestedDifferences == 0 || differenceCount == requestedDifferences)
            {
                return candidate;
            }
        }

        return best ?? store.CreatePuzzle();
    }

    private static IReadOnlyList<string> BuildDisplayedWords(
        WordRingsPuzzle puzzle,
        IReadOnlyList<string> previousWords)
    {
        if (puzzle.Words.Count == 0)
        {
            return [];
        }

        string[] last = [];
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var matchingWords = puzzle.Words
                .Where(word =>
                    puzzle.Expected.TryGetValue(word, out var membership) &&
                    !string.IsNullOrEmpty(membership))
                .OrderBy(_ => Random.Shared.Next())
                .ToList();
            var outsideWords = puzzle.Words
                .Where(word =>
                    !puzzle.Expected.TryGetValue(word, out var membership) ||
                    string.IsNullOrEmpty(membership))
                .OrderBy(_ => Random.Shared.Next())
                .ToList();

            var targetCount = Math.Min(MaximumDisplayedWords, puzzle.Words.Count);
            var minimumMatchingCount = Math.Min(
                matchingWords.Count,
                (int)Math.Ceiling(targetCount * 0.8));
            var selected = matchingWords.Take(minimumMatchingCount).ToList();

            selected.AddRange(
                outsideWords.Take(Math.Max(0, targetCount - selected.Count)));
            if (selected.Count < targetCount)
            {
                selected.AddRange(
                    matchingWords
                        .Skip(minimumMatchingCount)
                        .Take(targetCount - selected.Count));
            }

            last = selected
                .OrderBy(_ => Random.Shared.Next())
                .Take(MaximumDisplayedWords)
                .ToArray();

            if (!HaveSameWords(last, previousWords))
            {
                return last;
            }
        }

        if (last.Length > 1 && previousWords.Count == last.Length &&
            last.SequenceEqual(previousWords, StringComparer.OrdinalIgnoreCase))
        {
            last = last.Skip(1).Append(last[0]).ToArray();
        }

        return last;
    }

    private static IReadOnlyList<string> ParsePreviousWords(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value
                .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Take(MaximumDisplayedWords)
                .ToArray();

    private static bool HaveSameWords(
        IReadOnlyCollection<string> left,
        IReadOnlyCollection<string> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        var comparer = StringComparer.OrdinalIgnoreCase;
        return left.All(word => right.Contains(word, comparer));
    }
}
