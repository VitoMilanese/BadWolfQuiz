using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace BadWolfQuiz.Web.Services;

public static class ContributorRecognition
{
    public const string RecognitionCookieName = "badwolfquiz.contributor-thanks";

    public static bool IsContributor(FooterOptions options, string? name)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        return options.GetContributors().Any(contributor =>
            string.Equals(
                contributor,
                name.Trim(),
                StringComparison.OrdinalIgnoreCase));
    }

    public static string GetRecognitionCookieName(string hostId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostId);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(hostId.Trim()));
        return $"{RecognitionCookieName}.{Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    public static bool ShouldShowThankYou(
        FooterOptions options,
        string? name,
        string hostId,
        HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return IsContributor(options, name) &&
            !request.Cookies.ContainsKey(GetRecognitionCookieName(hostId));
    }

    public static void MarkThankYouShown(
        HttpResponse response,
        string hostId,
        bool secure)
    {
        ArgumentNullException.ThrowIfNull(response);
        response.Cookies.Append(
            GetRecognitionCookieName(hostId),
            "1",
            new CookieOptions
            {
                HttpOnly = true,
                IsEssential = true,
                MaxAge = TimeSpan.FromDays(3650),
                Path = "/",
                SameSite = SameSiteMode.Lax,
                Secure = secure
            });
    }
}

public static class ContributorAvatarFrameCatalog
{
    public const string DefaultId = "1";

    public static IReadOnlyList<ContributorAvatarFrame> GetFrames(
        IHostEnvironment environment)
    {
        var root = ResolveRootPath(environment);
        if (!Directory.Exists(root))
        {
            return Array.Empty<ContributorAvatarFrame>();
        }

        return Directory
            .EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly)
            .Where(path => string.Equals(
                Path.GetExtension(path),
                ".png",
                StringComparison.OrdinalIgnoreCase))
            .Select(path => new
            {
                Path = path,
                Id = Path.GetFileNameWithoutExtension(path)
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Id))
            .OrderBy(
                item => ParseNumericId(item.Id),
                Comparer<int?>.Create((left, right) =>
                    left.HasValue && right.HasValue
                        ? left.Value.CompareTo(right.Value)
                        : left.HasValue ? -1 : right.HasValue ? 1 : 0))
            .ThenBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .Select(item => new ContributorAvatarFrame(
                item.Id,
                BuildUrl(item.Id, item.Path)))
            .ToArray();
    }

    public static string ResolveRootPath(IHostEnvironment environment)
    {
        var contentRoot = Path.Combine(
            environment.ContentRootPath,
            "Resources",
            "Frames");
        if (Directory.Exists(contentRoot))
        {
            return contentRoot;
        }

        return Path.Combine(
            AppContext.BaseDirectory,
            "Resources",
            "Frames");
    }

    public static bool IsValid(
        IHostEnvironment environment,
        string? id) =>
        TryResolvePath(environment, id, out _);

    public static bool IsValid(string? id) => IsSafeId(id);

    public static string? Normalize(
        IHostEnvironment environment,
        string? id)
    {
        if (TryResolvePath(environment, id, out _))
        {
            return id!.Trim();
        }

        return GetFrames(environment).FirstOrDefault()?.Id;
    }

    public static string? GetUrl(
        IHostEnvironment environment,
        string? id)
    {
        var normalizedId = Normalize(environment, id);
        return normalizedId is not null &&
               TryResolvePath(environment, normalizedId, out var path)
            ? BuildUrl(normalizedId, path)
            : null;
    }

    public static bool TryResolvePath(
        IHostEnvironment environment,
        string? id,
        out string path) =>
        TryResolvePath(ResolveRootPath(environment), id, out path);

    private static bool TryResolvePath(
        string root,
        string? id,
        out string path)
    {
        path = string.Empty;
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        if (!IsSafeId(id))
        {
            return false;
        }

        var normalizedId = id!.Trim();

        if (!Directory.Exists(root))
        {
            return false;
        }

        var file = Directory
            .EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly)
            .FirstOrDefault(candidate =>
                string.Equals(
                    Path.GetExtension(candidate),
                    ".png",
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    Path.GetFileNameWithoutExtension(candidate),
                    normalizedId,
                    StringComparison.Ordinal));
        if (file is null)
        {
            return false;
        }

        path = file;
        return true;
    }

    private static bool IsSafeId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        var normalizedId = id.Trim();
        return normalizedId is not "." and not ".." &&
               string.Equals(
                   Path.GetFileName(normalizedId),
                   normalizedId,
                   StringComparison.Ordinal);
    }

    private static int? ParseNumericId(string id) =>
        int.TryParse(id, out var number) ? number : null;

    private static string BuildUrl(string id, string path)
    {
        var file = new FileInfo(path);
        var version = $"{file.Length:x}-{file.LastWriteTimeUtc.Ticks:x}";
        return $"/frames/{Uri.EscapeDataString(id)}.png?v={version}";
    }
}

public sealed record ContributorAvatarFrame(string Id, string Url);
