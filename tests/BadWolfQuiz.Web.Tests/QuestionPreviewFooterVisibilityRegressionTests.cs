namespace BadWolfQuiz.Web.Tests;

public sealed class QuestionPreviewFooterVisibilityRegressionTests
{
    [Fact]
    public void Shared_question_preview_hides_the_entire_portal_footer_while_open()
    {
        var root = FindRepositoryRoot();
        var preview = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "Quizzes",
            "Shared",
            "_QuestionPreviewModal.cshtml"));

        Assert.Contains(
            "body.question-preview-open > .portal-footer",
            preview,
            StringComparison.Ordinal);
        Assert.Contains(
            "opacity: 0;",
            preview,
            StringComparison.Ordinal);
        Assert.Contains(
            "pointer-events: none;",
            preview,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Preview_hiding_cannot_be_overridden_by_visible_contributor_children()
    {
        var root = FindRepositoryRoot();
        var preview = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "Quizzes",
            "Shared",
            "_QuestionPreviewModal.cshtml"));
        var styles = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "css",
            "site.css"));

        Assert.Contains(
            ".portal-footer-name > [data-footer-contributor][data-current=\"true\"]",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            "visibility: visible;",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            "body.question-preview-open > .portal-footer",
            preview,
            StringComparison.Ordinal);
        Assert.Contains(
            "opacity: 0;",
            preview,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("QuestionEditor.cshtml")]
    [InlineData("FinalQuestionEditor.cshtml")]
    public void Editor_previews_toggle_the_shared_preview_open_body_class(string fileName)
    {
        var root = FindRepositoryRoot();
        var page = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "Quizzes",
            fileName));

        Assert.Contains(
            "document.body.classList.add(",
            page,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"question-preview-open\"",
            page,
            StringComparison.Ordinal);
        Assert.Contains(
            "document.body.classList.remove(",
            page,
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
