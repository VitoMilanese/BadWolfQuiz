namespace BadWolfQuiz.Web.Services;

internal static class WordRingsRoomRuleSelection
{
    private static readonly string[] DefaultOutsideWords = ["дім", "сир"];

    public static WordRingsPuzzle CreatePuzzle(
        IWebHostEnvironment environment,
        Guid blueRuleId,
        Guid yellowRuleId,
        Guid redRuleId)
    {
        var store = WordRingsRuleStore.Get(environment);
        var blue = FindRule(store, WordRingColor.Blue, blueRuleId);
        var yellow = FindRule(store, WordRingColor.Yellow, yellowRuleId);
        var red = FindRule(store, WordRingColor.Red, redRuleId);
        if (blue is null || yellow is null || red is null)
        {
            return EmptyPuzzle();
        }

        var activeRules = Enum.GetValues<WordRingColor>()
            .SelectMany(ring => store.GetRules(ring))
            .Where(rule => rule.IsEnabled && rule.Words.Count > 0)
            .ToArray();
        return BuildPuzzle(activeRules, blue, yellow, red);
    }

    private static WordRingRule? FindRule(
        WordRingsRuleStore store,
        WordRingColor ring,
        Guid id) =>
        store.GetRules(ring).FirstOrDefault(rule =>
            rule.Id == id &&
            rule.IsEnabled &&
            rule.Words.Count > 0);

    private static WordRingsPuzzle BuildPuzzle(
        IReadOnlyList<WordRingRule> activeRules,
        WordRingRule blue,
        WordRingRule yellow,
        WordRingRule red)
    {
        var selected = new[] { blue, yellow, red };
        var comparer = StringComparer.OrdinalIgnoreCase;
        var selectedWords = selected
            .SelectMany(rule => rule.Words)
            .Distinct(comparer)
            .ToList();

        var selectedIds = selected.Select(rule => rule.Id).ToHashSet();
        var outsideWords = activeRules
            .Where(rule => !selectedIds.Contains(rule.Id))
            .SelectMany(rule => rule.Words)
            .Distinct(comparer)
            .Where(word => !selectedWords.Contains(word, comparer))
            .OrderBy(_ => Random.Shared.Next())
            .Take(2)
            .ToList();

        foreach (var fallback in DefaultOutsideWords)
        {
            if (outsideWords.Count >= 2)
            {
                break;
            }
            if (!selectedWords.Contains(fallback, comparer) &&
                !outsideWords.Contains(fallback, comparer))
            {
                outsideWords.Add(fallback);
            }
        }

        var words = selectedWords.Concat(outsideWords).Distinct(comparer).ToList();
        Shuffle(words);

        var expected = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var word in words)
        {
            expected[word] = string.Concat(
                blue.Words.Contains(word, comparer) ? "A" : string.Empty,
                yellow.Words.Contains(word, comparer) ? "B" : string.Empty,
                red.Words.Contains(word, comparer) ? "C" : string.Empty);
        }

        return new WordRingsPuzzle(
            blue.Text,
            yellow.Text,
            red.Text,
            words,
            expected);
    }

    private static WordRingsPuzzle EmptyPuzzle() => new(
        string.Empty,
        string.Empty,
        string.Empty,
        [],
        new Dictionary<string, string>(StringComparer.Ordinal));

    private static void Shuffle<T>(IList<T> items)
    {
        for (var index = items.Count - 1; index > 0; index--)
        {
            var swapIndex = Random.Shared.Next(index + 1);
            (items[index], items[swapIndex]) = (items[swapIndex], items[index]);
        }
    }
}
