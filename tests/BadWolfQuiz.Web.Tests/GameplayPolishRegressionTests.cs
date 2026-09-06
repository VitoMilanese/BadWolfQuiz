namespace BadWolfQuiz.Web.Tests;

public sealed class GameplayPolishRegressionTests
{
    [Fact]
    public void Gameplay_polish_assets_cover_intro_wait_player_layout_and_answering_name()
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
        var playerWaitingStyles = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "player-lobby-waiting-room-fixes.css"));
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
        Assert.Contains("gameplay-polish.css?v=3", helper, StringComparison.Ordinal);
        Assert.Contains("gameplay-polish.js?v=3", helper, StringComparison.Ordinal);
        Assert.Contains(
            "output.Attributes.SetAttribute(\"data-final-status\", \"lobby\")",
            helper,
            StringComparison.Ordinal);

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
        Assert.Contains(
            ".player-lobby[data-final-status=\"lobby\"] > .player-buzzer-panel > .player-buzzer",
            playerWaitingStyles,
            StringComparison.Ordinal);
        Assert.Contains("aspect-ratio: auto;", playerWaitingStyles, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "playerLobby.dataset.finalStatus = \"running\";",
            script,
            StringComparison.Ordinal);
        Assert.DoesNotContain("player-runtime-layout", script, StringComparison.Ordinal);

        Assert.Contains(
            ".game-scoreboard .scoreboard-player:not(.host-card)",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            "grid-template-columns: minmax(0, 1fr) !important;",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            "padding: 14px 16px !important;",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            ".game-scoreboard .scoreboard-player:not(.host-card) .player-card-actions",
            styles,
            StringComparison.Ordinal);
        Assert.Contains("position: absolute !important;", styles, StringComparison.Ordinal);
        Assert.Contains("right: 8px !important;", styles, StringComparison.Ordinal);
        Assert.Contains(
            "@media (hover: hover) and (pointer: fine)",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(".scoreboard-remove-player", styles, StringComparison.Ordinal);
        Assert.Contains("opacity: 0;", styles, StringComparison.Ordinal);
        Assert.Contains(":hover .scoreboard-remove-player", styles, StringComparison.Ordinal);
        Assert.Contains(":focus-within .scoreboard-remove-player", styles, StringComparison.Ordinal);

        Assert.Contains(
            ".scoreboard-player.question-answering-player",
            styles,
            StringComparison.Ordinal);
        Assert.Contains("color: var(--text) !important;", styles, StringComparison.Ordinal);

        Assert.Contains(
            "[data-join-code-panel] .join-code-floating-content .join-code-value",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "[data-join-code-panel] .join-code-floating-content .join-qr-code",
            script,
            StringComparison.Ordinal);
        Assert.Contains("panel?.dataset.gameCode", script, StringComparison.Ordinal);
        Assert.Contains("navigator.clipboard?.writeText", script, StringComparison.Ordinal);
        Assert.Contains("document.execCommand(\"copy\")", script, StringComparison.Ordinal);
        Assert.Contains(
            "`/Join/${encodeURIComponent(gameCode)}/`",
            script,
            StringComparison.Ordinal);
        Assert.Contains("event.stopImmediatePropagation();", script, StringComparison.Ordinal);
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
