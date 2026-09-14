using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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
    InvalidCsv,
    NotFound,
    LastRule,
    LastEnabledRule
}

public sealed record WordRingRule(
    Guid Id,
    WordRingColor Ring,
    string Text,
    IReadOnlyList<string> Words,
    bool? Enabled = null)
{
    [JsonIgnore]
    public bool IsEnabled => Enabled is not false;
}

public sealed record WordRingsWordItem(
    string Word,
    int BlueRuleCount,
    int YellowRuleCount,
    int RedRuleCount)
{
    public int TotalRuleCount => BlueRuleCount + YellowRuleCount + RedRuleCount;
}

public sealed record WordRingRuleMembershipItem(
    Guid Id,
    string Text,
    bool IsEnabled,
    bool ContainsWord);

public sealed record WordRingsImportRingSummary(
    int WordsAdded,
    int RulesCreated);

public sealed record WordRingsImportSummary(
    WordRingsImportRingSummary Blue,
    WordRingsImportRingSummary Yellow,
    WordRingsImportRingSummary Red)
{
    public bool HasChanges =>
        Blue.WordsAdded > 0 || Blue.RulesCreated > 0 ||
        Yellow.WordsAdded > 0 || Yellow.RulesCreated > 0 ||
        Red.WordsAdded > 0 || Red.RulesCreated > 0;

    public static WordRingsImportSummary Empty { get; } = new(
        new WordRingsImportRingSummary(0, 0),
        new WordRingsImportRingSummary(0, 0),
        new WordRingsImportRingSummary(0, 0));
}

public enum WordRingsCsvImportErrorKind
{
    FileRequired,
    FileTooLarge,
    EmptyFile,
    InvalidHeader,
    TooManyRows,
    MalformedRow,
    InvalidColumnCount,
    InvalidRing,
    InvalidRuleText,
    InvalidEnabled,
    InvalidWords,
    TooManyWords,
    UnplayableConfiguration
}

public sealed record WordRingsCsvImportError(
    int? LineNumber,
    WordRingsCsvImportErrorKind Kind,
    string? Value = null);

