namespace BadWolfQuiz.Web.Tests;

public sealed class AnswerKeyOverflowRegressionTests
{
    [Fact]
    public void Tall_mixed_answer_stays_in_flow_and_scrolls_from_the_top()
    {
        var css = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "answer-key-image-frame.css"));

        Assert.Contains(
            ".answer-key-content .game-content-blocks:not(.four-clue-grid)",
            css,
            StringComparison.Ordinal);
        Assert.Contains("grid-auto-rows: max-content;", css, StringComparison.Ordinal);
        Assert.Contains("align-content: safe center;", css, StringComparison.Ordinal);
        Assert.Contains("overflow-x: hidden;", css, StringComparison.Ordinal);
        Assert.Contains("overflow-y: auto;", css, StringComparison.Ordinal);
        Assert.Contains("scrollbar-gutter: stable;", css, StringComparison.Ordinal);
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
