namespace BadWolfQuiz.Web.Services;

public static class SeoRouteCatalog
{
    public const string CultureRouteParameter =
        "{culture:regex(^(uk|en|it)$)}";

    public static IReadOnlyList<string> Cultures { get; } =
        ["uk", "en", "it"];

    public static IReadOnlyList<SeoPageRoute> IndexablePages { get; } =
    [
        new("/Index", string.Empty),
        new("/Faq", "faq"),
        new("/About", "about"),
        new("/PublicQuizzes", "public-quizzes")
    ];

    public static bool IsSeoCulture(string? culture) =>
        culture is not null && Cultures.Contains(culture, StringComparer.Ordinal);

    public static string RewriteLocalizedReturnUrl(
        string returnUrl,
        string culture)
    {
        var queryIndex = returnUrl.IndexOf('?', StringComparison.Ordinal);
        var path = queryIndex >= 0 ? returnUrl[..queryIndex] : returnUrl;
        var query = queryIndex >= 0 ? returnUrl[queryIndex..] : string.Empty;
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 0 || !IsSeoCulture(segments[0]))
        {
            return returnUrl;
        }

        var suffix = segments.Length > 1
            ? $"/{string.Join('/', segments.Skip(1))}"
            : string.Empty;
        var rewrittenPath = IsSeoCulture(culture)
            ? $"/{culture}{suffix}"
            : string.IsNullOrEmpty(suffix) ? "/" : suffix;

        return rewrittenPath + query;
    }
}

public readonly record struct SeoPageRoute(string Page, string Path);
