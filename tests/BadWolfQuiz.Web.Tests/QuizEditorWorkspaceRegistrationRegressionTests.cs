namespace BadWolfQuiz.Web.Tests;

public sealed class QuizEditorWorkspaceRegistrationRegressionTests
{
    [Fact]
    public void Quiz_editor_workspace_tag_helper_is_registered_for_razor_pages()
    {
        var viewImports = File.ReadAllText(FindWebFile(
            "Pages",
            "_ViewImports.cshtml"));

        Assert.Contains(
            "@addTagHelper BadWolfQuiz.Web.TagHelpers.QuizEditorWorkspaceAssetsTagHelper, BadWolfQuiz.Web",
            viewImports,
            StringComparison.Ordinal);
    }

    private static string FindWebFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidateParts = new[]
            {
                directory.FullName,
                "src",
                "BadWolfQuiz.Web"
            }.Concat(parts).ToArray();
            var candidate = Path.Combine(candidateParts);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate BadWolfQuiz.Web file: {Path.Combine(parts)}");
    }
}
