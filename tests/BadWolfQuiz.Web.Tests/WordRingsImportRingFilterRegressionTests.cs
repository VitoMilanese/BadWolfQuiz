namespace BadWolfQuiz.Web.Tests;

public sealed class WordRingsImportRingFilterRegressionTests
{
    [Fact]
    public void Import_rule_detail_dialogs_filter_by_ring_before_paging()
    {
        var assets = ReadWebFile("TagHelpers", "WordRingsEditorAssetsTagHelper.cs");
        var script = ReadWebFile("wwwroot", "js", "word-rings-import-filters.js");
        var styles = ReadWebFile("wwwroot", "css", "word-rings-import-filters.css");

        Assert.Contains("word-rings-import-filters.css?v=1", assets);
        Assert.Contains("word-rings-import-filters.js?v=1", assets);
        Assert.True(
            assets.IndexOf("word-rings-import-filters.js?v=1", StringComparison.Ordinal) <
            assets.IndexOf("word-rings-editor-followup.js?v=4", StringComparison.Ordinal));

        Assert.Contains("importRuleDetailPageSize = 5", script);
        Assert.Contains("activeRingFilter = 'all'", script);
        Assert.Contains("data-word-rings-import-ring-filter=\"blue\"", script);
        Assert.Contains("data-word-rings-import-ring-filter=\"yellow\"", script);
        Assert.Contains("data-word-rings-import-ring-filter=\"red\"", script);
        Assert.Contains("source.filter(item => item.ring === activeRingFilter)", script);
        Assert.Contains("filteredRuleItems()", script);
        Assert.Contains("data-word-rings-ring-filter-page=\"first\"", script);
        Assert.Contains("data-word-rings-ring-filter-page=\"last\"", script);
        Assert.Contains("latestExportSnapshot", script);
        Assert.Contains("handler === 'ExportCsv'", script);
        Assert.Contains("handler !== 'ImportCsv'", script);

        Assert.Contains("word-rings-editor-import-ring-filters", styles);
        Assert.Contains("#5f86ee", styles);
        Assert.Contains("#e0b43c", styles);
        Assert.Contains("#e85d5d", styles);
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
