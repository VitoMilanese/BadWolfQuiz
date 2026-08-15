namespace BadWolfQuiz.Web.Tests;

public sealed class BusyIndicatorRegressionTests
{
    [Fact]
    public void BusyIndicatorAssetsAreLoadedGlobally()
    {
        var root = FindRepositoryRoot();
        var layout = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "Pages", "Shared", "_Layout.cshtml"));

        Assert.Contains("~/css/busy-indicators.css", layout, StringComparison.Ordinal);
        Assert.Contains("~/js/busy-indicators.js", layout, StringComparison.Ordinal);
    }

    [Fact]
    public void BusyIndicatorUsesGlobalModalOverlay()
    {
        var root = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "wwwroot", "js", "busy-indicators.js"));
        var styles = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "wwwroot", "css", "busy-indicators.css"));

        Assert.Contains("document.createElement(\"dialog\")", script, StringComparison.Ordinal);
        Assert.Contains("overlay.showModal()", script, StringComparison.Ordinal);
        Assert.Contains("document.body.appendChild(overlay)", script, StringComparison.Ordinal);
        Assert.Contains("aria-busy", script, StringComparison.Ordinal);
        Assert.Contains(".app-busy-overlay::backdrop", styles, StringComparison.Ordinal);
        Assert.DoesNotContain("busy-indicator-bounce", styles, StringComparison.Ordinal);
        Assert.DoesNotContain("nth-child(2)", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void BusyIndicatorTracksRequiredIssue167FlowsWithoutCatchingEveryEditorPost()
    {
        var root = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "wwwroot", "js", "busy-indicators.js"));

        Assert.Contains("/admin/quizzes", script, StringComparison.Ordinal);
        Assert.Contains("/public-quizzes", script, StringComparison.Ordinal);
        Assert.Contains("/admin/quizzes/editor", script, StringComparison.Ordinal);
        Assert.Contains("/admin/quizzes/questioneditor", script, StringComparison.Ordinal);
        Assert.Contains("/admin/quizzes/finalquestioneditor", script, StringComparison.Ordinal);
        Assert.Contains("/admin/quizzes/descriptioneditor", script, StringComparison.Ordinal);
        Assert.Contains("path.startsWith(`${route}/`)", script, StringComparison.Ordinal);
        Assert.Contains("creategame", script, StringComparison.Ordinal);
        Assert.Contains("continuegame", script, StringComparison.Ordinal);
        Assert.Contains("[data-ajax-save-round]", script, StringComparison.Ordinal);
        Assert.Contains("[data-ajax-question-editor]", script, StringComparison.Ordinal);
        Assert.Contains("submitter?.getAttribute(\"name\") === \"play\"", script, StringComparison.Ordinal);
        Assert.DoesNotContain("editorPaths.has(currentPath) &&\n            form.method.toLowerCase() === \"post\"", script, StringComparison.Ordinal);
    }

    [Fact]
    public void BusyIndicatorKeepsAjaxSaveVisibleUntilExistingSubmitterIsReenabled()
    {
        var root = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "wwwroot", "js", "busy-indicators.js"));

        Assert.Contains("event.defaultPrevented", script, StringComparison.Ordinal);
        Assert.Contains("watchAjaxCompletion", script, StringComparison.Ordinal);
        Assert.Contains("new MutationObserver", script, StringComparison.Ordinal);
        Assert.Contains("attributeFilter: [\"disabled\"]", script, StringComparison.Ordinal);
        Assert.Contains("if (!submitter.disabled)", script, StringComparison.Ordinal);
        Assert.DoesNotContain("submitter.disabled = true", script, StringComparison.Ordinal);
        Assert.DoesNotContain("submitter.setAttribute(\"disabled\"", script, StringComparison.Ordinal);
    }

    [Fact]
    public void BusyIndicatorHandlesEscapeBackNavigationAfterPainting()
    {
        var root = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "wwwroot", "js", "busy-indicators.js"));

        Assert.Contains("window.addEventListener(\"keyup\"", script, StringComparison.Ordinal);
        Assert.Contains("event.key !== \"Escape\"", script, StringComparison.Ordinal);
        Assert.Contains("question-editor-back-link", script, StringComparison.Ordinal);
        Assert.Contains("final-question-editor-back-link", script, StringComparison.Ordinal);
        Assert.Contains("description-editor-back", script, StringComparison.Ordinal);
        Assert.Contains("hasOpenEditorModal", script, StringComparison.Ordinal);
        Assert.Contains("runAfterPaint", script, StringComparison.Ordinal);
        Assert.Contains("window.requestAnimationFrame(() =>", script, StringComparison.Ordinal);
        Assert.Contains("navigate(backLink.href)", script, StringComparison.Ordinal);
    }

    [Fact]
    public void BusyIndicatorPreventsDuplicatesAndRecoversFromCancelledOrHistoryNavigation()
    {
        var root = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "wwwroot", "js", "busy-indicators.js"));

        Assert.Contains("form.dataset.busyLocked === \"true\"", script, StringComparison.Ordinal);
        Assert.Contains("event.stopImmediatePropagation();", script, StringComparison.Ordinal);
        Assert.Contains("overlay.addEventListener(\"cancel\"", script, StringComparison.Ordinal);
        Assert.Contains("window.addEventListener(\"pageshow\", hide)", script, StringComparison.Ordinal);
        Assert.Contains("if (!event.defaultPrevented)", script, StringComparison.Ordinal);
    }

    [Fact]
    public void BusyIndicatorUsesBrandedOrbitAnimationAndReducedMotionFallback()
    {
        var root = FindRepositoryRoot();
        var styles = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "wwwroot", "css", "busy-indicators.css"));

        Assert.Contains("app-busy-orbit-outer", styles, StringComparison.Ordinal);
        Assert.Contains("app-busy-orbit-inner", styles, StringComparison.Ordinal);
        Assert.Contains("app-busy-core", styles, StringComparison.Ordinal);
        Assert.Contains("app-busy-spin", styles, StringComparison.Ordinal);
        Assert.Contains("app-busy-spin-reverse", styles, StringComparison.Ordinal);
        Assert.Contains("var(--red-bright)", styles, StringComparison.Ordinal);
        Assert.Contains("prefers-reduced-motion: reduce", styles, StringComparison.Ordinal);
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
