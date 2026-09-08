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
            ".answer-key-content .game-content-block:has(> .game-content-image) > .game-content-image {",
            frameCss,
            StringComparison.Ordinal);
        Assert.Contains("0 0 0 1px", frameCss, StringComparison.Ordinal);
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
