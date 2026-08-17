namespace BadWolfQuiz.Web.Tests;

public sealed class GameSessionLauncherRegressionTests
{
    [Fact]
    public void LauncherLoadsRoundAndCategoryDescriptionBlocksForIntros()
    {
        var root = FindRepositoryRoot();
        var launcher = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Services",
            "GameSessionLauncher.cs"));

        Assert.Contains(
            ".ThenInclude(round => round.DescriptionBlocks)",
            launcher,
            StringComparison.Ordinal);
        Assert.Contains(
            ".ThenInclude(category => category.DescriptionBlocks)",
            launcher,
            StringComparison.Ordinal);
        Assert.Contains(
            "snapshotFactory.Create(quiz)",
            launcher,
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
