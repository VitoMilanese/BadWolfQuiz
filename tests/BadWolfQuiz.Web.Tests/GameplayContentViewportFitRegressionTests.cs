namespace BadWolfQuiz.Web.Tests;

public sealed class GameplayContentViewportFitRegressionTests
{
    [Fact]
    public void Gameplay_bootstrap_loads_viewport_fit_assets_before_soft_mounted_questions()
    {
        var root = FindRepositoryRoot();
        var bootstrap = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "gameplay-escape-shortcuts.js"));

        Assert.Contains(
            "loadSharedStyle(\"/css/game-content-viewport-fit.css\");",
            bootstrap,
            StringComparison.Ordinal);
        Assert.Contains(
            "loadSharedScript(\"/js/game-content-viewport-fit.js\");",
            bootstrap,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Viewport_fit_styles_are_available_before_first_gameplay_paint()
    {
        var root = FindRepositoryRoot();
        var busyStyles = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "css",
            "busy-indicators.css"));
        var viewportStyles = ReadAsset("css", "game-content-viewport-fit.css");
        var viewportScript = ReadAsset("js", "game-content-viewport-fit.js");

        Assert.Contains(
            "@import url(\"./game-content-viewport-fit.css?v=2\");",
            busyStyles,
            StringComparison.Ordinal);
        Assert.Contains(
            "img.game-content-image:not([data-game-content-fit-ready=\"true\"])",
            viewportStyles,
            StringComparison.Ordinal);
        Assert.Contains("visibility: hidden;", viewportStyles, StringComparison.Ordinal);
        Assert.Contains(
            "image.dataset.gameContentFitReady = \"true\";",
            viewportScript,
            StringComparison.Ordinal);
        Assert.Contains(
            "image.removeAttribute(\"data-game-content-fit-ready\");",
            viewportScript,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Active_and_resolved_question_views_use_the_page_shell_vertical_space()
    {
        var styles = ReadAsset("css", "game-content-viewport-fit.css");

        Assert.Contains(
            ".current-question-summary:not(.wager-mode)",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            ".question-review-preview",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            "height: calc(100dvh - var(--topbar-height));",
            styles,
            StringComparison.Ordinal);
        Assert.Contains("margin-top: -34px;", styles, StringComparison.Ordinal);
        Assert.Contains("margin-top: -20px;", styles, StringComparison.Ordinal);
        Assert.Contains(
            "gap: clamp(9px, 1.5vw, 18px);",
            styles,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Single_image_fit_preserves_multi_image_and_four_clue_layouts()
    {
        var script = ReadAsset("js", "game-content-viewport-fit.js");

        Assert.Contains(
            ".game-content-blocks:not(.four-clue-grid):not(.all-player-answer-grid)",
            script,
            StringComparison.Ordinal);
        Assert.Contains("images.length !== 1", script, StringComparison.Ordinal);
        Assert.Contains(
            "Math.max(120, Math.min(180, Math.round(window.innerHeight * 0.18)))",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "container.scrollHeight - container.clientHeight",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "--game-content-fit-height",
            script,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Auto_fitted_image_can_toggle_back_to_normal_size_and_refit()
    {
        var script = ReadAsset("js", "game-content-viewport-fit.js");

        Assert.Contains(
            "image.dataset.gameContentFitState === \"compact\"",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "image.dataset.gameContentFitExpanded = \"true\";",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "delete image.dataset.gameContentFitExpanded;",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "document.addEventListener(\"badwolf:host-gameplay-updated\", scheduleFit);",
            script,
            StringComparison.Ordinal);
        Assert.Contains("ResizeObserver", script, StringComparison.Ordinal);
        Assert.Contains(
            "window.addEventListener(\"resize\", scheduleFit);",
            script,
            StringComparison.Ordinal);
    }

    private static string ReadAsset(string folder, string fileName)
    {
        var root = FindRepositoryRoot();
        return File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            folder,
            fileName));
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
