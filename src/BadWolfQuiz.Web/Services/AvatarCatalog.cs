namespace BadWolfQuiz.Web.Services;

public sealed class AvatarCatalog(IWebHostEnvironment environment)
{
    private static readonly HashSet<string> Categories =
        new(StringComparer.Ordinal) { "F", "M", "I" };

    private readonly string _root = Path.Combine(
        environment.ContentRootPath,
        "Resources",
        "Avatars");

    public bool IsValid(string? avatarId)
    {
        if (string.IsNullOrWhiteSpace(avatarId))
        {
            return false;
        }

        var parts = avatarId.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 ||
            !Categories.Contains(parts[0]) ||
            Path.GetFileName(parts[1]) != parts[1] ||
            !string.Equals(Path.GetExtension(parts[1]), ".png", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return File.Exists(Path.Combine(_root, parts[0], parts[1]));
    }

    public IReadOnlyList<string> GetAvatarIds(string category)
    {
        if (!Categories.Contains(category))
        {
            return Array.Empty<string>();
        }

        var categoryDirectory = Path.Combine(_root, category);
        if (!Directory.Exists(categoryDirectory))
        {
            return Array.Empty<string>();
        }

        return Directory
            .EnumerateFiles(categoryDirectory, "*", SearchOption.TopDirectoryOnly)
            .Where(path => string.Equals(
                Path.GetExtension(path),
                ".png",
                StringComparison.OrdinalIgnoreCase))
            .Select(Path.GetFileName)
            .Where(fileName => !string.IsNullOrWhiteSpace(fileName))
            .OrderBy(fileName => ParseNumericFileName(fileName!), Comparer<int?>.Create(
                (left, right) => left.HasValue && right.HasValue
                    ? left.Value.CompareTo(right.Value)
                    : left.HasValue ? -1 : right.HasValue ? 1 : 0))
            .ThenBy(fileName => fileName, StringComparer.OrdinalIgnoreCase)
            .Select(fileName => $"{category}/{fileName}")
            .ToArray();
    }

    private static int? ParseNumericFileName(string fileName) =>
        int.TryParse(
            Path.GetFileNameWithoutExtension(fileName),
            out var number)
            ? number
            : null;
}
