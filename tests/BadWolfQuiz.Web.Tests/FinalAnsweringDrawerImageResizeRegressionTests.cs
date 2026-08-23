namespace BadWolfQuiz.Web.Tests;

public sealed class FinalAnsweringDrawerImageResizeRegressionTests
{
    [Fact]
    public void Final_answering_image_resize_recalculates_drawer_geometry()
    {
        var root = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "gameplay-right-overlay-safe-gap.js"));

        Assert.Contains(
            "const finalAnsweringImageSelector =",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "img.game-content-image",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "const observedFinalAnsweringImages = new WeakSet();",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "finalAnsweringImageResizeObserver = new ResizeObserver(() =>",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "finalAnsweringImageResizeObserver.observe(image);",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "observeFinalAnsweringImages();",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "refreshLayout();",
            script,
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
