namespace BadWolfQuiz.Web.Tests;

public sealed class FinalQuestionTransitionStylesRegressionTests
{
    [Fact]
    public void Transition_keeps_stage_styles_available_during_soft_navigation()
    {
        var markup = File.ReadAllText(FindWebFile(
            "Pages",
            "Admin",
            "Games",
            "FinalQuestionTransition.cshtml"));
        var navigation = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "site.js"));

        Assert.Contains("<style data-final-question-stage-styles>", markup);
        Assert.Contains("@@import url(\"/css/final-question-stage.css\");", markup);
        Assert.Contains("main.page-shell > style", navigation);
        Assert.Contains("styles.map(style => document.importNode(style, true))", navigation);
    }

    [Fact]
    public void Transition_stage_covers_the_host_viewport_without_side_rails()
    {
        var styles = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "final-question-stage.css"));

        Assert.Contains(
            "body.gameplay-layout:has(.final-question-transition) > .page-shell",
            styles);
        Assert.Contains("width: 100vw;", styles);
        Assert.Contains("margin-inline: calc(50% - 50vw);", styles);
        Assert.Contains("repeating-linear-gradient(", styles);
        Assert.DoesNotContain("transparent 0 7%", styles);
    }

    [Fact]
    public void Transition_blocks_stale_host_refreshes_while_it_is_mounted()
    {
        var guard = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "final-question-transition-guard.js"));

        Assert.Contains(
            "[data-host-gameplay-view] [data-final-question-transition]",
            guard);
        Assert.Contains("hostGameplay.refresh = (...args) =>", guard);
        Assert.Contains("if (isFinalTransitionActive())", guard);
        Assert.Contains("return Promise.resolve(false);", guard);
        Assert.Contains("finalQuestionTransitionRefreshGuardInstalled", guard);
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
