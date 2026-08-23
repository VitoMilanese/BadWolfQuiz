namespace BadWolfQuiz.Web.Tests;

public sealed class GameSessionLauncherRegressionTests
{
    [Fact]
    public void Launcher_projects_content_metadata_without_loading_file_blobs()
    {
        var root = FindRepositoryRoot();
        var launcher = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Services",
            "GameSessionLauncher.cs"));

        Assert.DoesNotContain(
            ".ThenInclude(question => question.QuestionBlocks)",
            launcher,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            ".ThenInclude(question => question.AnswerBlocks)",
            launcher,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            ".ThenInclude(round => round.DescriptionBlocks)",
            launcher,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            ".ThenInclude(category => category.DescriptionBlocks)",
            launcher,
            StringComparison.Ordinal);

        Assert.Contains("RoundDescriptionContentBlocks", launcher, StringComparison.Ordinal);
        Assert.Contains("CategoryDescriptionContentBlocks", launcher, StringComparison.Ordinal);
        Assert.Contains("QuestionContentBlocks", launcher, StringComparison.Ordinal);
        Assert.Contains("AnswerContentBlocks", launcher, StringComparison.Ordinal);
        Assert.Contains("FinalDescriptionContentBlocks", launcher, StringComparison.Ordinal);
        Assert.Contains("FinalQuestionContentBlocks", launcher, StringComparison.Ordinal);
        Assert.Contains("FinalAnswerContentBlocks", launcher, StringComparison.Ordinal);
        Assert.Contains("HasFileData = block.FileData != null", launcher, StringComparison.Ordinal);
        Assert.Contains(
            "snapshotFactory.CreateFromDetachedQuiz(quiz)",
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
