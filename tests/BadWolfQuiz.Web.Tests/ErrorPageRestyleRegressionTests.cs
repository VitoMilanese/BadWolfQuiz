namespace BadWolfQuiz.Web.Tests;

public sealed class ErrorPageRestyleRegressionTests
{
    [Fact]
    public void Error_page_uses_branded_500_presentation_and_existing_localization()
    {
        var markup = ReadWebFile("Pages", "Error.cshtml");

        Assert.Contains("~/css/public-account-pages.css", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"portal-page not-found-page error-page\"", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"portal-hero not-found-hero error-hero\"", markup, StringComparison.Ordinal);
        Assert.Contains("BAD WOLF QUIZ / 500", markup, StringComparison.Ordinal);
        Assert.Contains("<strong>500</strong>", markup, StringComparison.Ordinal);
        Assert.Contains("Localizer[\"Title_Error\"]", markup, StringComparison.Ordinal);
        Assert.Contains("Localizer[\"Error_Unexpected\"]", markup, StringComparison.Ordinal);
        Assert.Contains("Localizer[\"Error_CheckLogsAndRetry\"]", markup, StringComparison.Ordinal);
        Assert.Contains("Localizer[\"Menu_Home\"]", markup, StringComparison.Ordinal);
        Assert.Contains("href=\"@Url.Content(\"~/\")\"", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Error_page_preserves_exception_handler_and_request_id_contracts()
    {
        var program = ReadWebFile("Program.cs");
        var pageModel = ReadWebFile("Pages", "Error.cshtml.cs");

        Assert.Contains("app.UseExceptionHandler(\"/Error\")", program, StringComparison.Ordinal);
        Assert.Contains("[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]", pageModel, StringComparison.Ordinal);
        Assert.Contains("[IgnoreAntiforgeryToken]", pageModel, StringComparison.Ordinal);
        Assert.Contains("RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;", pageModel, StringComparison.Ordinal);
    }

    private static string ReadWebFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                new[] { directory.FullName, "src", "BadWolfQuiz.Web" }
                    .Concat(parts)
                    .ToArray());
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate web file: {string.Join('/', parts)}");
    }
}
