namespace BadWolfQuiz.Web.Tests;

public sealed class HostGameplaySubmitGuardBootstrapRegressionTests
{
    [Fact]
    public void Layout_body_loads_latest_host_submit_guard_before_other_body_content()
    {
        var helper = File.ReadAllText(FindWebFile(
            "TagHelpers",
            "HostGameplaySubmitGuardAssetsTagHelper.cs"));

        Assert.Contains("[HtmlTargetElement(\"body\")]", helper, StringComparison.Ordinal);
        Assert.Contains("output.PreContent.AppendHtml", helper, StringComparison.Ordinal);
        Assert.Contains(
            "/js/host-gameplay-submit-guard.js?v=5",
            helper,
            StringComparison.Ordinal);
        Assert.Contains(
            "data-host-gameplay-submit-guard",
            helper,
            StringComparison.Ordinal);
        Assert.Contains(
            "/js/host-question-selection-recovery.js?v=1",
            helper,
            StringComparison.Ordinal);
        Assert.Contains(
            "data-host-question-selection-recovery",
            helper,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Question_selection_freezes_visible_board_box_not_only_outer_host_container()
    {
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "host-gameplay-submit-guard.js"));

        Assert.Contains(
            "const boardBounds = board.getBoundingClientRect();",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "board: captureInlineProperties(board, boardPropertyNames)",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "board.style.setProperty(\"min-width\", boardWidth, \"important\");",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "board.style.setProperty(\"max-width\", boardWidth, \"important\");",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "board.style.setProperty(\"min-height\", boardHeight, \"important\");",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "board.style.setProperty(\"max-height\", boardHeight, \"important\");",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "restoreInlineProperties(snapshot.board);",
            script,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Question_selection_recovers_once_from_non_json_bad_request_with_fresh_antiforgery_token()
    {
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "host-question-selection-recovery.js"));

        Assert.Contains(
            "window.fetch = async (input, init) =>",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "url.searchParams.get(\"handler\")?.toLowerCase() === \"selectquestion\"",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "response.status !== 400",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "contentType.toLowerCase().includes(\"application/json\")",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "const token = await getFreshAntiforgeryToken();",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "body: cloneFormDataWithToken(init.body, token)",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "return await nativeFetch(input, retryInit);",
            script,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Antiforgery_validation_failures_are_logged_at_information_level()
    {
        var settings = File.ReadAllText(FindWebFile("appsettings.json"));

        Assert.Contains(
            "\"Microsoft.AspNetCore.Mvc.ViewFeatures.Filters.ValidateAntiforgeryTokenAuthorizationFilter\": \"Information\"",
            settings,
            StringComparison.Ordinal);
    }

    private static string FindWebFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                new[]
                {
                    directory.FullName,
                    "src",
                    "BadWolfQuiz.Web"
                }.Concat(parts).ToArray());

            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate BadWolfQuiz.Web file: {string.Join('/', parts)}");
    }
}
