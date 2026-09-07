namespace BadWolfQuiz.Web.Tests;

public sealed class PeerRatedLayoutRegressionTests
{
    [Fact]
    public void Player_peer_rated_answer_panel_spans_the_entire_lobby_grid()
    {
        var script = ReadRepoFile(
            "src", "BadWolfQuiz.Web", "wwwroot", "js",
            "peer-rated-all-player-layout.js");

        Assert.Contains(
            ".player-lobby > .peer-rated-player-panel",
            script,
            StringComparison.Ordinal);
        Assert.Contains("grid-column: 1 / -1 !important", script, StringComparison.Ordinal);
        Assert.Contains("width: 100% !important", script, StringComparison.Ordinal);
        Assert.Contains("justify-self: stretch !important", script, StringComparison.Ordinal);
        Assert.Contains(".peer-rated-review-card", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Host_peer_review_question_uses_available_width_without_card_chrome()
    {
        var script = ReadRepoFile(
            "src", "BadWolfQuiz.Web", "wwwroot", "js",
            "peer-rated-all-player-layout.js");

        Assert.Contains(
            ".host-game-board.peer-rated-reviewing .peer-rated-question-context",
            script,
            StringComparison.Ordinal);
        Assert.Contains("max-width: none !important", script, StringComparison.Ordinal);
        Assert.Contains("padding: 0 !important", script, StringComparison.Ordinal);
        Assert.Contains("border: 0 !important", script, StringComparison.Ordinal);
        Assert.Contains("background: transparent !important", script, StringComparison.Ordinal);
        Assert.Contains("box-shadow: none !important", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Peer_layout_overrides_load_after_the_question_context_script()
    {
        var tagHelper = ReadRepoFile(
            "src", "BadWolfQuiz.Web", "TagHelpers",
            "PeerRatedAllPlayerAssetsTagHelper.cs");

        var context = tagHelper.IndexOf(
            "peer-rated-question-context.js?v=1",
            StringComparison.Ordinal);
        var layout = tagHelper.IndexOf(
            "peer-rated-all-player-layout.js?v=1",
            StringComparison.Ordinal);

        Assert.True(context >= 0);
        Assert.True(layout > context);
    }

    private static string ReadRepoFile(params string[] relativeParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var parts = new string[relativeParts.Length + 1];
            parts[0] = directory.FullName;
            Array.Copy(relativeParts, 0, parts, 1, relativeParts.Length);
            var candidate = Path.Combine(parts);
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }
            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate repository file: {Path.Combine(relativeParts)}");
    }
}
