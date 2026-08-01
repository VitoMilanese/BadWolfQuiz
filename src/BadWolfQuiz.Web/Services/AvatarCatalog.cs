namespace BadWolfQuiz.Web.Services;

public sealed class AvatarCatalog(IWebHostEnvironment environment)
{
    private static readonly string[] SupportedExtensions =
        [".png", ".webp", ".jpg", ".jpeg"];

    private readonly string _root = ResolveRootPath(environment);

    public static string ResolveRootPath(IHostEnvironment environment)
    {
        var outputRoot = Path.Combine(
            AppContext.BaseDirectory,
            "Resources",
            "Avatars");

        return Directory.Exists(outputRoot)
            ? outputRoot
            : Path.Combine(
                environment.ContentRootPath,
                "Resources",
                "Avatars");
    }

    public bool IsValid(string? avatarId)
    {
        if (string.IsNullOrWhiteSpace(avatarId))
        {
            return false;
        }

        var parts = avatarId.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 ||
            Path.GetFileName(parts[0]) != parts[0] ||
            Path.GetFileName(parts[1]) != parts[1] ||
            !IsSupportedImage(parts[1]) ||
            !GetCategories().Any(category =>
                string.Equals(category.Id, parts[0], StringComparison.Ordinal)))
        {
            return false;
        }

        return File.Exists(Path.Combine(_root, parts[0], parts[1]));
    }

    public IReadOnlyList<AvatarCategory> GetCategories()
    {
        if (!Directory.Exists(_root))
        {
            return Array.Empty<AvatarCategory>();
        }

        return Directory.EnumerateDirectories(_root)
            .Select(CreateCategory)
            .Where(category => category is not null)
            .Select(category => category!)
            .OrderBy(category => CategorySortOrder(category.Id))
            .ThenBy(category => category.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private AvatarCategory? CreateCategory(string categoryDirectory)
    {
        var categoryId = Path.GetFileName(categoryDirectory);
        var avatars = Directory
            .EnumerateFiles(categoryDirectory, "*", SearchOption.TopDirectoryOnly)
            .Where(IsSupportedImage)
            .Select(Path.GetFileName)
            .Where(fileName => !string.IsNullOrWhiteSpace(fileName))
            .OrderBy(fileName => ParseNumericFileName(fileName!), Comparer<int?>.Create(
                (left, right) => left.HasValue && right.HasValue
                    ? left.Value.CompareTo(right.Value)
                    : left.HasValue ? -1 : right.HasValue ? 1 : 0))
            .ThenBy(fileName => fileName, StringComparer.OrdinalIgnoreCase)
            .Select(fileName => new AvatarOption(
                $"{categoryId}/{fileName}",
                GetFileVersion(Path.Combine(categoryDirectory, fileName!))))
            .ToArray();

        if (avatars.Length == 0)
        {
            return null;
        }

        var iconFileName = Directory
            .EnumerateFiles(_root, "*", SearchOption.TopDirectoryOnly)
            .Where(IsSupportedImage)
            .Select(Path.GetFileName)
            .FirstOrDefault(fileName => string.Equals(
                Path.GetFileNameWithoutExtension(fileName),
                categoryId,
                StringComparison.OrdinalIgnoreCase));

        return iconFileName is null
            ? null
            : new AvatarCategory(
                categoryId,
                iconFileName,
                GetFileVersion(Path.Combine(_root, iconFileName)),
                avatars);
    }

    private static bool IsSupportedImage(string path) =>
        SupportedExtensions.Contains(
            Path.GetExtension(path),
            StringComparer.OrdinalIgnoreCase);

    private static int CategorySortOrder(string categoryId) => categoryId switch
    {
        "F" => 0,
        "M" => 1,
        "I" => 2,
        _ => 3
    };

    private static int? ParseNumericFileName(string fileName) =>
        int.TryParse(
            Path.GetFileNameWithoutExtension(fileName),
            out var number)
            ? number
            : null;

    private static string GetFileVersion(string path)
    {
        var file = new FileInfo(path);
        return $"{file.Length:x}-{file.LastWriteTimeUtc.Ticks:x}";
    }
}

public sealed record AvatarCategory(
    string Id,
    string IconFileName,
    string IconVersion,
    IReadOnlyList<AvatarOption> Avatars);

public sealed record AvatarOption(string Id, string Version);
