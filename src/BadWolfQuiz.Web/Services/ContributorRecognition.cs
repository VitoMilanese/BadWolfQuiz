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
    private const int FrameCount = 24;

    public static IReadOnlyList<ContributorAvatarFrame> Frames { get; } =
        Enumerable.Range(1, FrameCount)
            .Select(number =>
            {
                var id = number.ToString(CultureInfo.InvariantCulture);
                return new ContributorAvatarFrame(id, $"/frames/{id}.png");
            })
            .ToArray();

    public static string ResolveRootPath(IHostEnvironment environment)
    {
        var outputRoot = Path.Combine(
            AppContext.BaseDirectory,
            "Resources",
            "Frames");

        return Directory.Exists(outputRoot)
            ? outputRoot
            : Path.Combine(
                environment.ContentRootPath,
                "Resources",
                "Frames");
    }

    public static bool IsValid(string? id) =>
        Frames.Any(frame => string.Equals(
            frame.Id,
            id?.Trim(),
            StringComparison.Ordinal));

    public static string Normalize(string? id) =>
        Frames.FirstOrDefault(frame => string.Equals(
            frame.Id,
            id?.Trim(),
            StringComparison.Ordinal))?.Id ?? DefaultId;

    public static string GetUrl(string? id) =>
        $"/frames/{Normalize(id)}.png";
}

public sealed record ContributorAvatarFrame(string Id, string Url);
