namespace BadWolfQuiz.Web.Tests;

public sealed class WordRingsActionCardsLayoutRegressionTests
{
    [Fact]
    public void Theft_preview_matches_dialog_card_and_action_cards_move_below_players()
    {
        var css = ReadWebFile("wwwroot", "css", "word-rings-action-cards-layout.css");
        var script = ReadWebFile("wwwroot", "js", "word-rings-action-cards-layout.js");
        var tagHelper = ReadWebFile("TagHelpers", "WordRingsActionCardsAssetsTagHelper.cs");

        Assert.Contains("width: min(96vw, 820px);", css, StringComparison.Ordinal);
        Assert.Contains("align-self: start;", css, StringComparison.Ordinal);
        Assert.Contains("width: min(100%, 308px);", css, StringComparison.Ordinal);
        Assert.Contains("max-width: 308px;", css, StringComparison.Ordinal);
        Assert.Contains("border: 0;", css, StringComparison.Ordinal);
        Assert.Contains("box-shadow: none;", css, StringComparison.Ordinal);

        Assert.Contains("word-rings-action-left-column", script, StringComparison.Ordinal);
        Assert.Contains("leftColumn.append(players);", script, StringComparison.Ordinal);
        Assert.Contains("leftColumn.append(actionShell);", script, StringComparison.Ordinal);
        Assert.Contains("has-action-left-column", script, StringComparison.Ordinal);
        Assert.Contains("MutationObserver", script, StringComparison.Ordinal);

        Assert.Contains("word-rings-action-cards-layout.css?v=1", tagHelper, StringComparison.Ordinal);
        Assert.Contains("word-rings-action-cards-layout.js?v=1", tagHelper, StringComparison.Ordinal);
        Assert.True(
            tagHelper.IndexOf("word-rings-action-cards-v2.js?v=5", StringComparison.Ordinal) <
            tagHelper.IndexOf("word-rings-action-cards-layout.js?v=1", StringComparison.Ordinal));
    }

    private static string ReadWebFile(params string[] pathParts) =>
        File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "BadWolfQuiz.Web",
            Path.Combine(pathParts)));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src", "BadWolfQuiz.Web")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
