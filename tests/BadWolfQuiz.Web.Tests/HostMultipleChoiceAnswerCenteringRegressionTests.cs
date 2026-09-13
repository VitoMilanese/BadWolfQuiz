namespace BadWolfQuiz.Web.Tests;

public sealed class HostMultipleChoiceAnswerCenteringRegressionTests
{
    [Fact]
    public void Every_live_multiple_choice_answer_stack_is_centered_with_additional_reveal_content()
    {
        var root = FindRepositoryRoot();
        var tagHelper = Read(root,
            "src/BadWolfQuiz.Web/TagHelpers/MultipleChoiceAnswerRevealTagHelper.cs");
        var reveal = Read(root,
            "src/BadWolfQuiz.Web/Pages/Admin/Games/_MultipleChoiceRevealBlocks.cshtml");

        Assert.Contains(
            "!MultipleChoiceAnswerContract.IsMultipleChoice(question.PresentationType)",
            tagHelper,
            StringComparison.Ordinal);
        Assert.Contains(
            "var centerMultipleChoiceAnswerReveal = blocks.Any();",
            tagHelper,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "question.PresentationType == QuestionPresentationType.HostMultipleChoice",
            tagHelper,
            StringComparison.Ordinal);
        Assert.Contains(
            "classes.Add(\"multiple-choice-answer-centered\")",
            tagHelper,
            StringComparison.Ordinal);

        Assert.Contains(
            ".game-content-blocks.multiple-choice-answer-reveal-grid.multiple-choice-answer-centered",
            reveal,
            StringComparison.Ordinal);
        Assert.Contains(
            "justify-content: safe center !important;",
            reveal,
            StringComparison.Ordinal);
        Assert.Contains(
            "align-self: stretch !important;",
            reveal,
            StringComparison.Ordinal);
        Assert.Contains(
            "overflow-y: auto !important;",
            reveal,
            StringComparison.Ordinal);
        Assert.Contains(
            "multiple-choice-additional-answer-block",
            reveal,
            StringComparison.Ordinal);
        Assert.Contains(
            "all-player-answer-option all-player-answer-option-correct",
            reveal,
            StringComparison.Ordinal);
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
