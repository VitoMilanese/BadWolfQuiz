using System.Xml.Linq;

namespace BadWolfQuiz.Web.Tests;

public sealed class WordRingsEditorSearchAndRussianRegressionTests
{
    [Fact]
    public void Word_catalog_endpoint_is_master_host_only_and_supports_search_sort_and_direction()
    {
        var page = ReadWebFile("Pages", "Admin", "WordRingsWordCatalog.cshtml");
        var model = ReadWebFile("Pages", "Admin", "WordRingsWordCatalog.cshtml.cs");

        Assert.Contains("/Admin/WordRingsWordCatalog", page, StringComparison.Ordinal);
        Assert.Contains("Authorize(Policy = \"MasterHost\")", model, StringComparison.Ordinal);
        Assert.Contains("CompareOptions.IgnoreCase", model, StringComparison.Ordinal);
        Assert.Contains("string? direction", model, StringComparison.Ordinal);
        Assert.Contains("OrderBy(item => item.TotalRuleCount)", model, StringComparison.Ordinal);
        Assert.Contains("OrderByDescending(item => item.TotalRuleCount)", model, StringComparison.Ordinal);
        Assert.Contains("OrderBy(item => item.Word, AlphabeticalComparer)", model, StringComparison.Ordinal);
        Assert.Contains("OrderByDescending(item => item.Word, AlphabeticalComparer)", model, StringComparison.Ordinal);
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
    public void Ring_tabs_support_condition_word_and_enabled_state_filtering()
    {
        var editor = ReadWebFile("Pages", "Admin", "WordRingsEditor.cshtml");
        var pagingScript = ReadWebFile("wwwroot", "js", "word-rings-editor-catalog-paging.js");
        var refinements = ReadWebFile("wwwroot", "js", "word-rings-editor-catalog-refinements.js");
        var styles = ReadWebFile("wwwroot", "css", "word-rings-editor-search.css");
        var assets = ReadWebFile("TagHelpers", "WordRingsEditorAssetsTagHelper.cs");

        Assert.Contains("EditorRingStats", editor, StringComparison.Ordinal);
        Assert.Contains("Model.Rules.Count(rule => rule.IsEnabled)", editor, StringComparison.Ordinal);
        Assert.Contains("searchRules", pagingScript, StringComparison.Ordinal);
        Assert.Contains(".word-rings-editor-rule-words p", refinements, StringComparison.Ordinal);
        Assert.Contains("['all', text.all]", refinements, StringComparison.Ordinal);
        Assert.Contains("['active', text.active]", refinements, StringComparison.Ordinal);
        Assert.Contains("['inactive', text.inactive]", refinements, StringComparison.Ordinal);
        Assert.Contains("wordRingsRuleStatus", refinements, StringComparison.Ordinal);
        Assert.Contains("wordRingsWordDirection", refinements, StringComparison.Ordinal);
        Assert.Contains("display: grid", styles, StringComparison.Ordinal);
        Assert.Contains(".word-rings-editor-mark-caption", styles, StringComparison.Ordinal);
        Assert.Contains("display: none !important", styles, StringComparison.Ordinal);
        Assert.Contains("word-rings-editor-catalog-refinements.js?v=1", assets, StringComparison.Ordinal);
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
