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
