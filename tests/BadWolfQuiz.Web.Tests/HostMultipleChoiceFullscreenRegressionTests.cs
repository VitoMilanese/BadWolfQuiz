namespace BadWolfQuiz.Web.Tests;

public sealed class HostMultipleChoiceFullscreenRegressionTests
{
    [Fact]
    public void Host_multiple_choice_panel_is_hidden_while_youtube_video_is_expanded()
    {
        var root = FindRepositoryRoot();
        var bootstrap = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "host-multiple-choice-bootstrap.js"));

        Assert.Contains(
            "body.youtube-auto-expanded-open > .host-multiple-choice-panel",
            bootstrap,
            StringComparison.Ordinal);
        Assert.Contains(
            "display: none !important;",
            bootstrap,
            StringComparison.Ordinal);
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
