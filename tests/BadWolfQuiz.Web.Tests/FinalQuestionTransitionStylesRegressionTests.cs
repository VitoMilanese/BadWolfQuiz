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
    public void Transition_stage_is_fixed_to_the_free_viewport_without_scroll_rails()
    {
        var styles = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "final-question-stage.css"));

        Assert.Contains(
            "body.gameplay-layout:has(.final-question-transition)",
            styles);
        Assert.Contains("position: fixed;", styles);
        Assert.Contains("inset: var(--topbar-height, 60px) 0 0;", styles);
        Assert.Contains("width: auto;", styles);
        Assert.Contains("margin: 0;", styles);
        Assert.Contains("overflow: hidden;", styles);
        Assert.Contains("repeating-linear-gradient(", styles);
        Assert.DoesNotContain("width: 100vw;", styles);
        Assert.DoesNotContain("calc(50% - 50vw)", styles);
        Assert.DoesNotContain("transparent 0 7%", styles);
    }

    [Fact]
    public void Transition_locks_refresh_before_soft_navigation_and_blocks_legacy_auto_submit()
    {
        var guard = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "final-question-transition-guard.js"));
        var loader = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "quick-timer-controls.js"));

        Assert.Contains(
            "[data-host-gameplay-view] [data-final-question-transition]",
            guard);
        Assert.Contains("finalTransitionRequested", guard);
        Assert.Contains("guardedHandlers", guard);
        Assert.Contains("StartFinalQuestion", guard);
        Assert.Contains("ForceAdvanceToFinalQuestion", guard);
        Assert.Contains("window.BadWolfHostGameplay?.cancelPending?.();", guard);
        Assert.Contains("hostGameplay.refresh = (...args) =>", guard);
        Assert.Contains("if (isFinalTransitionLocked())", guard);
        Assert.Contains("HTMLFormElement.prototype.requestSubmit", guard);
        Assert.Contains("arguments.length === 0", guard);
        Assert.Contains("final-question-transition-guard.js?v=3", loader);
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
