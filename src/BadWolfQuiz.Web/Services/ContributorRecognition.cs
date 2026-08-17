using System.Globalization;
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
    public const int DefaultAvatarInsetPixels = 10;
    private const int MaximumAvatarInsetPixels = 512;

    public static IReadOnlyList<ContributorAvatarFrame> GetFrames(
        IHostEnvironment environment)
    {
        var root = ResolveRootPath(environment);
        if (!Directory.Exists(root))
        {
            return Array.Empty<ContributorAvatarFrame>();
        }

        return GetFrameFiles(root)
            .OrderBy(
                item => ParseNumericId(item.Id),
                Comparer<int?>.Create((left, right) =>
                    left.HasValue && right.HasValue
                        ? left.Value.CompareTo(right.Value)
                        : left.HasValue ? -1 : right.HasValue ? 1 : 0))
            .ThenBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .Select(item => new ContributorAvatarFrame(
                item.Id,
                BuildUrl(item.Id, item.Path),
                item.AvatarInsetPixels))
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
        TryResolveFrameFile(environment, id, out _);

    public static bool IsValid(string? id) => IsSafeId(id);

    public static string? Normalize(
        IHostEnvironment environment,
        string? id)
    {
        if (TryResolveFrameFile(environment, id, out var frame))
        {
            return frame.Id;
        }

        return GetFrames(environment).FirstOrDefault()?.Id;
    }

    public static string? GetUrl(
        IHostEnvironment environment,
        string? id)
    {
        var normalizedId = Normalize(environment, id);
        return normalizedId is not null &&
               TryResolveFrameFile(environment, normalizedId, out var frame)
            ? BuildUrl(frame.Id, frame.Path)
            : null;
    }

    public static int GetAvatarInsetPixels(
        IHostEnvironment environment,
        string? id) =>
        TryResolveFrameFile(environment, id, out var frame)
            ? frame.AvatarInsetPixels
            : DefaultAvatarInsetPixels;

    public static bool TryResolvePath(
        IHostEnvironment environment,
        string? id,
        out string path)
    {
        if (TryResolveFrameFile(environment, id, out var frame))
        {
            path = frame.Path;
            return true;
        }

        path = string.Empty;
        return false;
    }

    private static bool TryResolveFrameFile(
        IHostEnvironment environment,
        string? id,
        out ContributorAvatarFrameFile frame) =>
        TryResolveFrameFile(ResolveRootPath(environment), id, out frame);

    private static bool TryResolveFrameFile(
        string root,
        string? id,
        out ContributorAvatarFrameFile frame)
    {
        frame = default!;
        if (!IsSafeId(id) || !Directory.Exists(root))
        {
            return false;
        }

        var normalizedId = id!.Trim();
        var match = GetFrameFiles(root).FirstOrDefault(item =>
            string.Equals(item.Id, normalizedId, StringComparison.Ordinal));
        if (match is null)
        {
            return false;
        }

        frame = match;
        return true;
    }

    private static IReadOnlyList<ContributorAvatarFrameFile> GetFrameFiles(string root)
    {
        return Directory
            .EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly)
            .Where(path => string.Equals(
                Path.GetExtension(path),
                ".png",
                StringComparison.OrdinalIgnoreCase))
            .Select(ParseFrameFile)
            .Where(frame => frame is not null)
            .Select(frame => frame!)
            .GroupBy(frame => frame.Id, StringComparer.Ordinal)
            .Select(group => group
                .OrderByDescending(frame => frame.HasExplicitInset)
                .ThenBy(
                    frame => Path.GetFileName(frame.Path),
                    StringComparer.OrdinalIgnoreCase)
                .First())
            .ToArray();
    }

    private static ContributorAvatarFrameFile? ParseFrameFile(string path)
    {
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path);
        if (string.IsNullOrWhiteSpace(fileNameWithoutExtension))
        {
            return null;
        }

        var id = fileNameWithoutExtension;
        var insetPixels = DefaultAvatarInsetPixels;
        var hasExplicitInset = false;
        var separatorIndex = fileNameWithoutExtension.LastIndexOf('-');
        if (separatorIndex > 0 &&
            separatorIndex < fileNameWithoutExtension.Length - 1 &&
            int.TryParse(
                fileNameWithoutExtension[(separatorIndex + 1)..],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var parsedInset) &&
            parsedInset is >= 0 and <= MaximumAvatarInsetPixels)
        {
            id = fileNameWithoutExtension[..separatorIndex];
            insetPixels = parsedInset;
            hasExplicitInset = true;
        }

        return IsSafeId(id)
            ? new ContributorAvatarFrameFile(
                id,
                path,
                insetPixels,
                hasExplicitInset)
            : null;
    }

    private static bool IsSafeId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        var normalizedId = id.Trim();
        return normalizedId is not "." and not ".." &&
               normalizedId.All(character =>
                   char.IsLetterOrDigit(character) ||
                   character is '-' or '_');
    }

    private static int? ParseNumericId(string id) =>
        int.TryParse(
            id,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var number)
            ? number
            : null;

    private static string BuildUrl(string id, string path)
    {
        var file = new FileInfo(path);
        var version = $"{file.Length:x}-{file.LastWriteTimeUtc.Ticks:x}";
        return $"/frames/{Uri.EscapeDataString(id)}.png?v={version}";
    }

    private sealed record ContributorAvatarFrameFile(
        string Id,
        string Path,
        int AvatarInsetPixels,
        bool HasExplicitInset);
}

public sealed record ContributorAvatarFrame(
    string Id,
    string Url,
    int AvatarInsetPixels);
