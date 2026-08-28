namespace BadWolfQuiz.Web.Tests;

public sealed class AnonymousSharedWagerEditorUiRegressionTests
{
    [Fact]
    public void Shared_wager_is_exposed_as_wager_mode_and_required_tag_helpers_are_registered()
    {
        var editor = ReadWebFile(
            "TagHelpers",
            "AnonymousSharedWagerEditorTagHelper.cs");
        var imports = ReadWebFile("Pages", "_ViewImports.cshtml");

        Assert.Contains("Wager mode", editor);
        Assert.Contains("Normal wager", editor);
        Assert.Contains("Anonymous shared wager", editor);
        Assert.Contains("data-anonymous-shared-wager-mode", editor);
        Assert.Contains("presentationTypeSelect.value = \"6\";", editor);
        Assert.Contains("presentationTypeSelect.value = \"0\";", editor);

        Assert.Contains(
            "AnonymousSharedWagerEditorTagHelper",
            imports);
        Assert.Contains(
            "AnonymousSharedWagerAssetsTagHelper",
            imports);
        Assert.Contains(
            "AnonymousSharedWagerBuzzerTagHelper",
            imports);
    }

    private static string ReadWebFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                new[] { directory.FullName, "src", "BadWolfQuiz.Web" }
                    .Concat(parts)
                    .ToArray());
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate {Path.Combine(parts)}");
    }
}
