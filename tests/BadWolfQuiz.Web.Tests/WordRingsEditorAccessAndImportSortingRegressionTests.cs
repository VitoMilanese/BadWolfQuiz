namespace BadWolfQuiz.Web.Tests;

public sealed class WordRingsEditorAccessAndImportSortingRegressionTests
{
    [Fact]
    public void Editor_is_master_host_only()
    {
        var page = ReadWebFile("Pages", "Admin", "WordRingsEditor.cshtml");
        var model = ReadWebFile("Pages", "Admin", "WordRingsEditor.cshtml.cs");

        Assert.Contains(
            "Authorize(Policy = \"MasterHost\")",
            page,
            StringComparison.Ordinal);
        Assert.Contains(
            "[Authorize(Policy = \"MasterHost\")]",
            model,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Import_detail_lists_are_sorted_alphabetically_before_paging()
    {
        var script = ReadWebFile("wwwroot", "js", "word-rings-editor-followup.js");
        var assets = ReadWebFile("TagHelpers", "WordRingsEditorAssetsTagHelper.cs");

        Assert.Contains("const compareAlphabetically", script, StringComparison.Ordinal);
        Assert.Contains("const sortWords = values => [...values].sort(compareAlphabetically)", script, StringComparison.Ordinal);
        Assert.Contains("compareAlphabetically(left.text, right.text)", script, StringComparison.Ordinal);
        Assert.Contains("? sortWords(sourceItems)", script, StringComparison.Ordinal);
        Assert.Contains(": sortRules(sourceItems)", script, StringComparison.Ordinal);
        Assert.Contains("word-rings-editor-followup.js?v=7", assets, StringComparison.Ordinal);
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
            $"Could not locate BadWolfQuiz.Web file: {Path.Combine(parts)}");
    }
}