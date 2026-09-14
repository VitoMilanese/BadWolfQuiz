namespace BadWolfQuiz.Web.Tests;

public sealed class WordRingsEditorCatalogPagingRegressionTests
{
    [Fact]
    public void Editor_shows_total_word_usage_and_pages_ring_rule_lists()
    {
        var model = ReadWebFile("Pages", "Admin", "WordRingsEditor.cshtml.cs");
        var assets = ReadWebFile("TagHelpers", "WordRingsEditorAssetsTagHelper.cs");
        var script = ReadWebFile("wwwroot", "js", "word-rings-editor-catalog-paging.js");

        Assert.Contains("MembershipRulePageSize = 50", model, StringComparison.Ordinal);
        Assert.Contains("rulePageSize = 25", script, StringComparison.Ordinal);
        Assert.Contains("word-rings-editor-word-usage-total", script, StringComparison.Ordinal);
        Assert.Contains("usage.prepend(badge)", script, StringComparison.Ordinal);
        Assert.Contains("makePager(ring, 'top')", script, StringComparison.Ordinal);
        Assert.Contains("makePager(ring, 'bottom')", script, StringComparison.Ordinal);
        Assert.Contains("word-rings-editor-word-pager-top", script, StringComparison.Ordinal);
        Assert.Contains("ringPages", script, StringComparison.Ordinal);
        Assert.Contains("card.hidden = index < start || index >= end", script, StringComparison.Ordinal);
        Assert.Contains("word-rings-editor-catalog-paging.js?v=1", assets, StringComparison.Ordinal);
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
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate BadWolfQuiz.Web file: {Path.Combine(parts)}");
    }
}
