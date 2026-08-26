namespace BadWolfQuiz.Web.Tests;

public sealed class AllPlayerTextAnswerEditorRegressionTests
{
    [Fact]
    public void Text_answer_mode_keeps_standard_answer_content_in_both_editor_controllers()
    {
        var root = FindRepositoryRoot();
        var allPlayerScript = Read(root,
            "src/BadWolfQuiz.Web/wwwroot/js/all-player-question.js");
        var answerOptionsScript = Read(root,
            "src/BadWolfQuiz.Web/wwwroot/js/multiple-choice-answer-options.js");

        Assert.DoesNotContain("answerCards.length === 1", allPlayerScript);
        Assert.DoesNotContain(
            "setAllowedTypes(answerSection, new Set([\"Text\"]));",
            allPlayerScript);
        Assert.DoesNotContain("text.invalidText", allPlayerScript);

        Assert.DoesNotContain("cards.length === 1", answerOptionsScript);
        Assert.DoesNotContain(
            "setAllowedTopLevelTypes(answerSection, new Set([\"Text\"]));",
            answerOptionsScript);
        Assert.DoesNotContain("text.invalidText", answerOptionsScript);
        Assert.DoesNotContain("text.yourAnswer", answerOptionsScript);
        Assert.Contains(
            "answerHeading.dataset.standardHeading ?? answerHeading.textContent",
            answerOptionsScript);
        Assert.Contains("if (!isChoiceType(type))", answerOptionsScript);
    }

    private static string Read(string root, string relativePath) =>
        File.ReadAllText(Path.Combine(
            root,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));

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
