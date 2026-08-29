using Microsoft.Extensions.Hosting;

namespace BadWolfQuiz.Web.Services;

public sealed class MinigameCardSetStore
{
    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".png",
            ".jpg",
            ".jpeg",
            ".webp",
            ".gif"
        };

    private readonly object _sync = new();
    private readonly string _rootPath;
    private readonly int _cardCount;
    private long _version;
    private MinigameCardSetSnapshot? _current;

    public MinigameCardSetStore(string rootPath, int cardCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cardCount);

        _rootPath = Path.GetFullPath(rootPath);
        _cardCount = cardCount;
    }

    public int DefaultCardCount => _cardCount;

    public int AvailableCardCount => DiscoverCards().Count;

    public static string ResolveRootPath(IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        return Path.Combine(
            environment.ContentRootPath,
            "Resources",
            "Minigames",
            "GameCards");
    }

    public IReadOnlyList<MinigameCardDescriptor> GenerateCards(int cardCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cardCount);

        var cards = DiscoverCards().ToList();
        if (cardCount > cards.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cardCount),
                cardCount,
                "The requested card count exceeds the available image count.");
        }

        Shuffle(cards);
        return cards.Take(cardCount).ToArray();
    }

    // Kept for compatibility with the first #445 implementation while the
    // room-based game becomes the only UI consumer.
    public MinigameCardSetSnapshot GetCurrent()
    {
        lock (_sync)
        {
            return _current ??= GenerateNext();
        }
    }

    public MinigameCardSetSnapshot Regenerate()
    {
        lock (_sync)
        {
            _current = GenerateNext();
            return _current;
        }
    }

    public bool TryResolveCard(
        string? fileName,
        out string physicalPath,
        out string contentType)
    {
        physicalPath = string.Empty;
        contentType = string.Empty;

        if (string.IsNullOrWhiteSpace(fileName) ||
            fileName.Contains('/') ||
            fileName.Contains('\\') ||
            !SupportedExtensions.Contains(Path.GetExtension(fileName)))
        {
            return false;
        }

        var candidate = Path.GetFullPath(Path.Combine(_rootPath, fileName));
        var rootPrefix = _rootPath.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (!candidate.StartsWith(rootPrefix, comparison) || !File.Exists(candidate))
        {
            return false;
        }

        physicalPath = candidate;
        contentType = GetContentType(Path.GetExtension(candidate));
        return true;
    }

    private MinigameCardSetSnapshot GenerateNext()
    {
        var cards = DiscoverCards().ToList();
        Shuffle(cards);

        var selected = cards
            .Take(Math.Min(_cardCount, cards.Count))
            .ToArray();

        return new MinigameCardSetSnapshot(++_version, selected);
    }

    private IReadOnlyList<MinigameCardDescriptor> DiscoverCards()
    {
        if (!Directory.Exists(_rootPath))
        {
            return [];
        }

        try
        {
            return Directory
                .EnumerateFiles(_rootPath, "*", SearchOption.TopDirectoryOnly)
                .Where(path => SupportedExtensions.Contains(Path.GetExtension(path)))
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .Select(path => new MinigameCardDescriptor(
                    Path.GetFileName(path),
                    Path.GetFileNameWithoutExtension(path)))
                .ToArray();
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static void Shuffle<T>(IList<T> items)
    {
        for (var index = items.Count - 1; index > 0; index--)
        {
            var swapIndex = Random.Shared.Next(index + 1);
            (items[index], items[swapIndex]) = (items[swapIndex], items[index]);
        }
    }

    private static string GetContentType(string extension) =>
        extension.ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "application/octet-stream"
        };
}

public sealed record MinigameCardDescriptor(
    string FileName,
    string DisplayName);

public sealed record MinigameCardSetSnapshot(
    long Version,
    IReadOnlyList<MinigameCardDescriptor> Cards);
