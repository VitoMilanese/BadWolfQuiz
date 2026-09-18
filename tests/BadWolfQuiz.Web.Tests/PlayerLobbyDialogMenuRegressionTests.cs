namespace BadWolfQuiz.Web.Tests;

public sealed class PlayerLobbyDialogMenuRegressionTests
{
    [Fact]
    public void Player_lobby_uses_top_action_menu_and_dialogs_instead_of_expanders()
    {
        var root = FindRepositoryRoot();
        var page = File.ReadAllText(Path.Combine(
            root, "src", "BadWolfQuiz.Web", "Pages", "Player", "Lobby.cshtml"));
        var css = File.ReadAllText(Path.Combine(
            root, "src", "BadWolfQuiz.Web", "wwwroot", "css", "player-lobby-dialog-menu.css"));
        var script = File.ReadAllText(Path.Combine(
            root, "src", "BadWolfQuiz.Web", "wwwroot", "js", "player-lobby-dialog-menu.js"));
        var contributor = File.ReadAllText(Path.Combine(
            root, "src", "BadWolfQuiz.Web", "wwwroot", "js", "contributor-frames.js"));
        var ukrainian = File.ReadAllText(Path.Combine(
            root, "src", "BadWolfQuiz.Web", "Resources", "Localization", "SharedResource.uk.resx"));

        Assert.Contains("class=\"player-name-line\"", page);
        Assert.Contains("data-open-player-lobby-menu", page);
        Assert.Contains("id=\"player-lobby-action-menu\"", page);
        Assert.Contains("id=\"player-menu-dialog\"", page);
        Assert.Contains("id=\"player-media-dialog\"", page);
        Assert.Contains("data-player-media-dialog", page);
        Assert.Contains("data-player-dialog-target=\"player-menu-dialog\"", page);
        Assert.Contains("data-player-dialog-target=\"player-media-dialog\"", page);
        Assert.Contains("data-player-dialog-target=\"player-achievements-dialog\"", page);
        Assert.Contains("data-player-achievements-open", page);
        Assert.Contains("hidden>", page);
        Assert.DoesNotContain("<details class=\"player-media-settings", page);
        Assert.DoesNotContain("<summary>@Localizer[\"PlayerMenu_Title\"]", page);
        Assert.DoesNotContain("<summary>@Localizer[\"PlayerMedia_Settings\"]", page);

        Assert.Contains("transform: translateX(100%)", css);
        Assert.Contains("@media (max-width: 700px)", css);
        Assert.Contains("transform: translateY(-100%)", css);
        Assert.Contains("right: calc(0.75rem + 3.35rem)", css);
        Assert.Contains("display: flex !important", css);

        Assert.Contains("menu.showModal()", script);
        Assert.Contains("menu.classList.add(\"is-open\")", script);
        Assert.Contains("pendingDialog = target", script);
        Assert.Contains("target.showModal()", script);
        Assert.Contains("[data-player-settings-dialog]", script);
        Assert.Contains("dialog.close()", script);

        Assert.Contains("[data-player-media-dialog] .player-media-settings-content", contributor);
        Assert.Contains("<data name=\"PlayerMenu_Achievements\"", ukrainian);
        Assert.Contains("<value>Досягнення</value>", ukrainian);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src")) &&
                Directory.Exists(Path.Combine(directory.FullName, "tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
