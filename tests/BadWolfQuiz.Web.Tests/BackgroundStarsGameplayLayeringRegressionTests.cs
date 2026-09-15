namespace BadWolfQuiz.Web.Tests;

public sealed class BackgroundStarsGameplayLayeringRegressionTests
{
    [Fact]
    public void Live_question_and_final_host_surfaces_keep_particles_above_panel_backgrounds()
    {
        var css = ReadWebFile("wwwroot", "css", "background-stars.css");

        Assert.Contains(
            ".current-question-summary.site-starfield-host > .site-starfield",
            css,
            StringComparison.Ordinal);
        Assert.Contains(
            ".final-question-panel.site-starfield-host > .site-starfield",
            css,
            StringComparison.Ordinal);
        Assert.Contains(
            ".final-question-transition.site-starfield-host > .site-starfield",
            css,
            StringComparison.Ordinal);
        Assert.Contains(
            ".current-question-summary.site-starfield-host > :not(.site-starfield)",
            css,
            StringComparison.Ordinal);
        Assert.Contains(
            ".final-question-transition.site-starfield-host > .final-question-transition-content",
            css,
            StringComparison.Ordinal);
        Assert.Contains("z-index: 0 !important;", css, StringComparison.Ordinal);
        Assert.Contains("z-index: 1;", css, StringComparison.Ordinal);
    }

    private static string ReadWebFile(params string[] relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                new[] { directory.FullName, "src", "BadWolfQuiz.Web" }
                    .Concat(relativePath)
                    .ToArray());
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }
            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate BadWolfQuiz.Web/{string.Join('/', relativePath)}.");
    }
}
