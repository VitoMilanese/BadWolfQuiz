namespace BadWolfQuiz.Web.Tests;

public sealed class QuizEditorCategoryExchangeTooltipRegressionTests
{
    [Fact]
    public void Exchange_category_button_uses_localized_tooltip_tag_helper()
    {
        var root = FindRepositoryRoot();
        var editor = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "Quizzes",
            "Editor.cshtml"));
        var tagHelper = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "TagHelpers",
            "QuizEditorCategoryExchangeTooltipTagHelper.cs"));
        var viewImports = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "_ViewImports.cshtml"));

        Assert.Contains("class=\"js-category-exchange", editor, StringComparison.Ordinal);
        Assert.Contains("js-category-exchange", tagHelper, StringComparison.Ordinal);
        Assert.Contains("QuizEditor_ExchangeCategory", tagHelper, StringComparison.Ordinal);
        Assert.Contains("SetAttribute(\"title\", tooltip)", tagHelper, StringComparison.Ordinal);
        Assert.Contains("SetAttribute(\"aria-label\", tooltip)", tagHelper, StringComparison.Ordinal);
        Assert.Contains(
            "QuizEditorCategoryExchangeTooltipTagHelper",
            viewImports,
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
