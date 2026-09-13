using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BadWolfQuiz.Web.Services;

public enum WordRingColor
{
    Blue,
    Yellow,
    Red
}

public enum WordRingRuleMutationResult
{
    Success,
    Duplicate,
    InvalidText,
    InvalidWords,
    NotFound,
    LastRule
}

public sealed record WordRingRule(
    Guid Id,
    WordRingColor Ring,
    string Text,
    IReadOnlyList<string> Words);

public sealed record WordRingsPuzzle(
    string BlueRuleText,
    string YellowRuleText,
    string RedRuleText,
    IReadOnlyList<string> Words,
    IReadOnlyDictionary<string, string> Expected);

public sealed class WordRingsRuleStore
{
    private const int MaximumRuleTextLength = 300;
    private const int MaximumWordsInputLength = 12_000;
    private const int MaximumWordsPerRule = 250;

    private static readonly Regex WordListPattern = new(
        @"^\s*[\p{L}\p{M}\p{N}'’\-]+(?:[\s,;]+[\p{L}\p{M}\p{N}'’\-]+)*[\s,;]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex WordSeparatorPattern = new(
        @"[\s,;]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly IReadOnlyList<WordRingRule> DefaultRules =
    [
        new(
            Guid.Parse("04f315e5-53c2-41f0-b5c3-ef267be0a47d"),
            WordRingColor.Blue,
            "Слово означає тварину",
            ["кіт", "вовк", "жаба", "собака", "песик", "олень", "панда", "коала"]),
        new(
            Guid.Parse("948bb753-8a8c-4522-a848-2d6de8b6595c"),
            WordRingColor.Yellow,
            "У слові є літера «а»",
            ["ракета", "машина", "жаба", "собака", "лампа", "банка", "панда", "коала"]),
        new(
            Guid.Parse("c2fd4315-8a39-4e37-99aa-9a046ff39e10"),
            WordRingColor.Red,
            "У слові рівно 5 літер",
            ["лісок", "човен", "песик", "олень", "лампа", "банка", "панда", "коала"])
    ];

    private static readonly string[] DefaultOutsideWords = ["дім", "сир"];
    private static readonly ConcurrentDictionary<string, Lazy<WordRingsRuleStore>> Instances =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
    private IReadOnlyList<WordRingRule> _rules;

    private WordRingsRuleStore(string contentRootPath)
    {
        _path = Path.Combine(contentRootPath, "App_Data", "word-rings-rules.json");
        _rules = EnsureEveryRingHasRule(LoadFromDisk());
    }

    public static WordRingsRuleStore Get(IWebHostEnvironment environment)
    {
        var root = Path.GetFullPath(environment.ContentRootPath);
        return Instances.GetOrAdd(
            root,
            path => new Lazy<WordRingsRuleStore>(
                () => new WordRingsRuleStore(path),
                LazyThreadSafetyMode.ExecutionAndPublication)).Value;
    }

    public IReadOnlyList<WordRingRule> GetRules(WordRingColor ring) =>
        _rules
            .Where(rule => rule.Ring == ring)
            .OrderBy(rule => rule.Text, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

    public WordRingsPuzzle CreatePuzzle()
    {
        var snapshot = EnsureEveryRingHasRule(_rules);
        var blue = Pick(snapshot, WordRingColor.Blue);
        var yellow = Pick(snapshot, WordRingColor.Yellow);
        var red = Pick(snapshot, WordRingColor.Red);
        var selected = new[] { blue, yellow, red };

        var comparer = StringComparer.OrdinalIgnoreCase;
        var selectedWords = selected
            .SelectMany(rule => rule.Words)
            .Distinct(comparer)
            .ToList();

        var selectedIds = selected.Select(rule => rule.Id).ToHashSet();
        var outsideWords = snapshot
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
            var membership = string.Concat(
                blue.Words.Contains(word, comparer) ? "A" : string.Empty,
                yellow.Words.Contains(word, comparer) ? "B" : string.Empty,
                red.Words.Contains(word, comparer) ? "C" : string.Empty);
            expected[word] = membership;
        }

        return new WordRingsPuzzle(
            blue.Text,
            yellow.Text,
            red.Text,
            words,
            expected);
    }

    public async Task<WordRingRuleMutationResult> AddAsync(
        WordRingColor ring,
        string? text,
        string? words,
        CancellationToken cancellationToken)
    {
        var normalizedText = text?.Trim() ?? string.Empty;
        if (normalizedText.Length is 0 or > MaximumRuleTextLength)
        {
            return WordRingRuleMutationResult.InvalidText;
        }

        var parsedWords = ParseWords(words);
        if (parsedWords is null)
        {
            return WordRingRuleMutationResult.InvalidWords;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_rules.Any(rule =>
                    rule.Ring == ring &&
                    string.Equals(rule.Text, normalizedText, StringComparison.OrdinalIgnoreCase)))
            {
                return WordRingRuleMutationResult.Duplicate;
            }

            var next = _rules
                .Append(new WordRingRule(Guid.NewGuid(), ring, normalizedText, parsedWords))
                .ToArray();
            await WriteAsync(next);
            _rules = next;
            return WordRingRuleMutationResult.Success;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<WordRingRuleMutationResult> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var rule = _rules.FirstOrDefault(item => item.Id == id);
            if (rule is null)
            {
                return WordRingRuleMutationResult.NotFound;
            }

            if (_rules.Count(item => item.Ring == rule.Ring) <= 1)
            {
                return WordRingRuleMutationResult.LastRule;
            }

            var next = _rules.Where(item => item.Id != id).ToArray();
            await WriteAsync(next);
            _rules = next;
            return WordRingRuleMutationResult.Success;
        }
        finally
        {
            _gate.Release();
        }
    }

