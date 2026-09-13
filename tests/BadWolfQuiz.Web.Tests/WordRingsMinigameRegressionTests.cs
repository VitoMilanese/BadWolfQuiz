namespace BadWolfQuiz.Web.Tests;

public sealed class WordRingsMinigameRegressionTests
{
    [Fact]
    public void Catalog_and_header_expose_word_rings_game_and_editor()
    {
        var catalog = ReadWebFile("Pages", "Minigames.cshtml");
        var layout = ReadWebFile("Pages", "Shared", "_Layout.cshtml");
        var navigation = ReadWebFile("TagHelpers", "HeaderSeoNavigationTagHelper.cs");
        var game = ReadWebFile("Pages", "WordRings.cshtml");
        var editor = ReadWebFile("Pages", "Admin", "WordRingsEditor.cshtml");
        var editorStyles = ReadWebFile("wwwroot", "css", "word-rings-editor.css");

        Assert.Contains("asp-page=\"/WordRings\"", catalog);
        Assert.DoesNotContain("<strong>02</strong>", catalog);
        Assert.Contains("asp-page=\"/Admin/WordRingsEditor\"", layout);
        Assert.Contains("wordRingsLocalizer", navigation);
        Assert.Contains("output.SuppressOutput()", navigation);
        Assert.Contains("href=\\\"/Admin/MinigameEditor\\\"", navigation);
        Assert.Contains("href=\\\"/Admin/WordRingsEditor\\\"", navigation);
        Assert.True(
            navigation.LastIndexOf("href=\\\"/Admin/MinigameEditor\\\"", StringComparison.Ordinal) <
            navigation.LastIndexOf("href=\\\"/Admin/WordRingsEditor\\\"", StringComparison.Ordinal),
            "Word-rings editor must render after the existing minigame editor for MasterHost navigation.");
        Assert.Contains("@page \"/minigames/word-rings\"", game);
        Assert.Contains("data-ring-stage", game);
        Assert.Contains("data-word-list", game);
        Assert.Contains("data-outside-zone", game);
        Assert.Contains("@page \"/Admin/WordRingsEditor\"", editor);
        Assert.Contains("Authorize", editor);
        Assert.Contains("~/css/minigame-editor.css", editor);
        Assert.Contains("~/css/word-rings-editor.css", editor);
        Assert.Contains("word-rings-editor-heading", editor);
        Assert.DoesNotContain("style=\"", editor);
        Assert.Contains("word-rings-editor-coming-soon", editorStyles);
        Assert.Contains("@media (max-width: 600px)", editorStyles);
    }

    [Fact]
    public void Runtime_supports_all_venn_memberships_and_validation()
    {
        var script = ReadWebFile("wwwroot", "js", "word-rings.js");

        Assert.Contains("membershipAt", script);
        Assert.Contains("Math.hypot", script);
        Assert.Contains("['панда', 'ABC']", script);
        Assert.Contains("['лампа', 'BC']", script);
        Assert.Contains("['песик', 'AC']", script);
        Assert.Contains("['жаба', 'AB']", script);
        Assert.Contains("['дім', '']", script);
        Assert.Contains("assignOutside", script);
        Assert.Contains("errors === 0", script);
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
}
