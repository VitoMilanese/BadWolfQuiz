namespace BadWolfQuiz.Web.Tests;

public sealed class AnonymousSharedWagerBuzzerRegressionTests
{
    [Fact]
    public void Reload_during_collection_suppresses_buzzer_on_server()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "TagHelpers",
            "AnonymousSharedWagerBuzzerTagHelper.cs"));

        Assert.Contains("player-buzzer", source);
        Assert.Contains("RuntimeQuestionStatus.AwaitingWager", source);
        Assert.Contains("QuestionWagerModes.IsAnonymousShared", source);
        Assert.Contains("output.SuppressOutput();", source);
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
