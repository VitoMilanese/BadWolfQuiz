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
                .Select(word => new MatchingWordCandidate(
                    word,
                    puzzle.Expected.TryGetValue(word, out var membership)
                        ? membership
                        : string.Empty))
                .Where(candidate => !string.IsNullOrEmpty(candidate.Membership))
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
            var selected = SelectBalancedMatchingWords(
                    matchingWords,
                    minimumMatchingCount)
                .ToList();
            var selectedSet = selected.ToHashSet(StringComparer.OrdinalIgnoreCase);

            selected.AddRange(
                outsideWords.Take(Math.Max(0, targetCount - selected.Count)));
            if (selected.Count < targetCount)
            {
                selected.AddRange(
                    matchingWords
                        .Where(candidate => !selectedSet.Contains(candidate.Word))
                        .Select(candidate => candidate.Word)
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

    private static IReadOnlyList<string> SelectBalancedMatchingWords(
        IReadOnlyList<MatchingWordCandidate> candidates,
        int count)
    {
        if (count <= 0 || candidates.Count == 0)
        {
            return [];
        }

        var remaining = candidates
            .OrderBy(_ => Random.Shared.Next())
            .ToList();
        var selected = new List<string>(Math.Min(count, remaining.Count));
        var ringUse = new Dictionary<char, int>
        {
            ['A'] = 0,
            ['B'] = 0,
            ['C'] = 0
        };

        while (selected.Count < count && remaining.Count > 0)
        {
            var currentMax = ringUse.Values.Max();
            MatchingWordCandidate? best = null;
            var bestScore = double.MinValue;

            foreach (var candidate in remaining)
            {
                var balanceBonus = candidate.Membership
                    .Where(ringUse.ContainsKey)
                    .Sum(ring => (currentMax - ringUse[ring]) * 55.0);
                var overlapBonus = candidate.Membership.Length switch
                {
                    >= 3 => 360.0,
                    2 => 220.0,
                    _ => 100.0
                };
                var score = overlapBonus + balanceBonus + (Random.Shared.NextDouble() * 12.0);
                if (score <= bestScore)
                {
                    continue;
                }

                best = candidate;
                bestScore = score;
            }

            if (best is null)
            {
                break;
            }

            selected.Add(best.Word);
            foreach (var ring in best.Membership.Where(ringUse.ContainsKey))
            {
                ringUse[ring]++;
            }
            remaining.Remove(best);
        }

        return selected;
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

    private sealed record MatchingWordCandidate(string Word, string Membership);
}