    private IReadOnlyList<string>? ParseWords(string? value)
    {
        var input = value?.Trim() ?? string.Empty;
        if (input.Length == 0 ||
            input.Length > MaximumWordsInputLength ||
            !WordListPattern.IsMatch(input))
        {
            return null;
        }

        var words = WordSeparatorPattern
            .Split(input)
            .Where(word => !string.IsNullOrWhiteSpace(word))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return words.Length is > 0 and <= MaximumWordsPerRule
            ? words
            : null;
    }

    private IReadOnlyList<WordRingRule> LoadFromDisk()
    {
        if (!File.Exists(_path))
        {
            return DefaultRules.ToArray();
        }

        try
        {
            using var stream = File.OpenRead(_path);
            var rules = JsonSerializer.Deserialize<WordRingRule[]>(stream, _jsonOptions) ?? [];
            return rules
                .Where(rule =>
                    Enum.IsDefined(rule.Ring) &&
                    rule.Id != Guid.Empty &&
                    !string.IsNullOrWhiteSpace(rule.Text) &&
                    rule.Words is { Count: > 0 })
                .Select(rule => rule with
                {
                    Text = rule.Text.Trim(),
                    Words = rule.Words
                        .Where(word => !string.IsNullOrWhiteSpace(word))
                        .Select(word => word.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray()
                })
                .Where(rule => rule.Words.Count > 0)
                .ToArray();
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            JsonException or
            NotSupportedException)
        {
            return DefaultRules.ToArray();
        }
    }

    private static IReadOnlyList<WordRingRule> EnsureEveryRingHasRule(
        IReadOnlyList<WordRingRule> source)
    {
        var result = source.ToList();
        foreach (var ring in Enum.GetValues<WordRingColor>())
        {
            if (result.Any(rule => rule.Ring == ring))
            {
                continue;
            }

            result.Add(DefaultRules.Single(rule => rule.Ring == ring));
        }
        return result;
    }

    private static WordRingRule Pick(
        IReadOnlyList<WordRingRule> rules,
        WordRingColor ring)
    {
        var candidates = rules.Where(rule => rule.Ring == ring).ToArray();
        return candidates[Random.Shared.Next(candidates.Length)];
    }

    private static void Shuffle<T>(IList<T> items)
    {
        for (var index = items.Count - 1; index > 0; index--)
        {
            var swapIndex = Random.Shared.Next(index + 1);
            (items[index], items[swapIndex]) = (items[swapIndex], items[index]);
        }
    }

    private async Task WriteAsync(IReadOnlyList<WordRingRule> rules)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temporaryPath = _path + ".tmp";
        await using (var stream = new FileStream(
            temporaryPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 16 * 1024,
            useAsync: true))
        {
            await JsonSerializer.SerializeAsync(
                stream,
                rules,
                _jsonOptions,
                CancellationToken.None);
            await stream.FlushAsync(CancellationToken.None);
        }

        File.Move(temporaryPath, _path, overwrite: true);
    }
}
