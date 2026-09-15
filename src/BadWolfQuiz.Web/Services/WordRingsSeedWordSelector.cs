namespace BadWolfQuiz.Web.Services;

public sealed record WordRingsSeedWord(string Word, string Membership);

public static class WordRingsSeedWordSelector
{
    public static IReadOnlyList<WordRingsSeedWord> SelectAutomatic(WordRingsPuzzle puzzle)
    {
        var result = new List<WordRingsSeedWord>(5);
        foreach (var membership in new[] { "A", "B", "C", string.Empty })
        {
            AddExact(result, puzzle, membership);
        }

        var shared = FindExact(puzzle, "ABC") ??
                     puzzle.Words
                         .Select(word => ToSeed(puzzle, word))
                         .FirstOrDefault(seed => seed.Membership.Length == 2);
        if (shared is not null && result.All(item => !string.Equals(item.Word, shared.Word, StringComparison.OrdinalIgnoreCase)))
        {
            result.Add(shared);
        }

        return result;
    }

    public static IReadOnlyList<WordRingsSeedWord> SelectHostSetup(WordRingsPuzzle puzzle, int count = 4)
    {
        var target = Math.Max(0, Math.Min(count, puzzle.Words.Count));
        var result = new List<WordRingsSeedWord>(target);
        foreach (var membership in new[] { "A", "B", "C", string.Empty })
        {
            if (result.Count >= target) break;
            AddExact(result, puzzle, membership);
        }

        foreach (var word in puzzle.Words)
        {
            if (result.Count >= target) break;
            if (result.Any(item => string.Equals(item.Word, word, StringComparison.OrdinalIgnoreCase))) continue;
            result.Add(ToSeed(puzzle, word));
        }

        return result;
    }

    private static void AddExact(List<WordRingsSeedWord> result, WordRingsPuzzle puzzle, string membership)
    {
        var seed = FindExact(puzzle, membership);
        if (seed is null || result.Any(item => string.Equals(item.Word, seed.Word, StringComparison.OrdinalIgnoreCase))) return;
        result.Add(seed);
    }

    private static WordRingsSeedWord? FindExact(WordRingsPuzzle puzzle, string membership) =>
        puzzle.Words
            .Select(word => ToSeed(puzzle, word))
            .FirstOrDefault(seed => string.Equals(seed.Membership, membership, StringComparison.Ordinal));

    private static WordRingsSeedWord ToSeed(WordRingsPuzzle puzzle, string word) =>
        new(word, CanonicalMembership(puzzle.Expected.TryGetValue(word, out var membership) ? membership : string.Empty));

    private static string CanonicalMembership(string value) =>
        string.Concat(value
            .Where(ch => ch is 'A' or 'B' or 'C')
            .Distinct()
            .OrderBy(ch => ch));
}