public sealed record WordRingsImportResult(
    WordRingRuleMutationResult Result,
    WordRingsImportSummary Summary,
    WordRingsCsvImportError? Error = null);

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
    private const int MinimumSingleWordLength = 3;
    private const int MaximumSingleWordLength = 120;
    private const int MaximumCsvRows = 10_000;

    private static readonly Regex WordListPattern = new(
        @"^ *[\p{L}\p{M}\p{N}'’\-]+(?:[ ,;]+[\p{L}\p{M}\p{N}'’\-]+)*[ ,;]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex WordSeparatorPattern = new(
        @"[ ,;]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex SingleWordPattern = new(
        @"^[\p{L}\p{M}\p{N}'’\-]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly CompareInfo WordCompareInfo =
        CultureInfo.GetCultureInfo("uk-UA").CompareInfo;

    private static readonly IComparer<string> WordAlphabeticalComparer =
        Comparer<string>.Create((left, right) =>
            WordCompareInfo.Compare(left, right, CompareOptions.IgnoreCase));

    private static readonly IReadOnlyList<WordRingRule> DefaultRules =
    [
        new(
            Guid.Parse("04f315e5-53c2-41f0-b5c3-ef267be0a47d"),
            WordRingColor.Blue,
            "Слово означає тварину",
            ["кіт", "вовк", "жаба", "собака", "песик", "олень", "панда", "коала"],
            true),
        new(
            Guid.Parse("948bb753-8a8c-4522-a848-2d6de8b6595c"),
            WordRingColor.Yellow,
            "У слові є літера «а»",
            ["ракета", "машина", "жаба", "собака", "лампа", "банка", "панда", "коала"],
            true),
        new(
            Guid.Parse("c2fd4315-8a39-4e37-99aa-9a046ff39e10"),
            WordRingColor.Red,
            "У слові рівно 5 літер",
            ["лісок", "човен", "песик", "олень", "лампа", "банка", "панда", "коала"],
            true)
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
    private StoreState _state;

    private WordRingsRuleStore(string contentRootPath)
    {
        _path = Path.Combine(contentRootPath, "App_Data", "word-rings-rules.json");
        _state = BuildState(LoadFromDisk());
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

    private StoreState Snapshot => Volatile.Read(ref _state);

    public IReadOnlyList<WordRingRule> GetRules(WordRingColor ring) =>
        Snapshot.Rules
            .Where(rule => rule.Ring == ring)
            .OrderBy(rule => rule.Text, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

    public int GetRuleCount(WordRingColor ring) =>
        Snapshot.Rules.Count(rule => rule.Ring == ring);

    public int GetRuleMembershipCount(
        WordRingColor ring,
        string? word,
        bool includedOnly)
    {
        var normalizedWord = NormalizeSingleWord(word);
        return Snapshot.Rules.Count(rule =>
            rule.Ring == ring &&
            (!includedOnly ||
             (normalizedWord is not null &&
              rule.Words.Contains(normalizedWord, StringComparer.OrdinalIgnoreCase))));
    }

    public int GetWordCount() => Snapshot.Words.Count;

    public IReadOnlyList<WordRingsWordItem> GetWordsPage(int skip, int take)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(skip);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(take);
        return Snapshot.Words.Skip(skip).Take(take).ToArray();
    }

    public IReadOnlyList<WordRingRuleMembershipItem> GetRuleMembershipPage(
        WordRingColor ring,
        string? word,
        bool includedOnly,
        int skip,
        int take)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(skip);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(take);

        var normalizedWord = NormalizeSingleWord(word);
        return Snapshot.Rules
            .Where(rule => rule.Ring == ring)
            .Where(rule =>
                !includedOnly ||
                (normalizedWord is not null &&
                 rule.Words.Contains(normalizedWord, StringComparer.OrdinalIgnoreCase)))
            .OrderBy(rule => rule.Text, StringComparer.CurrentCultureIgnoreCase)
            .Skip(skip)
            .Take(take)
            .Select(rule => new WordRingRuleMembershipItem(
                rule.Id,
                rule.Text,
                rule.IsEnabled,
                normalizedWord is not null &&
                rule.Words.Contains(normalizedWord, StringComparer.OrdinalIgnoreCase)))
            .ToArray();
    }

    public WordRingsPuzzle CreatePuzzle()
    {
        var activeSnapshot = Snapshot.Rules
            .Where(rule => rule.IsEnabled && rule.Words.Count > 0)
            .ToArray();
        var blue = Pick(activeSnapshot, WordRingColor.Blue);
        var yellow = Pick(activeSnapshot, WordRingColor.Yellow);
        var red = Pick(activeSnapshot, WordRingColor.Red);
        if (blue is null || yellow is null || red is null)
        {
            return new WordRingsPuzzle(
                string.Empty,
                string.Empty,
                string.Empty,
                [],
                new Dictionary<string, string>(StringComparer.Ordinal));
        }
        var selected = new[] { blue, yellow, red };

        var comparer = StringComparer.OrdinalIgnoreCase;
        var selectedWords = selected
            .SelectMany(rule => rule.Words)
            .Distinct(comparer)
            .ToList();

        var selectedIds = selected.Select(rule => rule.Id).ToHashSet();
        var outsideWords = activeSnapshot
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
        var normalizedText = NormalizeText(text);
        if (normalizedText is null)
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
            var rules = Snapshot.Rules;
            if (rules.Any(rule =>
                    rule.Ring == ring &&
                    string.Equals(rule.Text, normalizedText, StringComparison.OrdinalIgnoreCase)))
            {
                return WordRingRuleMutationResult.Duplicate;
            }

            var next = rules
                .Append(new WordRingRule(Guid.NewGuid(), ring, normalizedText, parsedWords, true))
                .ToArray();
            await PersistAsync(next);
            return WordRingRuleMutationResult.Success;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<WordRingRuleMutationResult> UpdateAsync(
        Guid id,
        string? text,
        string? words,
        CancellationToken cancellationToken)
    {
        var normalizedText = NormalizeText(text);
        if (normalizedText is null)
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
            var rules = Snapshot.Rules;
            var rule = rules.FirstOrDefault(item => item.Id == id);
            if (rule is null)
            {
                return WordRingRuleMutationResult.NotFound;
            }

            if (rules.Any(item =>
                    item.Id != id &&
                    item.Ring == rule.Ring &&
                    string.Equals(item.Text, normalizedText, StringComparison.OrdinalIgnoreCase)))
            {
                return WordRingRuleMutationResult.Duplicate;
            }

            var next = rules
                .Select(item => item.Id == id
                    ? item with { Text = normalizedText, Words = parsedWords }
                    : item)
                .ToArray();
            await PersistAsync(next);
            return WordRingRuleMutationResult.Success;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<WordRingRuleMutationResult> SetEnabledAsync(
        Guid id,
        bool enabled,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var rules = Snapshot.Rules;
            var rule = rules.FirstOrDefault(item => item.Id == id);
            if (rule is null)
            {
                return WordRingRuleMutationResult.NotFound;
            }

            if (enabled && rule.Words.Count == 0)
            {
                return WordRingRuleMutationResult.InvalidWords;
            }

            if (!enabled &&
                rule.IsEnabled &&
                rules.Count(item =>
                    item.Ring == rule.Ring &&
                    item.IsEnabled &&
                    item.Words.Count > 0) <= 1)
            {
                return WordRingRuleMutationResult.LastEnabledRule;
            }

            if (rule.IsEnabled == enabled)
            {
                return WordRingRuleMutationResult.Success;
            }

            var next = rules
                .Select(item => item.Id == id ? item with { Enabled = enabled } : item)
                .ToArray();
            await PersistAsync(next);
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
            var rules = Snapshot.Rules;
            var rule = rules.FirstOrDefault(item => item.Id == id);
            if (rule is null)
            {
                return WordRingRuleMutationResult.NotFound;
            }

            if (rules.Count(item => item.Ring == rule.Ring) <= 1)
            {
                return WordRingRuleMutationResult.LastRule;
            }

            if (rule.IsEnabled &&
                rule.Words.Count > 0 &&
                rules.Count(item =>
                    item.Ring == rule.Ring &&
                    item.IsEnabled &&
                    item.Words.Count > 0) <= 1)
            {
                return WordRingRuleMutationResult.LastEnabledRule;
            }

            var next = rules.Where(item => item.Id != id).ToArray();
            await PersistAsync(next);
            return WordRingRuleMutationResult.Success;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<WordRingRuleMutationResult> ApplyWordMembershipChangesAsync(
        string? word,
        IReadOnlyDictionary<Guid, bool> changes,
        CancellationToken cancellationToken)
    {
        var normalizedWord = NormalizeSingleWord(word);
        if (normalizedWord is null || changes.Count == 0)
        {
            return WordRingRuleMutationResult.InvalidWords;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var rules = Snapshot.Rules;
            var knownIds = rules.Select(rule => rule.Id).ToHashSet();
            if (changes.Keys.Any(id => !knownIds.Contains(id)))
            {
                return WordRingRuleMutationResult.NotFound;
            }

            var changed = false;
            var candidate = new List<WordRingRule>(rules.Count);
            foreach (var rule in rules)
            {
                if (!changes.TryGetValue(rule.Id, out var include))
                {
                    candidate.Add(rule);
                    continue;
                }

                var contains = rule.Words.Contains(
                    normalizedWord,
                    StringComparer.OrdinalIgnoreCase);
                if (contains == include)
                {
                    candidate.Add(rule);
                    continue;
                }

                if (include)
                {
                    if (rule.Words.Count >= MaximumWordsPerRule)
                    {
                        return WordRingRuleMutationResult.InvalidWords;
                    }

                    candidate.Add(rule with
                    {
                        Words = rule.Words.Append(normalizedWord).ToArray()
                    });
                }
                else
                {
                    candidate.Add(rule with
                    {
                        Words = rule.Words
                            .Where(item => !string.Equals(
                                item,
                                normalizedWord,
                                StringComparison.OrdinalIgnoreCase))
                            .ToArray()
                    });
                }
                changed = true;
            }

            if (!changed)
            {
                return WordRingRuleMutationResult.Success;
            }

            var playableResult = NormalizePlayableRules(candidate, out var next);
            if (playableResult != WordRingRuleMutationResult.Success)
            {
                return playableResult;
            }

            await PersistAsync(next);
            return WordRingRuleMutationResult.Success;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<WordRingRuleMutationResult> DeleteAllWordsAsync(
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var rules = Snapshot.Rules;
            if (!rules.Any(rule => rule.Words.Count > 0))
            {
                return WordRingRuleMutationResult.Success;
            }

            var next = rules
                .Select(rule => rule with { Words = Array.Empty<string>() })
                .ToArray();
            await PersistAsync(next);
            return WordRingRuleMutationResult.Success;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<WordRingRuleMutationResult> DeleteAllRulesAsync(
        WordRingColor ring,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var next = Snapshot.Rules
                .Where(rule => rule.Ring != ring)
                .ToArray();
            await PersistAsync(next);
            return WordRingRuleMutationResult.Success;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<WordRingRuleMutationResult> DeleteWordAsync(
        string? word,
        CancellationToken cancellationToken)
    {
        var normalizedWord = NormalizeSingleWord(word);
        if (normalizedWord is null)
        {
            return WordRingRuleMutationResult.InvalidWords;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var rules = Snapshot.Rules;
            if (!rules.Any(rule => rule.Words.Contains(
                    normalizedWord,
                    StringComparer.OrdinalIgnoreCase)))
            {
                return WordRingRuleMutationResult.NotFound;
            }

            var candidate = rules
                .Select(rule => rule with
                {
                    Words = rule.Words
                        .Where(item => !string.Equals(
                            item,
                            normalizedWord,
                            StringComparison.OrdinalIgnoreCase))
                        .ToArray()
                })
                .ToArray();

            var playableResult = NormalizePlayableRules(candidate, out var next);
            if (playableResult != WordRingRuleMutationResult.Success)
            {
                return playableResult;
            }

            await PersistAsync(next);
            return WordRingRuleMutationResult.Success;
        }
        finally
        {
            _gate.Release();
        }
    }

    public string ExportCsv()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Ring,Rule,Enabled,Words");

        foreach (var rule in Snapshot.Rules
                     .OrderBy(rule => rule.Ring)
                     .ThenBy(rule => rule.Text, StringComparer.CurrentCultureIgnoreCase))
        {
            builder.Append(CsvEscape(ToRingKey(rule.Ring))).Append(',')
                .Append(CsvEscape(rule.Text)).Append(',')
                .Append(CsvEscape(rule.IsEnabled ? "true" : "false")).Append(',')
                .Append(CsvEscape(string.Join("; ", rule.Words)))
                .AppendLine();
        }

        return builder.ToString();
    }

    public async Task<WordRingsImportResult> ImportCsvAsync(
        string? content,
        CancellationToken cancellationToken)
    {
        if (!TryParseCsv(content, out var parsed, out var parseError))
        {
            return new WordRingsImportResult(
                WordRingRuleMutationResult.InvalidCsv,
                WordRingsImportSummary.Empty,
                parseError);
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var rules = Snapshot.Rules;
            var next = rules.ToList();
            var wordsAdded = new int[3];
            var rulesCreated = new int[3];
            var changed = false;

            foreach (var row in parsed)
            {
                var index = next.FindIndex(rule =>
                    rule.Ring == row.Ring &&
                    string.Equals(rule.Text, row.Text, StringComparison.OrdinalIgnoreCase));
                var ringIndex = (int)row.Ring;

                if (index >= 0)
                {
                    var existing = next[index];
                    var additions = row.Words
                        .Where(word => !existing.Words.Contains(
                            word,
                            StringComparer.OrdinalIgnoreCase))
                        .ToArray();
                    if (existing.Words.Count + additions.Length > MaximumWordsPerRule)
                    {
                        return new WordRingsImportResult(
                            WordRingRuleMutationResult.InvalidWords,
                            WordRingsImportSummary.Empty,
                            new WordRingsCsvImportError(
                                row.SourceLineNumber,
                                WordRingsCsvImportErrorKind.TooManyWords));
                    }

                    var enabledChanged = existing.IsEnabled != row.Enabled;
                    if (additions.Length == 0 && !enabledChanged)
                    {
                        continue;
                    }

                    next[index] = existing with
                    {
                        Words = existing.Words.Concat(additions).ToArray(),
                        Enabled = row.Enabled
                    };
                    wordsAdded[ringIndex] += additions.Length;
                    changed = true;
                    continue;
                }

                next.Add(new WordRingRule(
                    Guid.NewGuid(),
                    row.Ring,
                    row.Text,
                    row.Words,
                    row.Enabled));
                wordsAdded[ringIndex] += row.Words.Count;
                rulesCreated[ringIndex] += 1;
                changed = true;
            }

            var summary = new WordRingsImportSummary(
                new WordRingsImportRingSummary(wordsAdded[(int)WordRingColor.Blue], rulesCreated[(int)WordRingColor.Blue]),
                new WordRingsImportRingSummary(wordsAdded[(int)WordRingColor.Yellow], rulesCreated[(int)WordRingColor.Yellow]),
                new WordRingsImportRingSummary(wordsAdded[(int)WordRingColor.Red], rulesCreated[(int)WordRingColor.Red]));

            if (changed)
            {
                var unplayableRing = Enum.GetValues<WordRingColor>()
                    .Where(ring => !next.Any(rule =>
                        rule.Ring == ring &&
                        rule.IsEnabled &&
                        rule.Words.Count > 0))
                    .Select(ring => (WordRingColor?)ring)
                    .FirstOrDefault();
                if (unplayableRing.HasValue)
                {
                    var sourceLine = parsed
                        .Where(row => row.Ring == unplayableRing.Value)
                        .Select(row => (int?)row.SourceLineNumber)
                        .Max();
                    return new WordRingsImportResult(
                        WordRingRuleMutationResult.LastEnabledRule,
                        WordRingsImportSummary.Empty,
                        new WordRingsCsvImportError(
                            sourceLine,
                            WordRingsCsvImportErrorKind.UnplayableConfiguration,
                            ToRingKey(unplayableRing.Value)));
                }

                var playableResult = NormalizePlayableRules(next, out var normalized);
                if (playableResult != WordRingRuleMutationResult.Success)
                {
                    return new WordRingsImportResult(
                        playableResult,
                        WordRingsImportSummary.Empty,
                        new WordRingsCsvImportError(
                            null,
                            WordRingsCsvImportErrorKind.UnplayableConfiguration));
                }

                await PersistAsync(normalized);
            }

            return new WordRingsImportResult(
                WordRingRuleMutationResult.Success,
                summary);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static string? NormalizeText(string? value)
    {
        var normalized = value?.Trim() ?? string.Empty;
        return normalized.Length is > 0 and <= MaximumRuleTextLength
            ? normalized
            : null;
    }

    private static string? NormalizeSingleWord(string? value)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length < MinimumSingleWordLength ||
            normalized.Length > MaximumSingleWordLength ||
            !SingleWordPattern.IsMatch(normalized))
        {
            return null;
        }

        return normalized;
    }

    private static IReadOnlyList<string>? ParseWords(string? value)
    {
        var input = value ?? string.Empty;
        if (input.Length == 0 ||
            input.Length > MaximumWordsInputLength ||
            !WordListPattern.IsMatch(input))
        {
            return null;
        }

        var candidates = WordSeparatorPattern
            .Split(input)
            .Where(word => !string.IsNullOrWhiteSpace(word))
            .ToArray();
        var normalizedWords = new List<string>(candidates.Length);
        foreach (var candidate in candidates)
        {
            var normalizedWord = NormalizeSingleWord(candidate);
            if (normalizedWord is null)
            {
                return null;
            }
            normalizedWords.Add(normalizedWord);
        }

        var words = normalizedWords
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
                    rule.Words is not null)
                .Select(rule => rule with
                {
                    Text = rule.Text.Trim(),
                    Words = rule.Words
                        .Where(word => !string.IsNullOrWhiteSpace(word))
                        .Select(word => word.Trim())
                        .Where(word => NormalizeSingleWord(word) is not null)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Take(MaximumWordsPerRule)
                        .ToArray()
                })
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
            if (!result.Any(rule => rule.Ring == ring))
            {
                result.Add(DefaultRules.Single(rule => rule.Ring == ring));
            }

            if (result.Any(rule =>
                    rule.Ring == ring &&
                    rule.IsEnabled &&
                    rule.Words.Count > 0))
            {
                continue;
            }

            var playableIndex = result.FindIndex(rule =>
                rule.Ring == ring && rule.Words.Count > 0);
            if (playableIndex >= 0)
            {
                result[playableIndex] = result[playableIndex] with { Enabled = true };
                continue;
            }

            var fallback = DefaultRules.Single(rule => rule.Ring == ring);
            var existingFallback = result.FindIndex(rule => rule.Id == fallback.Id);
            if (existingFallback >= 0)
            {
                result[existingFallback] = fallback;
            }
            else
            {
                result.Add(fallback);
            }
        }
        return result;
    }

    private static WordRingRuleMutationResult NormalizePlayableRules(
        IEnumerable<WordRingRule> source,
        out WordRingRule[] normalized)
    {
        var candidate = source
            .Select(rule => rule.IsEnabled && rule.Words.Count == 0
                ? rule with { Enabled = false }
                : rule)
            .ToArray();

        foreach (var ring in Enum.GetValues<WordRingColor>())
        {
            if (!candidate.Any(rule =>
                    rule.Ring == ring &&
                    rule.IsEnabled &&
                    rule.Words.Count > 0))
            {
                normalized = [];
                return WordRingRuleMutationResult.LastEnabledRule;
            }
        }

        normalized = candidate;
        return WordRingRuleMutationResult.Success;
    }

    private static WordRingRule? Pick(
        IReadOnlyList<WordRingRule> rules,
        WordRingColor ring)
    {
        var candidates = rules
            .Where(rule =>
                rule.Ring == ring &&
                rule.IsEnabled &&
                rule.Words.Count > 0)
            .ToArray();
        return candidates.Length == 0
            ? null
            : candidates[Random.Shared.Next(candidates.Length)];
    }

    private static void Shuffle<T>(IList<T> items)
    {
        for (var index = items.Count - 1; index > 0; index--)
        {
            var swapIndex = Random.Shared.Next(index + 1);
            (items[index], items[swapIndex]) = (items[swapIndex], items[index]);
        }
    }

    private static StoreState BuildState(IReadOnlyList<WordRingRule> rules)
    {
        var usage = new Dictionary<string, WordUsageBuilder>(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in rules)
        {
            foreach (var word in rule.Words)
            {
                if (!usage.TryGetValue(word, out var item))
                {
                    item = new WordUsageBuilder(word);
                    usage[word] = item;
                }

                item.Increment(rule.Ring);
            }
        }

        var words = usage.Values
            .Select(item => item.ToItem())
            .OrderBy(item => item.Word, WordAlphabeticalComparer)
            .ThenBy(item => item.Word, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new StoreState(rules, words);
    }

    private async Task PersistAsync(IReadOnlyList<WordRingRule> rules)
    {
        await WriteAsync(rules);
        Volatile.Write(ref _state, BuildState(rules));
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

    private static bool TryParseCsv(
        string? content,
        out IReadOnlyList<CsvRuleRow> parsedRows,
        out WordRingsCsvImportError? error)
    {
        parsedRows = [];
        error = null;
        if (string.IsNullOrWhiteSpace(content))
        {
            error = new WordRingsCsvImportError(
                1,
                WordRingsCsvImportErrorKind.EmptyFile);
            return false;
        }

        using var reader = new StringReader(content);
        var headerLine = reader.ReadLine();
        if (headerLine is null)
        {
            error = new WordRingsCsvImportError(
                1,
                WordRingsCsvImportErrorKind.EmptyFile);
            return false;
        }

        var header = ParseCsvLine(headerLine);
        if (header is null)
        {
            error = new WordRingsCsvImportError(
                1,
                WordRingsCsvImportErrorKind.MalformedRow);
            return false;
        }
        if (header.Length != 4 ||
            !string.Equals(header[0].TrimStart('\uFEFF'), "Ring", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(header[1], "Rule", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(header[2], "Enabled", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(header[3], "Words", StringComparison.OrdinalIgnoreCase))
        {
            error = new WordRingsCsvImportError(
                1,
                WordRingsCsvImportErrorKind.InvalidHeader);
            return false;
        }

        var rows = new Dictionary<string, CsvRuleRow>(StringComparer.OrdinalIgnoreCase);
        var dataRowCount = 0;
        var lineNumber = 1;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber += 1;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            dataRowCount += 1;
            if (dataRowCount > MaximumCsvRows)
            {
                error = new WordRingsCsvImportError(
                    lineNumber,
                    WordRingsCsvImportErrorKind.TooManyRows);
                return false;
            }

            var fields = ParseCsvLine(line);
            if (fields is null)
            {
                error = new WordRingsCsvImportError(
                    lineNumber,
                    WordRingsCsvImportErrorKind.MalformedRow);
                return false;
            }
            if (fields.Length != 4)
            {
                error = new WordRingsCsvImportError(
                    lineNumber,
                    WordRingsCsvImportErrorKind.InvalidColumnCount,
                    fields.Length.ToString(CultureInfo.InvariantCulture));
                return false;
            }
            if (!TryParseRingKey(fields[0], out var ring))
            {
                error = new WordRingsCsvImportError(
                    lineNumber,
                    WordRingsCsvImportErrorKind.InvalidRing,
                    fields[0].Trim());
                return false;
            }

            var text = NormalizeText(fields[1]);
            if (text is null)
            {
                error = new WordRingsCsvImportError(
                    lineNumber,
                    WordRingsCsvImportErrorKind.InvalidRuleText);
                return false;
            }
            if (!TryParseBoolean(fields[2], out var enabled))
            {
                error = new WordRingsCsvImportError(
                    lineNumber,
                    WordRingsCsvImportErrorKind.InvalidEnabled,
                    fields[2].Trim());
                return false;
            }
            if (!TryParseCsvWords(fields[3], out var words, out var wordsError))
            {
                error = new WordRingsCsvImportError(
                    lineNumber,
                    wordsError);
                return false;
            }

            var key = $"{(int)ring}\u001f{text}";
            if (!rows.TryGetValue(key, out var existing))
            {
                rows[key] = new CsvRuleRow(
                    ring,
                    text,
                    enabled,
                    words,
                    lineNumber);
                continue;
            }

            var mergedWords = existing.Words
                .Concat(words)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (mergedWords.Length > MaximumWordsPerRule)
            {
                error = new WordRingsCsvImportError(
                    lineNumber,
                    WordRingsCsvImportErrorKind.TooManyWords);
                return false;
            }

            rows[key] = existing with
            {
                Enabled = enabled,
                Words = mergedWords,
                SourceLineNumber = lineNumber
            };
        }

        parsedRows = rows.Values.ToArray();
        return true;
    }

    private static bool TryParseCsvWords(
        string? value,
        out IReadOnlyList<string> words,
        out WordRingsCsvImportErrorKind errorKind)
    {
        words = [];
        errorKind = WordRingsCsvImportErrorKind.InvalidWords;
        var input = value ?? string.Empty;
        if (input.Length == 0 ||
            input.Length > MaximumWordsInputLength ||
            !WordListPattern.IsMatch(input))
        {
            return false;
        }

        var candidates = WordSeparatorPattern
            .Split(input)
            .Where(word => !string.IsNullOrWhiteSpace(word))
            .ToArray();
        var normalizedWords = new List<string>(candidates.Length);
        foreach (var candidate in candidates)
        {
            var normalizedWord = NormalizeSingleWord(candidate);
            if (normalizedWord is null)
            {
                return false;
            }
            normalizedWords.Add(normalizedWord);
        }

        var distinct = normalizedWords
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (distinct.Length == 0)
        {
            return false;
        }
        if (distinct.Length > MaximumWordsPerRule)
        {
            errorKind = WordRingsCsvImportErrorKind.TooManyWords;
            return false;
        }

        words = distinct;
        return true;
    }

    private static string[]? ParseCsvLine(string line)
    {
        var fields = new List<string>(4);
        var builder = new StringBuilder();
        var quoted = false;

        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                if (quoted && index + 1 < line.Length && line[index + 1] == '"')
                {
                    builder.Append('"');
                    index += 1;
                }
                else
                {
                    quoted = !quoted;
                }
                continue;
            }

            if (character == ',' && !quoted)
            {
                fields.Add(builder.ToString());
                builder.Clear();
                continue;
            }

            builder.Append(character);
        }

        if (quoted)
        {
            return null;
        }

        fields.Add(builder.ToString());
        return fields.ToArray();
    }

    private static bool TryParseBoolean(string value, out bool result)
    {
        var normalized = value.Trim();
        if (bool.TryParse(normalized, out result))
        {
            return true;
        }

        if (normalized == "1")
        {
            result = true;
            return true;
        }

        if (normalized == "0")
        {
            result = false;
            return true;
        }

        result = false;
        return false;
    }

    private static bool TryParseRingKey(string value, out WordRingColor ring)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "blue":
                ring = WordRingColor.Blue;
                return true;
            case "yellow":
                ring = WordRingColor.Yellow;
                return true;
            case "red":
                ring = WordRingColor.Red;
                return true;
            default:
                ring = WordRingColor.Blue;
                return false;
        }
    }

    private static string ToRingKey(WordRingColor ring) => ring switch
    {
        WordRingColor.Yellow => "yellow",
        WordRingColor.Red => "red",
        _ => "blue"
    };

    private static string CsvEscape(string value) =>
        $"\"{value.Replace("\"", "\"\"")}\"";

    private sealed record StoreState(
        IReadOnlyList<WordRingRule> Rules,
        IReadOnlyList<WordRingsWordItem> Words);

    private sealed record CsvRuleRow(
        WordRingColor Ring,
        string Text,
        bool Enabled,
        IReadOnlyList<string> Words,
        int SourceLineNumber);

    private sealed class WordUsageBuilder(string word)
    {
        private int _blue;
        private int _yellow;
        private int _red;

        public void Increment(WordRingColor ring)
        {
            switch (ring)
            {
                case WordRingColor.Blue:
                    _blue += 1;
                    break;
                case WordRingColor.Yellow:
                    _yellow += 1;
                    break;
                case WordRingColor.Red:
                    _red += 1;
                    break;
            }
        }

        public WordRingsWordItem ToItem() => new(word, _blue, _yellow, _red);
    }
}
