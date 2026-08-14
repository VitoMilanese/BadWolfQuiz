namespace BadWolfQuiz.Web.Tests;

public sealed class GameplayOverlayRegressionTests
{
    [Fact]
    public void Partial_gameplay_refresh_reinitializes_dynamic_overlay_auto_fit()
    {
        var markup = File.ReadAllText(FindLobbyView());

        Assert.Contains("const initializeAutoFitCard = card =>", markup);
        Assert.Contains("const initializeGameplayOverlays = () =>", markup);
        Assert.Contains(
            "document.querySelectorAll(\"[data-auto-fit-overlay]\").forEach(",
            markup);
        Assert.Contains("card.classList.add(\"is-auto-fit-ready\");", markup);

        var transientReplace = markup.IndexOf(
            "currentTransient.replaceChildren(",
            StringComparison.Ordinal);
        var overlayInitialization = markup.IndexOf(
            "initializeGameplayOverlays();",
            transientReplace,
            StringComparison.Ordinal);

        Assert.True(transientReplace >= 0);
        Assert.True(overlayInitialization > transientReplace);
    }

    [Fact]
    public void Answer_result_overlay_uses_the_full_five_second_lifetime()
    {
        var markup = File.ReadAllText(FindLobbyView());
        var css = File.ReadAllText(FindWebFile("wwwroot", "css", "site.css"));

        var dynamicStart = markup.IndexOf(
            "const initializeGameplayOverlays = () =>",
            StringComparison.Ordinal);
        var dynamicEnd = markup.IndexOf(
            "const syncHeaderState = () =>",
            dynamicStart,
            StringComparison.Ordinal);
        var dynamicInitializer = markup[dynamicStart..dynamicEnd];

        Assert.Contains("window.setTimeout(() => overlay.remove(), 5000);", dynamicInitializer);
        Assert.Contains("animation: buzzer-race-overlay-life 5s ease forwards;", css);

        var initialStart = markup.IndexOf(
            "const overlay = document.querySelector(\"[data-answer-result-overlay]\")",
            StringComparison.Ordinal);
        var initialEnd = markup.IndexOf("        })();", initialStart, StringComparison.Ordinal);
        var initialInitializer = markup[initialStart..initialEnd];

        Assert.Contains("}, 5000);", initialInitializer);
        Assert.DoesNotContain("}, 1500);", initialInitializer);
    }

    [Fact]
    public void Buzzer_overlay_keeps_winner_and_late_player_names_in_server_markup()
    {
        var markup = File.ReadAllText(FindLobbyView());

        Assert.Contains("Model.Game.BuzzerRace is { } buzzerRace", markup);
        Assert.Contains("buzzerRace.WinnerPlayerName", markup);
        Assert.Contains("buzzerRace.LatePlayers", markup);
        Assert.Contains("<div class=\"buzzer-race-card\" data-auto-fit-overlay>", markup);
    }

    private static string FindLobbyView() =>
        FindWebFile("Pages", "Admin", "Games", "Lobby.cshtml");

    private static string FindWebFile(params string[] pathParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var src = Path.Combine(directory.FullName, "src", "BadWolfQuiz.Web");
            if (Directory.Exists(src))
            {
                return Path.Combine(new[] { src }.Concat(pathParts).ToArray());
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
