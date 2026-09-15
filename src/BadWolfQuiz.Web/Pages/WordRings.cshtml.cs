using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BadWolfQuiz.Web.Pages;

public sealed class WordRingsModel(IWebHostEnvironment environment) : PageModel
{
    private const int MaximumBankWords = 10;
    private const int MaximumGameWords = 20;
    private const int CorrectWordsToWin = 10;
    private const int PuzzleSelectionAttempts = 32;

    public WordRingsPuzzle Puzzle { get; private set; } = null!;
    public IReadOnlyList<string> DisplayedWords { get; private set; } = [];
    public IReadOnlyList<string> InitialWords { get; private set; } = [];
    public IReadOnlyList<string> QueuedWords { get; private set; } = [];
    public IReadOnlyList<WordRingsSeedWord> SeedWords { get; private set; } = [];
    public IReadOnlyDictionary<string, string> DisplayedExpected { get; private set; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public int BankWordLimit => MaximumBankWords;
    public int GameWordLimit => MaximumGameWords;
    public int CorrectWordTarget => CorrectWordsToWin;
    public string AchievementSessionId { get; private set; } = Guid.NewGuid().ToString("N");

    public void OnGet(
        string? previousBlueRule,
        string? previousYellowRule,
        string? previousRedRule,
        string? previousWords,
        string? room)
    {
        ApplyRoomBranding(room);
        var store = WordRingsRuleStore.Get(environment);
        var previousWordList = ParsePreviousWords(previousWords);
        var selection = PickFreshPuzzle(
            store,
            previousBlueRule,
            previousYellowRule,
            previousRedRule,
            previousWordList);

        Puzzle = selection.Puzzle;
        DisplayedWords = selection.Words;
        InitialWords = DisplayedWords.Take(MaximumBankWords).ToArray();
        QueuedWords = DisplayedWords.Skip(MaximumBankWords).ToArray();
        SeedWords = selection.SeedWords;
        DisplayedExpected = Puzzle.Expected
            .Where(item => DisplayedWords.Contains(item.Key, StringComparer.Ordinal))
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
    }

    private void ApplyRoomBranding(string? roomCode)
    {
        var normalized = (roomCode ?? string.Empty).Trim().ToUpperInvariant();
        if (normalized.Length != 6 || !normalized.All(char.IsLetterOrDigit))
        {
            return;
        }

        var logo = WordRingsRoomHostCoordinator.Get(environment).GetBrandLogo(normalized);
        if (logo is null)
        {
            return;
        }

        ViewData["HeaderBrandLogoUrl"] = Url.Page(
            "/WordRingsRoomApi",
            "RoomBrandLogo",
            new { roomCode = normalized });
    }

    public IActionResult OnGetNewPuzzle(
        string? previousBlueRule,
        string? previousYellowRule,
        string? previousRedRule,
        string? previousWords)
    {
        var store = WordRingsRuleStore.Get(environment);
        var selection = PickFreshPuzzle(
            store,
            previousBlueRule,
            previousYellowRule,
            previousRedRule,
            ParsePreviousWords(previousWords));
        var expected = selection.Puzzle.Expected
            .Where(item => selection.Words.Contains(item.Key, StringComparer.Ordinal))
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);

        return new JsonResult(new
        {
            achievementSessionId = Guid.NewGuid().ToString("N"),
            blueRuleText = selection.Puzzle.BlueRuleText,
            yellowRuleText = selection.Puzzle.YellowRuleText,
            redRuleText = selection.Puzzle.RedRuleText,
            words = selection.Words,
            seeds = selection.SeedWords,
            expected
        });
    }

