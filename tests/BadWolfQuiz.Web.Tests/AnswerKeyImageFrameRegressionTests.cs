namespace BadWolfQuiz.Web.Tests;

public sealed class AnswerKeyImageFrameRegressionTests
{
    [Fact]
    public void Image_frame_follows_the_image_without_changing_the_layout_block_width()
    {
        var page = File.ReadAllText(FindWebFile(
            "Pages",
            "Admin",
            "Games",
            "AnswerKey.cshtml"));
        var stageCss = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "answer-key.css"));
        var frameCss = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "answer-key-image-frame.css"));

        Assert.Contains("answer-key-image-frame.css", page, StringComparison.Ordinal);
        Assert.Contains(
            ".answer-key-content .game-content-block {\n    width: 100%;",
            stageCss,
            StringComparison.Ordinal);
        Assert.Contains(
            ".answer-key-content .game-content-block:has(> .game-content-image) {",
            frameCss,
            StringComparison.Ordinal);
        Assert.Contains("border-color: transparent;", frameCss, StringComparison.Ordinal);
        Assert.Contains("background: transparent;", frameCss, StringComparison.Ordinal);
        Assert.Contains("box-shadow: none;", frameCss, StringComparison.Ordinal);
        Assert.Contains(
            ".answer-key-content.game-content-presentation\n    .game-content-blocks:not(.four-clue-grid)\n    > .game-content-block:has(> .game-content-image)\n    > .game-content-image {",
            frameCss,
            StringComparison.Ordinal);
        Assert.Contains("width: auto;", frameCss, StringComparison.Ordinal);
        Assert.Contains("height: auto;", frameCss, StringComparison.Ordinal);
        Assert.Contains("max-width: min(1180px, 100%);", frameCss, StringComparison.Ordinal);
        Assert.Contains("0 0 0 1px", frameCss, StringComparison.Ordinal);
    }

    [Fact]
    public void Single_image_is_scaled_up_to_the_available_stage_without_touching_multi_block_layout()
    {
        var page = File.ReadAllText(FindWebFile(
            "Pages",
            "Admin",
            "Games",
            "AnswerKey.cshtml"));
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "answer-key-image-fit.js"));

        Assert.Contains("~/js/answer-key-image-fit.js", page, StringComparison.Ordinal);
        Assert.Contains("if (blocks.length !== 1)", script, StringComparison.Ordinal);
        Assert.Contains("image.naturalWidth", script, StringComparison.Ordinal);
        Assert.Contains("image.naturalHeight", script, StringComparison.Ordinal);
        Assert.Contains("availableWidth / image.naturalWidth", script, StringComparison.Ordinal);
        Assert.Contains("availableHeight / image.naturalHeight", script, StringComparison.Ordinal);
        Assert.Contains(
            "image.style.setProperty(\"width\", `${targetWidth}px`, \"important\");",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "image.style.setProperty(\"height\", `${targetHeight}px`, \"important\");",
            script,
            StringComparison.Ordinal);
        Assert.Contains("MutationObserver", script, StringComparison.Ordinal);
        Assert.Contains("ResizeObserver", script, StringComparison.Ordinal);
        Assert.Contains("attributeFilter: [\"hidden\"]", script, StringComparison.Ordinal);
    }

    private static string FindWebFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidateParts = new[]
            {
                directory.FullName,
                "src",
                "BadWolfQuiz.Web"
            }.Concat(parts).ToArray();
            var candidate = Path.Combine(candidateParts);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate BadWolfQuiz.Web file: {Path.Combine(parts)}");
    }
}
