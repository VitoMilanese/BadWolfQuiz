namespace BadWolfQuiz.Web.Tests;

public sealed class AnswerKeyCaptionScaleRegressionTests
{
    [Fact]
    public void Answer_key_caption_scale_matches_regular_gameplay_caption_scale()
    {
        var gameplayCss = NormalizeLineEndings(File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "busy-indicators.css")));
        var answerKeyCss = NormalizeLineEndings(File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "answer-key-image-frame.css")));

        const string captionScale = "font-size: clamp(1.26rem, 3.15vw, 2.8rem);";

        Assert.Contains(
            $".game-content-caption {{\n    {captionScale}",
            gameplayCss,
            StringComparison.Ordinal);
        Assert.Contains(
            $".answer-key-content .game-content-caption {{\n    {captionScale}",
            answerKeyCss,
            StringComparison.Ordinal);
    }

    private static string NormalizeLineEndings(string content) =>
        content.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');

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
