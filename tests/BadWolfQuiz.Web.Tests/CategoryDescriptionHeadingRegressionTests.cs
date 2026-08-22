namespace BadWolfQuiz.Web.Tests;

public sealed class CategoryDescriptionHeadingRegressionTests
{
    [Theory]
    [InlineData("RoundIntro.cshtml.cs")]
    [InlineData("RunningRoundIntro.cshtml.cs")]
    public void Gameplay_category_description_heading_uses_category_title_without_label_prefix(
        string fileName)
    {
        var source = File.ReadAllText(FindPageModel("Games", fileName));
        var method = ExtractMethod(source, "private string ResolveCategoryHeading");

        Assert.Contains("if (!string.IsNullOrWhiteSpace(title))", method);
        Assert.Contains("return title;", method);
        Assert.DoesNotContain("$\"{label}: {title}\"", method);
        Assert.DoesNotContain("StartsWithLabel", method);
    }

    [Fact]
    public void Editor_category_description_preview_uses_category_title_without_label_prefix()
    {
        var source = File.ReadAllText(
            FindPageModel("Quizzes", "DescriptionEditor.cshtml.cs"));
        var method = ExtractMethod(
            source,
            "private async Task<string> BuildPreviewTitleAsync");
        var categoryStart = method.IndexOf(
            "if (categoryId.HasValue)",
            StringComparison.Ordinal);
        var roundStart = method.IndexOf(
            "var roundLabel",
            categoryStart,
            StringComparison.Ordinal);

        Assert.True(categoryStart >= 0);
        Assert.True(roundStart > categoryStart);

        var categoryBranch = method[categoryStart..roundStart];
        Assert.Contains(
            "if (!string.IsNullOrWhiteSpace(trimmedTitle)) return trimmedTitle;",
            categoryBranch);
        Assert.DoesNotContain("{categoryLabel}: {trimmedTitle}", categoryBranch);
        Assert.DoesNotContain("StartsWithLabel", source);
    }

    private static string ExtractMethod(string source, string methodSignature)
    {
        var start = source.IndexOf(methodSignature, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Could not find {methodSignature}.");

        var end = source.IndexOf(
            "\n    private ",
            start + 1,
            StringComparison.Ordinal);
        Assert.True(end > start, $"Could not find the end of {methodSignature}.");

        return source[start..end];
    }

    private static string FindPageModel(string area, string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src",
                "BadWolfQuiz.Web",
                "Pages",
                "Admin",
                area,
                fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate {fileName} from the test output directory.");
    }
}
