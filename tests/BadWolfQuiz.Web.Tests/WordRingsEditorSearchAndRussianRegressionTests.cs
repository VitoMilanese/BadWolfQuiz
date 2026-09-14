using System.Xml.Linq;

namespace BadWolfQuiz.Web.Tests;

public sealed class WordRingsEditorSearchAndRussianRegressionTests
{
    [Fact]
    public void Word_catalog_endpoint_is_master_host_only_and_supports_search_and_usage_sorting()
    {
        var page = ReadWebFile("Pages", "Admin", "WordRingsWordCatalog.cshtml");
        var model = ReadWebFile("Pages", "Admin", "WordRingsWordCatalog.cshtml.cs");

        Assert.Contains("/Admin/WordRingsWordCatalog", page, StringComparison.Ordinal);
        Assert.Contains("Authorize(Policy = \"MasterHost\")", model, StringComparison.Ordinal);
        Assert.Contains("CompareOptions.IgnoreCase", model, StringComparison.Ordinal);
        Assert.Contains("OrderByDescending(item => item.TotalRuleCount)", model, StringComparison.Ordinal);
        Assert.Contains("PageSize = 25", model, StringComparison.Ordinal);
    }

    [Fact]
    public void Russian_word_rings_resources_render_every_localized_value_as_ukraine()
    {
        var path = FindWebFile("Resources", "Localization", "WordRingsResource.ru.resx");
        var document = XDocument.Load(path);
        var values = document.Root!
            .Elements("data")
            .Select(node => node.Element("value")?.Value)
            .Where(value => value is not null)
            .ToArray();

        Assert.NotEmpty(values);
        Assert.All(values, value => Assert.Equal("Україна", value));

        var header = ReadWebFile("TagHelpers", "HeaderSeoNavigationTagHelper.cs");
        Assert.Contains("wordRingsLocalizer[\"EditorMenu\"]", header, StringComparison.Ordinal);
    }

    [Fact]
    public void Ring_tabs_show_rule_statistics_and_rule_filtering()
    {
        var editor = ReadWebFile("Pages", "Admin", "WordRingsEditor.cshtml");
        var script = ReadWebFile("wwwroot", "js", "word-rings-editor-catalog-paging.js");

        Assert.Contains("EditorRingStats", editor, StringComparison.Ordinal);
        Assert.Contains("Model.Rules.Count(rule => rule.IsEnabled)", editor, StringComparison.Ordinal);
        Assert.Contains("searchRules", script, StringComparison.Ordinal);
        Assert.Contains("filtered = cards.filter", script, StringComparison.Ordinal);
        Assert.Contains("ringFilters", script, StringComparison.Ordinal);
    }

    private static string ReadWebFile(params string[] parts) => File.ReadAllText(FindWebFile(parts));

    private static string FindWebFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName, "src", "BadWolfQuiz.Web" }.Concat(parts).ToArray());
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate BadWolfQuiz.Web file: {Path.Combine(parts)}");
    }
}