    private static PuzzleSelection PickFreshPuzzle(
        WordRingsRuleStore store,
        string? previousBlueRule,
        string? previousYellowRule,
        string? previousRedRule,
        IReadOnlyList<string> previousWords)
    {
        var previousRules = new[]
        {
            previousBlueRule?.Trim(),
            previousYellowRule?.Trim(),
            previousRedRule?.Trim()
        };

        PuzzleSelection? best = null;
        for (var attempt = 0; attempt < PuzzleSelectionAttempts; attempt++)
        {
            var candidate = store.CreatePuzzle();
            var seedWords = WordRingsSeedWordSelector.SelectAutomatic(candidate);
            var excludedWords = seedWords.Select(item => item.Word).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var candidateWords = BuildDisplayedWordsExcludingSeeds(candidate, previousWords, excludedWords);
            var differenceCount = CountRuleDifferences(candidate, previousRules);
            var qualityScore = ScoreDisplayedWords(candidate, candidateWords) + ScoreSeedWords(seedWords);

            if (best is null ||
                differenceCount > best.RuleDifferenceCount ||
                (differenceCount == best.RuleDifferenceCount && qualityScore > best.QualityScore))
            {
                best = new PuzzleSelection(
                    candidate,
                    candidateWords,
                    seedWords,
                    differenceCount,
                    qualityScore);
            }
        }

        if (best is not null)
        {
            return best;
        }

        var fallback = store.CreatePuzzle();
        var fallbackSeeds = WordRingsSeedWordSelector.SelectAutomatic(fallback);
        var fallbackExcluded = fallbackSeeds.Select(item => item.Word).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return new PuzzleSelection(
            fallback,
            BuildDisplayedWordsExcludingSeeds(fallback, previousWords, fallbackExcluded),
            fallbackSeeds,
            0,
            int.MinValue);
    }

    private static int CountRuleDifferences(
        WordRingsPuzzle puzzle,
        IReadOnlyList<string?> previousRules)
    {
        var currentRules = new[]
        {
            puzzle.BlueRuleText,
            puzzle.YellowRuleText,
            puzzle.RedRuleText
        };
        var differenceCount = 0;
        for (var index = 0; index < currentRules.Length; index++)
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

        return differenceCount;
    }

    private static int ScoreDisplayedWords(
        WordRingsPuzzle puzzle,
        IReadOnlyList<string> words)
    {
        var ringUse = new Dictionary<char, int>
        {
            ['A'] = 0,
            ['B'] = 0,
            ['C'] = 0
        };
        var matchingCount = 0;
        var dualCount = 0;
        var tripleCount = 0;
        var regions = new HashSet<string>(StringComparer.Ordinal);

        foreach (var word in words)
        {
            if (!puzzle.Expected.TryGetValue(word, out var membership) ||
                string.IsNullOrEmpty(membership))
            {
                continue;
            }

            matchingCount++;
            regions.Add(membership);
            if (membership.Length >= 3)
            {
                tripleCount++;
            }
            else if (membership.Length == 2)
            {
                dualCount++;
            }

            foreach (var ring in membership.Where(ringUse.ContainsKey))
            {
                ringUse[ring]++;
            }
        }

        if (matchingCount == 0)
        {
            return int.MinValue / 2;
        }

        var minimumRingUse = ringUse.Values.Min();
        var maximumRingUse = ringUse.Values.Max();
        var representedRings = ringUse.Values.Count(value => value > 0);
        var spread = maximumRingUse - minimumRingUse;

        return
            (matchingCount * 200) +
            (tripleCount * 1500) +
            (dualCount * 900) +
            (representedRings * 600) +
            (minimumRingUse * 250) +
            (regions.Count * 100) -
            (spread * 350);
    }

    private static int ScoreSeedWords(IReadOnlyList<WordRingsSeedWord> seeds)
    {
        var memberships = seeds.Select(item => item.Membership).ToHashSet(StringComparer.Ordinal);
        var primary = new[] { "A", "B", "C", string.Empty }.Count(memberships.Contains);
        var shared = memberships.Contains("ABC")
            ? 2
            : memberships.Any(item => item.Length == 2) ? 1 : 0;
        return (primary * 5000) + (shared * 2500);
    }

