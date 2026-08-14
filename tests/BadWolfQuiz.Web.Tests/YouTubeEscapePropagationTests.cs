namespace BadWolfQuiz.Web.Tests;

public sealed class YouTubeEscapePropagationTests
{
    [Fact]
    public void Escape_used_to_exit_expanded_youtube_does_not_reach_editor_shortcuts()
    {
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "youtube-auto-expand.js"));

        Assert.Contains("let suppressNextEscapeKeyUp = false;", script);
        Assert.Contains("suppressNextEscapeKeyUp = true;", script);
        Assert.Contains("window.addEventListener(\"keyup\", event =>", script);
        Assert.Contains("event.stopImmediatePropagation();", script);
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
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate BadWolfQuiz.Web file: {Path.Combine(parts)}");
    }
}
