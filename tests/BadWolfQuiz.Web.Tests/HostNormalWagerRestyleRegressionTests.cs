namespace BadWolfQuiz.Web.Tests;

public sealed class HostNormalWagerRestyleRegressionTests
{
    [Fact]
    public void Host_normal_wager_uses_player_wager_visual_language_without_replacing_runtime()
    {
        var helper = File.ReadAllText(FindWebFile(
            "TagHelpers",
            "AnonymousSharedWagerAssetsTagHelper.cs"));
        var styles = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "host-normal-wager-restyle.css"))
            .ReplaceLineEndings("\n");
        var lobby = File.ReadAllText(FindWebFile(
            "Pages",
            "Admin",
            "Games",
            "Lobby.cshtml"));

        Assert.Contains(
            "/css/host-normal-wager-restyle.css?v=2",
            helper,
            StringComparison.Ordinal);

        Assert.Contains(
            ".current-question-summary.wager-mode:not(.anonymous-shared-wager-mode):has(> .wager-entry-panel)",
            styles,
            StringComparison.Ordinal);
        Assert.Contains("width: min(560px, 100%) !important;", styles, StringComparison.Ordinal);
        Assert.Contains("border-radius: 24px !important;", styles, StringComparison.Ordinal);
        Assert.Contains("grid-template-columns: repeat(3, minmax(0, 1fr)) !important;", styles, StringComparison.Ordinal);
        Assert.Contains("min-height: 76px !important;", styles, StringComparison.Ordinal);
        Assert.Contains("font-size: clamp(2.4rem, 7vw, 4.4rem) !important;", styles, StringComparison.Ordinal);
        Assert.Contains("min-height: 54px !important;", styles, StringComparison.Ordinal);
        Assert.Contains("grid-column: 1 / -1 !important;", styles, StringComparison.Ordinal);
        Assert.Contains(".wager-validation.wager-valid", styles, StringComparison.Ordinal);

        Assert.Contains("class=\"wager-entry-panel\"", lobby, StringComparison.Ordinal);
        Assert.Contains("data-wager-form", lobby, StringComparison.Ordinal);
        Assert.Contains("asp-page-handler=\"SubmitQuestionWager\"", lobby, StringComparison.Ordinal);
        Assert.Contains("data-minimum=\"@wagerLimits.Minimum\"", lobby, StringComparison.Ordinal);
        Assert.Contains("data-maximum=\"@wagerLimits.Maximum\"", lobby, StringComparison.Ordinal);
    }

    [Fact]
    public void Host_normal_wager_resets_legacy_two_column_child_placement()
    {
        var styles = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "host-normal-wager-restyle.css"))
            .ReplaceLineEndings("\n");

        Assert.Contains(
            "> .wager-entry-panel > .wager-player-summary,",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            "> .wager-entry-panel > .question-wager-form {",
            styles,
            StringComparison.Ordinal);
        Assert.Contains("position: static !important;", styles, StringComparison.Ordinal);
        Assert.Contains("grid-column: 1 !important;", styles, StringComparison.Ordinal);
        Assert.Contains("grid-row: auto !important;", styles, StringComparison.Ordinal);
        Assert.Contains("order: 1 !important;", styles, StringComparison.Ordinal);
        Assert.Contains("order: 2 !important;", styles, StringComparison.Ordinal);
        Assert.Contains("overflow: hidden !important;", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void Host_normal_wager_restyle_does_not_target_all_player_or_anonymous_shared_wagers()
    {
        var styles = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "host-normal-wager-restyle.css"));

        Assert.Contains(
            ":not(.anonymous-shared-wager-mode):has(> .wager-entry-panel)",
            styles,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            ".all-player-wager-waiting {",
            styles,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            ".anonymous-shared-wager-host-panel {",
            styles,
            StringComparison.Ordinal);
    }

    private static string FindWebFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                new[]
                {
                    directory.FullName,
                    "src",
                    "BadWolfQuiz.Web"
                }.Concat(parts).ToArray());

            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate BadWolfQuiz.Web file: {string.Join('/', parts)}");
    }
}
