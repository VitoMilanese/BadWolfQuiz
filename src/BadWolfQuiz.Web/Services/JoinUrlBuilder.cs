namespace BadWolfQuiz.Web.Services;

public sealed class JoinUrlBuilder(IConfiguration configuration)
{
    public string Build(HttpRequest request, string publicCode)
    {
        var configuredBaseUrl = configuration["Game:PublicBaseUrl"]?.Trim();
        var baseUrl = string.IsNullOrWhiteSpace(configuredBaseUrl)
            ? $"{request.Scheme}://{request.Host}{request.PathBase}"
            : configuredBaseUrl.TrimEnd('/');

        return $"{baseUrl}/Join/{Uri.EscapeDataString(publicCode)}";
    }
}
