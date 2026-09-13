namespace BadWolfQuiz.Web.Tests;

public sealed class WordRingsMinigameRegressionTests
{
    [Fact]
    public void Catalog_and_header_expose_word_rings_game_and_editor()
    {
        var catalog = ReadWebFile("Pages", "Minigames.cshtml");
        var layout = ReadWebFile("Pages", "Shared", "_Layout.cshtml");
        var game = ReadWebFile("Pages", "WordRings.cshtml");
        var editor = ReadWebFile("Pages", "Admin", "WordRingsEditor.cshtml");

        Assert.Contains("asp-page=\"/WordRings\"", catalog);
        Assert.Contains("asp-page=\"/Admin/WordRingsEditor\"", layout);
        Assert.Contains("@page \"/minigames/word-rings\"", game);
        Assert.Contains("data-ring-stage", game);
        Assert.Contains("data-word-list", game);
        Assert.Contains("data-outside-zone", game);
        Assert.Contains("@page \"/Admin/WordRingsEditor\"", editor);
        Assert.Contains("Authorize", editor);
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
