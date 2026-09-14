using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Hosting;

namespace BadWolfQuiz.Web.Tests;

public sealed class WordRingsEditorFollowUpRegressionTests
{
    [Fact]
    public void Editor_defaults_to_all_words_and_enhances_word_catalog_ui()
    {
        var model = ReadWebFile("Pages", "Admin", "WordRingsEditor.cshtml.cs");
        var assets = ReadWebFile("TagHelpers", "WordRingsEditorAssetsTagHelper.cs");
        var script = ReadWebFile("wwwroot", "js", "word-rings-editor-followup.js");
        var styles = ReadWebFile("wwwroot", "css", "word-rings-editor-followup.css");

        Assert.Contains("string.IsNullOrWhiteSpace(ring)", model);
        Assert.Contains("MinimumSingleWordLength = 3", ReadWebFile("Services", "WordRingsRuleStore.cs"));
        Assert.Contains("minimumWordLength = 3", script);
        Assert.Contains("input.minLength = minimumWordLength", script);
        Assert.Contains("data-word-rings-word-pager-position", script);
        Assert.Contains("cloneNode(true)", script);
        Assert.Contains("captureImportDetails", script);
        Assert.Contains("parseConfigurationCsv", script);
        Assert.Contains("compareConfigurations", script);
        Assert.Contains("newWords", script);
        Assert.Contains("newRules", script);
        Assert.Contains("updatedExistingRules", script);
        Assert.Contains("response.clone().json()", script);
        Assert.Contains("data-word-rings-import-detail", script);
        Assert.Contains("word-rings-editor-import-details-dialog", script);
        Assert.Contains("importDetailPageSize = 5", script);
        Assert.Contains("word-rings-editor-import-detail-eye", script);
        Assert.Contains("data-word-rings-import-details-page=\"last\"", script);
        Assert.Contains("changeImportDetailsPage", script);
        Assert.Contains("word-rings-editor-word-pager-top", styles);
        Assert.Contains("text-transform: uppercase", styles);
        Assert.Contains("[data-word-rings-membership-word]", styles);
        Assert.Contains("[data-word-rings-delete-word-target]", styles);
        Assert.Contains("word-rings-editor-import-detail-actions", styles);
        Assert.Contains("word-rings-editor-import-detail-eye", styles);
        Assert.Contains("word-rings-editor-import-details-pager", styles);
        Assert.Contains("word-rings-editor-import-rule-detail-blue", styles);
        Assert.Contains("word-rings-editor-followup.css?v=3", assets);
        Assert.Contains("word-rings-editor-followup.js?v=3", assets);
    }

    [Fact]
    public async Task Word_validation_is_minimum_three_characters_and_membership_comparison_is_case_insensitive()
    {
        var root = Path.Combine(Path.GetTempPath(), $"badwolf-word-rings-followup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var store = WordRingsRuleStore.Get(new TestWebHostEnvironment(root));
            var yellow = Assert.Single(store.GetRules(WordRingColor.Yellow));

            var shortMembership = await store.ApplyWordMembershipChangesAsync(
                "ab",
                new Dictionary<Guid, bool> { [yellow.Id] = true },
                CancellationToken.None);
            Assert.Equal(WordRingRuleMutationResult.InvalidWords, shortMembership);

            var shortRule = await store.AddAsync(
                WordRingColor.Blue,
                "Коротке слово",
                "аб",
                CancellationToken.None);
            Assert.Equal(WordRingRuleMutationResult.InvalidWords, shortRule);

            var blueMembership = store.GetRuleMembershipPage(
                WordRingColor.Blue,
                "КІТ",
                0,
                25);
            Assert.Contains(blueMembership, item => item.ContainsWord);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Csv_round_trip_preserves_enabled_state_for_existing_rules()
    {
        var root = Path.Combine(Path.GetTempPath(), $"badwolf-word-rings-csv-state-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var store = WordRingsRuleStore.Get(new TestWebHostEnvironment(root));
            var originalBlue = Assert.Single(store.GetRules(WordRingColor.Blue));

            var reserveResult = await store.AddAsync(
                WordRingColor.Blue,
                "Резервне правило",
                "слово, тест",
                CancellationToken.None);
            Assert.Equal(WordRingRuleMutationResult.Success, reserveResult);

            var disableCsv =
                "Ring,Rule,Enabled,Words\n" +
                $"\"blue\",\"{originalBlue.Text}\",\"false\",\"кіт\"\n";
            var disabledImport = await store.ImportCsvAsync(disableCsv, CancellationToken.None);
            Assert.Equal(WordRingRuleMutationResult.Success, disabledImport.Result);
            Assert.False(store.GetRules(WordRingColor.Blue)
                .Single(rule => rule.Id == originalBlue.Id)
                .IsEnabled);
            Assert.Contains(
                $"\"blue\",\"{originalBlue.Text}\",\"false\"",
                store.ExportCsv(),
                StringComparison.Ordinal);

            var enableCsv =
                "Ring,Rule,Enabled,Words\n" +
                $"\"blue\",\"{originalBlue.Text}\",\"true\",\"КІТ\"\n";
            var enabledImport = await store.ImportCsvAsync(enableCsv, CancellationToken.None);
            Assert.Equal(WordRingRuleMutationResult.Success, enabledImport.Result);
            Assert.True(store.GetRules(WordRingColor.Blue)
                .Single(rule => rule.Id == originalBlue.Id)
                .IsEnabled);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string ReadWebFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName, "src", "BadWolfQuiz.Web" }.Concat(parts).ToArray());
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate BadWolfQuiz.Web file: {Path.Combine(parts)}");
    }

    private sealed class TestWebHostEnvironment(string root) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "BadWolfQuiz.Web.Tests";
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
        public string WebRootPath { get; set; } = root;
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = root;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
