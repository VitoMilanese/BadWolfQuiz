using Microsoft.AspNetCore.Http;

namespace BadWolfQuiz.Web.Services;

public static class ContributorRecognition
{
    public const string RecognitionCookieName = "badwolfquiz.contributor-thanks";
    public const string ThankYouTempDataKey = "ContributorThankYou";

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

    public static bool ShouldShowThankYou(
        FooterOptions options,
        string? name,
        HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return IsContributor(options, name) &&
            !request.Cookies.ContainsKey(RecognitionCookieName);
    }

    public static void MarkThankYouShown(HttpResponse response, bool secure)
    {
        ArgumentNullException.ThrowIfNull(response);
        response.Cookies.Append(
            RecognitionCookieName,
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
    public const string DefaultId = "gold-fang";

    public static IReadOnlyList<ContributorAvatarFrame> Frames { get; } =
    [
        new("gold-fang", "ContributorFrame_GoldFang"),
        new("moonlight", "ContributorFrame_Moonlight"),
        new("ember", "ContributorFrame_Ember")
    ];

    public static bool IsValid(string? id) =>
        Frames.Any(frame => string.Equals(
            frame.Id,
            id?.Trim(),
            StringComparison.OrdinalIgnoreCase));

    public static string Normalize(string? id) =>
        Frames.FirstOrDefault(frame => string.Equals(
            frame.Id,
            id?.Trim(),
            StringComparison.OrdinalIgnoreCase))?.Id ?? DefaultId;
}

public sealed record ContributorAvatarFrame(string Id, string ResourceKey);
