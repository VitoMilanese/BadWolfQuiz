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
            "const showBuzzerRace = race =>",
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
    public void Question_judging_bypasses_global_html_navigation()
    {
        var markup = File.ReadAllText(FindLobbyView());
        var script = File.ReadAllText(FindWebFile("wwwroot", "js", "site.js"));

        var gameplayFormStart = script.IndexOf(
            "const isGameplayForm = form =>",
            StringComparison.Ordinal);
        var gameplayFormEnd = script.IndexOf(
            "document.addEventListener(\"click\"",
            gameplayFormStart,
            StringComparison.Ordinal);
        var gameplayFormBlock = script[gameplayFormStart..gameplayFormEnd];

        Assert.Contains(
            "!form.matches(\".question-judge-actions\")",
            gameplayFormBlock);
        Assert.Contains("event.target.matches(\".question-judge-actions\")", markup);
        Assert.Contains("await submitGameControl(form, event.submitter);", markup);
    }

    [Fact]
    public void Buzzer_race_is_rendered_directly_from_signalr_state()
    {
        var markup = File.ReadAllText(FindLobbyView());
        var hub = File.ReadAllText(FindWebFile("Hubs", "GameHub.cs"));

        Assert.Contains("const showBuzzerRace = race =>", markup);
        Assert.Contains("container.replaceChildren(overlay);", markup);
        Assert.Contains("initializeAutoFitCard(card);", markup);
        Assert.Contains("badwolf:buzzer-race", markup);
        Assert.Contains("{ detail: update.buzzerRace }", markup);
        Assert.Contains(".then(showBuzzerRaceOverlay);", markup);
        Assert.DoesNotContain("Model.Game.BuzzerRace is { } buzzerRace", markup);

        Assert.Contains("buzzerRace = game.BuzzerRace is { } race &&", hub);
        Assert.DoesNotContain("LatePlayers.Count: > 0", hub);
    }

    [Fact]
    public void Buzzer_and_score_overlays_share_the_five_second_visual_lifetime()
    {
        var markup = File.ReadAllText(FindLobbyView());

        Assert.Contains("buzzerOverlayDismissHandle = window.setTimeout(() =>", markup);
        Assert.Contains("}, 5000);", markup);
        Assert.Contains("data-answer-result-overlay", markup);
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
