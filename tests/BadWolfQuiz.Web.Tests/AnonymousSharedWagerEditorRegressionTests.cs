namespace BadWolfQuiz.Web.Tests;

public sealed class AnonymousSharedWagerEditorRegressionTests
{
    [Fact]
    public void Question_editor_exposes_schema_compatible_shared_wager_mode()
    {
        var root = FindRepositoryRoot();
        var tagHelper = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "TagHelpers",
            "AnonymousSharedWagerEditorTagHelper.cs"));
        var modes = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Game",
            "Definitions",
            "QuestionWagerModes.cs"));

        Assert.Contains("Input.PresentationType", tagHelper);
        Assert.Contains("<option value=\\\"6\\\"", tagHelper);
        Assert.Contains("QuestionWagerModes.IsAnonymousShared", tagHelper);
        Assert.Contains("(QuestionPresentationType)6", modes);
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
