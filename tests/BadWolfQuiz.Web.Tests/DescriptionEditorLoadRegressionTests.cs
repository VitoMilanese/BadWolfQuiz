namespace BadWolfQuiz.Web.Tests;

public sealed class DescriptionEditorLoadRegressionTests
{
    [Fact]
    public void Description_editor_get_projects_metadata_without_hydrating_media_blobs()
    {
        var source = File.ReadAllText(FindWebFile(
            "Pages",
            "Admin",
            "Quizzes",
            "DescriptionEditor.cshtml.cs"));

        var onGetStart = source.IndexOf(
            "public async Task<IActionResult> OnGetAsync",
            StringComparison.Ordinal);
        var onPostStart = source.IndexOf(
            "public async Task<IActionResult> OnPostAsync",
            StringComparison.Ordinal);

        Assert.True(onGetStart >= 0);
        Assert.True(onPostStart > onGetStart);

        var onGet = source[onGetStart..onPostStart];

        Assert.DoesNotContain(
            ".Include(x => x.DescriptionBlocks)",
            onGet,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "FileData",
            onGet,
            StringComparison.Ordinal);

        Assert.Contains(
            "db.CategoryDescriptionContentBlocks",
            onGet,
            StringComparison.Ordinal);
        Assert.Contains(
            "db.RoundDescriptionContentBlocks",
            onGet,
            StringComparison.Ordinal);
        Assert.Contains(
            ".Select(x => new ContentBlockInputModel",
            onGet,
            StringComparison.Ordinal);
        Assert.Contains(
            "StoredFileHandler = \"CategoryDescriptionBlockFile\"",
            onGet,
            StringComparison.Ordinal);
        Assert.Contains(
            "StoredFileHandler = \"RoundDescriptionBlockFile\"",
            onGet,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Category_cell_navigation_does_not_wait_before_starting_description_editor_navigation()
    {
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "quiz-description-editor-links.js"));

        var categoryNavigation =
            "window.location.href = `/Admin/Quizzes/DescriptionEditor?categoryId=${encodeURIComponent(categoryId)}`;";

        Assert.Contains(
            categoryNavigation,
            script,
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
