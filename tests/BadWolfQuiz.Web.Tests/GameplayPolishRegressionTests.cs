namespace BadWolfQuiz.Web.Tests;

public sealed class GameplayPolishRegressionTests
{
    [Fact]
    public void Gameplay_polish_assets_cover_intro_wait_player_runtime_and_answering_name()
    {
        var imports = File.ReadAllText(FindWebFile(
            "Pages",
            "_ViewImports.cshtml"));
        var helper = File.ReadAllText(FindWebFile(
            "TagHelpers",
            "GameplayPolishAssetsTagHelper.cs"));
        var styles = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "gameplay-polish.css"));
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "gameplay-polish.js"));
        var intro = File.ReadAllText(FindWebFile(
            "Pages",
            "Admin",
            "Games",
            "RoundIntro.cshtml"));
        var player = File.ReadAllText(FindWebFile(
            "Pages",
            "Player",
            "Lobby.cshtml"));

        Assert.Contains(
            "GameplayPolishAssetsTagHelper",
            imports,
            StringComparison.Ordinal);
        Assert.Contains(
            "Attributes = \"data-game-intro-page\"",
            helper,
            StringComparison.Ordinal);
        Assert.Contains(
            "Attributes = \"data-game-code,data-player-id,data-final-status\"",
            helper,
            StringComparison.Ordinal);
        Assert.Contains(
            "Attributes = \"data-host-gameplay-view\"",
            helper,
            StringComparison.Ordinal);
        Assert.Contains("gameplay-polish.css?v=1", helper, StringComparison.Ordinal);
        Assert.Contains("gameplay-polish.js?v=1", helper, StringComparison.Ordinal);

        Assert.Contains("data-game-intro-start", intro, StringComparison.Ordinal);
        Assert.Contains("is-leaving", intro, StringComparison.Ordinal);
        Assert.Contains(
            ".game-intro-page.is-starting-game.is-leaving",
            styles,
            StringComparison.Ordinal);
        Assert.Contains("animation: none !important;", styles, StringComparison.Ordinal);
        Assert.Contains("BadWolfBusy?.show?.()", script, StringComparison.Ordinal);
        Assert.Contains("badwolf:host-shell-mounted", script, StringComparison.Ordinal);

        Assert.Contains("data-final-status=", player, StringComparison.Ordinal);
        Assert.Contains("is-page-buzzer-active", player, StringComparison.Ordinal);
        Assert.Contains("player-buzzer-open", script, StringComparison.Ordinal);
        Assert.Contains("new MutationObserver", script, StringComparison.Ordinal);
        Assert.Contains(
            "playerLobby.dataset.finalStatus = \"running\";",
            script,
            StringComparison.Ordinal);

        Assert.Contains(
            ".scoreboard-player.question-answering-player",
            styles,
            StringComparison.Ordinal);
        Assert.Contains("color: var(--text) !important;", styles, StringComparison.Ordinal);
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
