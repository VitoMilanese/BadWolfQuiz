namespace BadWolfQuiz.Web.Tests;

public sealed class FinalQuestionViewportFitRegressionTests
{
    [Fact]
    public void Final_question_and_answer_use_the_shared_viewport_fit_controller()
    {
        var root = FindRepositoryRoot();
        var script = Read(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "game-content-viewport-fit.js");
        var lobby = Read(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "Games",
            "Lobby.cshtml");

        Assert.Contains(
            ".host-game-board .final-question-panel .game-content-presentation .game-content-blocks:not(.four-clue-grid):not(.all-player-answer-grid)",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "data-media-autoplay-state=\"final-question\"",
            lobby,
            StringComparison.Ordinal);
        Assert.Contains(
            "data-media-autoplay-state=\"final-answer\"",
            lobby,
            StringComparison.Ordinal);
        Assert.Contains(
            "<img class=\"game-content-image\"",
            lobby,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Final_images_keep_the_existing_compact_expand_and_refit_behavior()
    {
        var script = ReadAsset("js", "game-content-viewport-fit.js");

        Assert.Contains("images.length !== 1", script, StringComparison.Ordinal);
        Assert.Contains(
            "Math.max(120, Math.min(180, Math.round(window.innerHeight * 0.18)))",
            script,
            StringComparison.Ordinal);
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
            "event.key !== \"Enter\" && event.key !== \" \"",
            script,
            StringComparison.Ordinal);
        Assert.Contains("ResizeObserver", script, StringComparison.Ordinal);
        Assert.Contains(
            "document.addEventListener(\"badwolf:host-gameplay-updated\", fitAll);",
            script,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Final_images_share_first_paint_hiding_and_scrollbar_suppression()
    {
        var styles = ReadAsset("css", "game-content-viewport-fit.css");

        Assert.Contains(
            ".final-question-panel .game-content-presentation",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            "img.game-content-image:not([data-game-content-fit-ready=\"true\"])",
            styles,
            StringComparison.Ordinal);
        Assert.Contains("visibility: hidden;", styles, StringComparison.Ordinal);
        Assert.Contains(":has(", styles, StringComparison.Ordinal);
        Assert.Contains("overflow-y: hidden;", styles, StringComparison.Ordinal);
    }

    private static string ReadAsset(string folder, string fileName)
    {
        var root = FindRepositoryRoot();
        return Read(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            folder,
            fileName);
    }

    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(parts).ToArray()));

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
