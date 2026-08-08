namespace BadWolfQuiz.Web.Services;

public sealed class UserQuestionHistoryService
{
    private const string CookieName = "BadWolfQuiz.UserQuestions";
    private const int MaxQuestionCount = 80;
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(365);

    public IReadOnlyList<string> GetTokens(HttpRequest request)
    {
        if (!request.Cookies.TryGetValue(CookieName, out var value) ||
            string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(IsValidToken)
            .Distinct(StringComparer.Ordinal)
            .Take(MaxQuestionCount)
            .ToArray();
    }

    public bool HasAny(HttpRequest request) => GetTokens(request).Count > 0;

    public void Add(HttpRequest request, HttpResponse response, string token)
    {
        if (!IsValidToken(token))
        {
            return;
        }

        var tokens = GetTokens(request)
            .Where(existing => !string.Equals(existing, token, StringComparison.Ordinal))
            .Prepend(token)
            .Take(MaxQuestionCount)
            .ToArray();

        WriteTokens(response, tokens);
    }

    public void Remove(HttpRequest request, HttpResponse response, string token)
    {
        var tokens = GetTokens(request)
            .Where(existing => !string.Equals(existing, token, StringComparison.Ordinal))
            .ToArray();

        WriteTokens(response, tokens);
    }

    public void Replace(HttpResponse response, IEnumerable<string> tokens)
    {
        var normalizedTokens = tokens
            .Where(IsValidToken)
            .Distinct(StringComparer.Ordinal)
            .Take(MaxQuestionCount)
            .ToArray();

        WriteTokens(response, normalizedTokens);
    }

    private static bool IsValidToken(string token) =>
        Guid.TryParseExact(token, "N", out _);

    private static void WriteTokens(HttpResponse response, IReadOnlyCollection<string> tokens)
    {
        if (tokens.Count == 0)
        {
            response.Cookies.Delete(CookieName, new CookieOptions
            {
                Path = "/",
                Secure = true,
                HttpOnly = true,
                SameSite = SameSiteMode.Lax
            });

            return;
        }

        response.Cookies.Append(
            CookieName,
            string.Join(',', tokens),
            new CookieOptions
            {
                Path = "/",
                Secure = true,
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                IsEssential = true,
                MaxAge = Lifetime
            });
    }
}