    private static IReadOnlyList<string> BuildDisplayedWords(
        WordRingsPuzzle puzzle,
        IReadOnlyList<string> previousWords) =>
        BuildDisplayedWordsExcludingSeeds(
            puzzle,
            previousWords,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase));

    private static IReadOnlyList<string> BuildDisplayedWordsExcludingSeeds(
        WordRingsPuzzle puzzle,
        IReadOnlyList<string> previousWords,
        IReadOnlySet<string> excludedWords)
    {
        if (puzzle.Words.Count == 0)
        {
            return [];
        }

        string[] last = [];
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var matchingWords = puzzle.Words
                .Where(word => !excludedWords.Contains(word))
                .Select(word => new MatchingWordCandidate(
                    word,
                    puzzle.Expected.TryGetValue(word, out var membership)
                        ? membership
                        : string.Empty))
                .Where(candidate => !string.IsNullOrEmpty(candidate.Membership))
                .OrderBy(_ => Random.Shared.Next())
                .ToList();
            var outsideWords = puzzle.Words
                .Where(word => !excludedWords.Contains(word))
                .Where(word =>
                    !puzzle.Expected.TryGetValue(word, out var membership) ||
                    string.IsNullOrEmpty(membership))
                .OrderBy(_ => Random.Shared.Next())
                .ToList();

            var targetCount = Math.Min(
                MaximumGameWords,
                puzzle.Words.Count(word => !excludedWords.Contains(word)));
            var minimumMatchingCount = Math.Min(
                matchingWords.Count,
                (int)Math.Ceiling(targetCount * 0.8));
            var outsideCount = Math.Min(
                outsideWords.Count,
                Math.Max(0, targetCount - minimumMatchingCount));
            var matchingCount = Math.Min(
                matchingWords.Count,
                targetCount - outsideCount);

            var selectedMatching = SelectBalancedMatchingWords(
                    matchingWords,
                    matchingCount)
                .ToArray();
            var selectedOutside = outsideWords
                .Take(Math.Max(0, targetCount - selectedMatching.Length))
                .ToArray();

            last = InterleaveOutsideWords(
                    selectedMatching,
                    selectedOutside,
                    targetCount)
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

    private static IReadOnlyList<string> InterleaveOutsideWords(
        IReadOnlyList<string> matching,
        IReadOnlyList<string> outside,
        int targetCount)
    {
        var totalCount = Math.Min(targetCount, matching.Count + outside.Count);
        if (totalCount == 0)
        {
            return [];
        }

        var result = new List<string>(totalCount);
        var matchingIndex = 0;
        var outsideIndex = 0;
        var outsideCount = Math.Min(outside.Count, totalCount);

        for (var index = 0; index < totalCount; index++)
        {
            var outsideBefore = (index * outsideCount) / totalCount;
            var outsideAfter = ((index + 1) * outsideCount) / totalCount;
            var shouldUseOutside =
                outsideAfter > outsideBefore &&
                outsideIndex < outside.Count;

            if (shouldUseOutside)
            {
                result.Add(outside[outsideIndex++]);
                continue;
            }

            if (matchingIndex < matching.Count)
            {
                result.Add(matching[matchingIndex++]);
                continue;
            }

            if (outsideIndex < outside.Count)
            {
                result.Add(outside[outsideIndex++]);
            }
        }

        return result;
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
        var regionUse = new Dictionary<string, int>(StringComparer.Ordinal);

        while (selected.Count < count && remaining.Count > 0)
        {
            var minimumUse = ringUse.Values.Min();
            MatchingWordCandidate? best = null;
            var bestScore = double.MinValue;

            foreach (var candidate in remaining)
            {
                var membershipRings = candidate.Membership
                    .Where(ringUse.ContainsKey)
                    .Distinct()
                    .ToArray();
                var underrepresentedRings = membershipRings.Count(ring => ringUse[ring] == minimumUse);
                var balanceBonus = underrepresentedRings * 1000.0;
                var overlapBonus = candidate.Membership.Length switch
                {
                    >= 3 => 750.0,
                    2 => 450.0,
                    _ => 0.0
                };
                var saturationPenalty = membershipRings.Sum(ring => ringUse[ring]) * 90.0;
                var existingRegionUse = regionUse.GetValueOrDefault(candidate.Membership);
                var regionVarietyBonus = existingRegionUse == 0
                    ? 180.0
                    : -(existingRegionUse * 60.0);
                var score =
                    balanceBonus +
                    overlapBonus +
                    regionVarietyBonus -
                    saturationPenalty +
                    (Random.Shared.NextDouble() * 10.0);

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
            foreach (var ring in best.Membership.Where(ringUse.ContainsKey).Distinct())
            {
                ringUse[ring]++;
            }
            regionUse[best.Membership] = regionUse.GetValueOrDefault(best.Membership) + 1;
            remaining.Remove(best);
        }

        return selected;
    }

    private static IReadOnlyList<string> ParsePreviousWords(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value
                .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Take(MaximumGameWords)
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

    private sealed record PuzzleSelection(
        WordRingsPuzzle Puzzle,
        IReadOnlyList<string> Words,
        IReadOnlyList<WordRingsSeedWord> SeedWords,
        int RuleDifferenceCount,
        int QualityScore);
}
