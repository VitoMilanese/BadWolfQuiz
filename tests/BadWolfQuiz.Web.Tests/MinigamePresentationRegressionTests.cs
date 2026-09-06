namespace BadWolfQuiz.Web.Tests;

public sealed class MinigamePresentationRegressionTests
{
    [Fact]
    public void Guess_what_i_play_uses_the_branded_responsive_game_shell()
    {
        var page = ReadWebFile("Pages", "GuessWhatIPlay.cshtml");
        var styles = ReadWebFile("wwwroot", "css", "minigames.css");

        Assert.Contains("data-minigames-root", page);
        Assert.Contains("data-room-entry", page);
        Assert.Contains("data-room-shell", page);
        Assert.Contains("data-minigames-grid", page);
        Assert.Contains("data-question-panel", page);

        Assert.Contains("body.portal-layout:has(.minigames-page) > .page-shell", styles);
        Assert.Contains(".minigames-entry-card::before", styles);
        Assert.Contains("content: \"BAD WOLF / MINIGAME\"", styles);
        Assert.Contains(".minigames-toolbar h1::before", styles);
        Assert.Contains(".minigames-stage::before", styles);
        Assert.Contains("grid-template-areas: \"stage questions\"", styles);
        Assert.Contains("aspect-ratio: 1 / 1", styles);
        Assert.Contains("@media (max-width: 520px)", styles);
        Assert.Contains("@media (prefers-reduced-motion: reduce)", styles);
    }

    private static string ReadWebFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = System.IO.Path.Combine(
                new[] { directory.FullName, "src", "BadWolfQuiz.Web" }
                    .Concat(parts)
                    .ToArray());
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate BadWolfQuiz.Web file: {System.IO.Path.Combine(parts)}");
    }
}
