using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Hosting;

namespace BadWolfQuiz.Web.Tests;

public sealed class WordRingsEditorBulkActionsRegressionTests
{
    [Fact]
    public void Editor_has_membership_filter_bulk_delete_dialogs_and_correct_page_sizes()
    {
        var page = ReadWebFile("Pages", "Admin", "WordRingsEditor.cshtml");
        var model = ReadWebFile("Pages", "Admin", "WordRingsEditor.cshtml.cs");
        var script = ReadWebFile("wwwroot", "js", "word-rings-editor.js");
        var followup = ReadWebFile("wwwroot", "js", "word-rings-editor-followup.js");
        var store = ReadWebFile("Services", "WordRingsRuleStore.cs");

        Assert.Contains("MembershipRulePageSize = 10", model, StringComparison.Ordinal);
        Assert.Contains("includedOnly = false", model, StringComparison.Ordinal);
        Assert.Contains("GetRuleMembershipCount", store, StringComparison.Ordinal);
        Assert.Contains("data-word-rings-membership-included-only", page, StringComparison.Ordinal);
        Assert.Contains("includedOnly", script, StringComparison.Ordinal);
        Assert.Contains("data-delete-all-word-rings-words", page, StringComparison.Ordinal);
        Assert.Contains("data-delete-all-word-rings-rules", page, StringComparison.Ordinal);
        Assert.Contains("asp-page-handler=\"DeleteAllWords\"", page, StringComparison.Ordinal);
        Assert.Contains("asp-page-handler=\"DeleteAllRules\"", page, StringComparison.Ordinal);
        Assert.Contains("dialog-card-danger", page, StringComparison.Ordinal);
        Assert.Contains("DeleteAllWordsAsync", store, StringComparison.Ordinal);
        Assert.Contains("DeleteAllRulesAsync", store, StringComparison.Ordinal);
        Assert.Contains("importNewWordDetailRowsPerPage = 9", followup, StringComparison.Ordinal);
        Assert.Contains("measureNewWordDetailPages", followup, StringComparison.Ordinal);
        Assert.Contains("importRuleDetailPageSize = 5", followup, StringComparison.Ordinal);
        Assert.DoesNotContain("window.confirm", script, StringComparison.Ordinal);
        Assert.DoesNotContain("alert(", script, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Bulk_delete_words_and_rules_are_single_write_destructive_operations()
    {
        var root = Path.Combine(Path.GetTempPath(), $"badwolf-word-rings-bulk-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var store = WordRingsRuleStore.Get(new TestWebHostEnvironment(root));
            Assert.True(store.GetWordCount() > 0);

            var clearWords = await store.DeleteAllWordsAsync(CancellationToken.None);
            Assert.Equal(WordRingRuleMutationResult.Success, clearWords);
            Assert.Equal(0, store.GetWordCount());
            Assert.All(Enum.GetValues<WordRingColor>(), ring =>
                Assert.All(store.GetRules(ring), rule => Assert.Empty(rule.Words)));

            var emptyPuzzle = store.CreatePuzzle();
            Assert.Empty(emptyPuzzle.Words);
            Assert.Empty(emptyPuzzle.Expected);

            var clearBlue = await store.DeleteAllRulesAsync(
                WordRingColor.Blue,
                CancellationToken.None);
            Assert.Equal(WordRingRuleMutationResult.Success, clearBlue);
            Assert.Empty(store.GetRules(WordRingColor.Blue));
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