namespace BadWolfQuiz.Web.Tests;

public sealed class FinalQuestionTransitionStylesRegressionTests
{
    [Fact]
    public void Transition_keeps_stage_styles_inline_during_soft_navigation()
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
        Assert.Contains(".final-question-transition {", markup);
        Assert.Contains("position: fixed;", markup);
        Assert.Contains("inset: var(--topbar-height, 60px) 0 0;", markup);
        Assert.DoesNotContain("@@import url(", markup);
        Assert.Contains("main.page-shell > style", navigation);
        Assert.Contains("styles.map(style => document.importNode(style, true))", navigation);
    }

    [Fact]
    public void Transition_stage_is_fixed_to_the_free_viewport_without_scroll_rails()
    {
        var markup = File.ReadAllText(FindWebFile(
            "Pages",
            "Admin",
            "Games",
            "FinalQuestionTransition.cshtml"));

        Assert.Contains(
            "body.gameplay-layout:has(.final-question-transition)",
            markup);
        Assert.Contains("position: fixed;", markup);
        Assert.Contains("width: auto;", markup);
        Assert.Contains("margin: 0;", markup);
        Assert.Contains("overflow: hidden;", markup);
        Assert.Contains("repeating-linear-gradient(", markup);
        Assert.DoesNotContain("width: 100vw;", markup);
        Assert.DoesNotContain("calc(50% - 50vw)", markup);
        Assert.DoesNotContain("transparent 0 7%", markup);
    }

    [Fact]
    public void Transition_guard_is_installed_synchronously_with_host_controls()
    {
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "quick-timer-controls.js"));

        Assert.Contains("window.badWolfFinalQuestionTransitionGuardInstalled", script);
        Assert.Contains("finalTransitionRequested", script);
        Assert.Contains("guardedHandlers", script);
        Assert.Contains("StartFinalQuestion", script);
        Assert.Contains("ForceAdvanceToFinalQuestion", script);
        Assert.Contains("window.BadWolfHostGameplay?.cancelPending?.();", script);
        Assert.Contains("hostGameplay.refresh = (...args) =>", script);
        Assert.Contains("if (isFinalTransitionLocked())", script);
        Assert.Contains("HTMLFormElement.prototype.requestSubmit", script);
        Assert.Contains("arguments.length === 0", script);
        Assert.DoesNotContain("final-question-transition-guard.js", script);
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
