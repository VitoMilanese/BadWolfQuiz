namespace BadWolfQuiz.Web.Tests;

public sealed class ReportedHostLayoutRegressionTests
{
    [Fact]
    public void Host_normal_wager_runtime_stacks_the_two_left_cards_together()
    {
        var helper = ReadWebFile("TagHelpers", "AnonymousSharedWagerAssetsTagHelper.cs");
        var script = ReadWebFile("wwwroot", "js", "host-normal-wager-left-stack.js");
        var styles = ReadWebFile("wwwroot", "css", "host-normal-wager-left-stack.css");

        Assert.Contains("/css/host-normal-wager-left-stack.css?v=1", helper, StringComparison.Ordinal);
        Assert.Contains("/js/host-normal-wager-left-stack.js?v=1", helper, StringComparison.Ordinal);
        Assert.Contains("host-normal-wager-context", script, StringComparison.Ordinal);
        Assert.Contains("context.appendChild(heading);", script, StringComparison.Ordinal);
        Assert.Contains("context.appendChild(playerSummary);", script, StringComparison.Ordinal);
        Assert.Contains("grid-row: 1 / span 2 !important;", styles, StringComparison.Ordinal);
        Assert.Contains("gap: 32px !important;", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void Scrollable_host_answers_keep_their_image_at_normal_size()
    {
        var helper = ReadWebFile("TagHelpers", "AnonymousSharedWagerAssetsTagHelper.cs");
        var script = ReadWebFile("wwwroot", "js", "game-content-viewport-fit.js");

        Assert.Contains("/js/game-content-viewport-fit.js?v=6", helper, StringComparison.Ordinal);
        Assert.Contains("overflowWithoutImage", script, StringComparison.Ordinal);
        Assert.Contains("if (overflowWithoutImage > overflowTolerance)", script, StringComparison.Ordinal);
        Assert.Contains("clearImageFit(image);", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Scrollable_answer_key_never_squeezes_the_only_image_to_a_pixel()
    {
        var script = ReadWebFile("wwwroot", "js", "answer-key-image-fit.js");

        Assert.Contains("nonImageContentHeight", script, StringComparison.Ordinal);
        Assert.Contains("container.clientHeight - overflowTolerance", script, StringComparison.Ordinal);
        Assert.Contains("clearFit(currentImage);", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Math.max(1,\n            Math.min(\n                container.clientHeight -", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Quiz_editor_unfinished_game_guard_is_registered_for_razor_pages()
    {
        var imports = ReadWebFile("Pages", "_ViewImports.cshtml");

        Assert.Contains(
            "@addTagHelper BadWolfQuiz.Web.TagHelpers.QuizEditorUnfinishedGameGuardTagHelper, BadWolfQuiz.Web",
            imports,
            StringComparison.Ordinal);
    }

    private static string ReadWebFile(params string[] parts)
    {
        var root = FindRepositoryRoot();
        return File.ReadAllText(Path.Combine(
            new[] { root, "src", "BadWolfQuiz.Web" }.Concat(parts).ToArray()));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src")) &&
                Directory.Exists(Path.Combine(directory.FullName, "tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
