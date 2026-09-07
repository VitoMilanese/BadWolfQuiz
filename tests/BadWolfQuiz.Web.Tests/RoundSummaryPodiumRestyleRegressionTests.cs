namespace BadWolfQuiz.Web.Tests;

public sealed class RoundSummaryPodiumRestyleRegressionTests
{
    [Fact]
    public void Host_markup_keeps_round_and_final_podium_contracts()
    {
        var markup = ReadWebFile("Pages", "Admin", "Games", "Lobby.cshtml");

        Assert.Contains("class=\"round-summary\"", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"round-podium\"", markup, StringComparison.Ordinal);
        Assert.Contains("round-podium-player", markup, StringComparison.Ordinal);
        Assert.Contains("round-podium-position", markup, StringComparison.Ordinal);
        Assert.Contains("round-podium-score", markup, StringComparison.Ordinal);
        Assert.Contains("round-winner", markup, StringComparison.Ordinal);
        Assert.Contains("FinalQuestion_Results", markup, StringComparison.Ordinal);
        Assert.Contains("FinalStandings.Take(3)", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Shared_styles_center_one_two_or_three_podium_cards()
    {
        var sharedStyles = ReadWebFile("wwwroot", "css", "busy-indicators.css");
        var styles = ReadWebFile("wwwroot", "css", "round-summary-podium.css")
            .ReplaceLineEndings("\n");

        Assert.Contains("@import url(\"./round-summary-podium.css?v=2\");", sharedStyles, StringComparison.Ordinal);
        Assert.Contains("body.gameplay-layout:has(.host-game-board .round-summary)", styles, StringComparison.Ordinal);
        Assert.Contains(".final-question-host[data-game-status=\"completed\"]", styles, StringComparison.Ordinal);
        Assert.Contains("display: flex;", styles, StringComparison.Ordinal);
        Assert.Contains("justify-content: center;", styles, StringComparison.Ordinal);
        Assert.Contains("align-items: end;", styles, StringComparison.Ordinal);
        Assert.Contains("flex: 0 1 340px;", styles, StringComparison.Ordinal);
        Assert.Contains(".round-podium-player.round-winner", styles, StringComparison.Ordinal);
        Assert.Contains("color: var(--gold);", styles, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 760px)", styles, StringComparison.Ordinal);
        Assert.Contains("flex-direction: column;", styles, StringComparison.Ordinal);
        Assert.Contains("align-items: stretch;", styles, StringComparison.Ordinal);
        Assert.Contains("@media (max-height: 700px) and (min-width: 761px)", styles, StringComparison.Ordinal);
        Assert.Contains("@media (prefers-reduced-motion: reduce)", styles, StringComparison.Ordinal);
        Assert.Contains("animation: none !important;", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void Existing_podium_refresh_and_empty_round_guards_remain_intact()
    {
        var script = ReadWebFile("wwwroot", "js", "site.js");
        var markup = ReadWebFile("Pages", "Admin", "Games", "Lobby.cshtml");

        Assert.Contains("const advanceEmptyRoundSummary = () =>", script, StringComparison.Ordinal);
        Assert.Contains("summary.querySelector(\".round-podium-player\")", script, StringComparison.Ordinal);
        Assert.Contains("const animatedLeaderboardSignature = view =>", markup, StringComparison.Ordinal);
        Assert.Contains(".round-summary, .final-question-panel", markup, StringComparison.Ordinal);
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
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate web file: {string.Join('/', parts)}");
    }
}
