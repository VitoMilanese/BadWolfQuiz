namespace BadWolfQuiz.Web.Tests;

public sealed class PlayerBuzzerTouchRegressionTests
{
    [Fact]
    public void Player_buzzer_blocks_horizontal_touch_panning()
    {
        var root = FindRepositoryRoot();
        var css = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "wwwroot", "css", "player-admission-menu.css"));

        Assert.Contains("body:has(.player-buzzer-panel)", css);
        Assert.DoesNotContain(":has(.player-lobby:has(.player-buzzer-panel))", css);
        Assert.Contains("overscroll-behavior-x: none;", css);
        Assert.Contains("touch-action: manipulation;", css);
        Assert.Contains(".player-lobby:has(.player-buzzer-panel) .player-buzzer", css);
        Assert.Contains("touch-action: none;", css);
        Assert.Contains("-webkit-touch-callout: none;", css);
    }

    [Fact]
    public void Player_buzzer_uses_available_mobile_panel_space()
    {
        var root = FindRepositoryRoot();
        var css = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "wwwroot", "css", "player-admission-menu.css"));

        Assert.Contains("body:has(.player-buzzer-panel) > .page-shell", css);
        Assert.Contains("position: fixed;", css);
        Assert.Contains("height: 100dvh;", css);
        Assert.Contains("flex: 1 1 auto;", css);
        Assert.DoesNotContain("flex: 1 1 0;", css);
        Assert.DoesNotContain("height: 0;", css);
        Assert.Contains("aspect-ratio: auto;", css);
        Assert.Contains("border-radius: clamp(24px, 8vw, 56px);", css);
        Assert.Contains("user-select: none;", css);
        Assert.Contains("-webkit-user-select: none;", css);
    }

    [Fact]
    public void Player_buzzer_fires_on_primary_pointer_down()
    {
        var root = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "wwwroot", "js", "player-admission-menu.js"));

        Assert.Contains("document.getElementById(\"player-buzzer\")", script);
        Assert.Contains("buzzerButton.addEventListener(\"pointerdown\"", script);
        Assert.Contains("event.pointerType !== \"mouse\" || event.button === 0", script);
        Assert.Contains("event.isPrimary", script);
        Assert.Contains("event.preventDefault();", script);
        Assert.Contains("buzzerButton.click();", script);
        Assert.Contains("{ passive: false }", script);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src")) && Directory.Exists(Path.Combine(directory.FullName, "tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
