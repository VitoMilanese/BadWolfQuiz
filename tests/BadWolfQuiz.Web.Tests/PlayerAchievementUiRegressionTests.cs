namespace BadWolfQuiz.Web.Tests;

public sealed class PlayerAchievementUiRegressionTests
{
    [Fact]
    public void Player_lobby_exposes_achievements_from_the_top_player_label_in_a_fullscreen_dialog()
    {
        var root = FindRepositoryRoot();
        var tagHelper = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "TagHelpers",
            "PlayerAchievementsTagHelper.cs"));
        var hostTagHelper = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "TagHelpers",
            "HostPlayerAchievementsAssetsTagHelper.cs"));
        var imports = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "_ViewImports.cshtml"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "css",
            "player-achievements.css"));
        var script = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "player-achievements.js"));
        var hostScript = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "host-player-achievements.js"));
        var trimScript = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "achievement-image-trim.js"));
        var ukrainian = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Resources",
            "Localization",
            "AchievementResource.uk.resx"));

        Assert.Contains("PlayerAchievementAssetsTagHelper", imports);
        Assert.Contains("PlayerAchievementsTagHelper", imports);
        Assert.Contains("GitHubAchievementLinkTagHelper", imports);
        Assert.Contains("/css/player-achievements.css?v=12", tagHelper);
        Assert.Contains("/js/achievement-image-trim.js?v=1", tagHelper);
        Assert.Contains("/js/player-achievements.js?v=1", tagHelper);
        Assert.Contains("/css/player-achievements.css?v=11", hostTagHelper);
        Assert.Contains("/js/achievement-image-trim.js?v=1", hostTagHelper);
        Assert.Contains("/js/host-player-achievements.js?v=7", hostTagHelper);
        Assert.Contains("data-player-achievements-label", tagHelper);
        Assert.Contains("Achievements_PlayerLabel", tagHelper);
        Assert.Contains("player-achievements-dialog", tagHelper);
        Assert.Contains("player-achievements-dialog-body", tagHelper);
        Assert.Contains("data-player-achievements-close", tagHelper);
        Assert.Contains("Achievements_UnlockedCount", tagHelper);
        Assert.Contains("IsNewInCurrentGame", tagHelper);
        Assert.Contains("player-achievement-unlocked", tagHelper);
        Assert.Contains("Achievements_SecretTitle", tagHelper);
        Assert.Contains("Achievements_SecretDescription", tagHelper);
        Assert.Contains("lockedSecret", tagHelper);
        Assert.Contains("OrderBy(GetAchievementSortGroup)", tagHelper);
        Assert.Contains("achievement.IsUnlocked ? 0 : achievement.IsSecret ? 2 : 1", tagHelper);
        Assert.Contains("/images/achievements/", tagHelper);
        Assert.Contains("player-achievement-image", tagHelper);
        Assert.Contains("player-achievement-state is-secret-unlocked", tagHelper);
        Assert.Contains("player-achievement-secret-revealed", tagHelper);
        Assert.Contains("player-achievement-secret-eye", tagHelper);
        Assert.Contains("player-achievement-secret-pupil", tagHelper);
        Assert.Contains("player-achievement-secret-check", tagHelper);
        Assert.Contains("M2.4 12s3.5-5.2", tagHelper);
        Assert.DoesNotContain("M7.8 8.4a4.2", tagHelper);
        Assert.Contains("achievement.Progress", tagHelper);
        Assert.Contains("achievement.Target", tagHelper);
        Assert.Contains("LinkPlayerAccount", tagHelper);

        Assert.Contains(".player-achievements-open", css);
        Assert.Contains(".player-achievements-dialog", css);
        Assert.Contains("height: calc(100dvh - 16px)", css);
        Assert.Contains("height: 100dvh", css);
        Assert.Contains(".player-achievements-dialog-body", css);
        Assert.Contains("overflow: hidden", css);
        Assert.DoesNotContain("position: sticky", css);
        Assert.Contains(".player-achievements-grid", css);
        Assert.Contains("minmax(min(100%, 240px), 1fr)", css);
        Assert.Contains("align-items: start", css);
        Assert.Contains("padding: 10px 12px", css);
        Assert.Contains(".player-achievement-card-top", css);
        Assert.Contains("height: 176px", css);
        Assert.Contains("margin: -10px -12px 6px", css);
        Assert.Contains("padding: 4px 12px", css);
        Assert.Contains("linear-gradient(", css);
        Assert.Contains(".player-achievement-image", css);
        Assert.Contains("width: 168px", css);
        Assert.Contains("height: 168px", css);
        Assert.Contains("object-fit: contain", css);
        Assert.Contains("background: transparent", css);
        Assert.Contains(".player-achievement-card.is-locked .player-achievement-image", css);
        Assert.Contains("filter: grayscale(1)", css);
        Assert.Contains(".player-achievement-state-icon", css);
        Assert.Contains("width: 19px", css);
        Assert.Contains(".player-achievement-secret-pupil", css);
        Assert.Contains(".player-achievement-secret-check", css);
        Assert.Contains("stroke: var(--bg)", css);
        Assert.DoesNotContain(".player-achievement-state-check", css);
        Assert.DoesNotContain(".player-achievement-card.is-unlocked .player-achievement-image", css);
        Assert.DoesNotContain("margin: -18px -18px 16px", css);
        Assert.DoesNotContain("padding: 24px 18px 28px", css);
        Assert.DoesNotContain("width: 64px", css);
        Assert.Contains(".player-achievement-progress", css);
        Assert.Contains(".player-achievement-card.is-new", css);
        Assert.Contains("@media (max-width: 600px)", css);
        Assert.Contains("grid-template-columns: 1fr", css);

        Assert.Contains("dataset.playerAchievementsLabel", script);
        Assert.Contains("player-achievements-open", script);
        Assert.Contains("dialog.showModal()", script);
        Assert.Contains("dialog.close()", script);
        Assert.Contains("playerLine.replaceChildren(opener)", script);

        Assert.Contains("achievementSortGroup", hostScript);
        Assert.Contains("item.isUnlocked ? 0 : item.isSecret ? 2 : 1", hostScript);
        Assert.Contains("player-achievement-state is-secret-unlocked", hostScript);
        Assert.Contains("player-achievement-secret-revealed", hostScript);
        Assert.Contains("secretUnlockedStateIcon", hostScript);
        Assert.Contains("player-achievement-secret-eye", hostScript);
        Assert.Contains("player-achievement-secret-pupil", hostScript);
        Assert.Contains("player-achievement-secret-check", hostScript);
        Assert.Contains("M2.4 12s3.5-5.2", hostScript);
        Assert.DoesNotContain("M7.8 8.4a4.2", hostScript);

        Assert.Contains(".player-achievement-image", trimScript);
        Assert.Contains("image.naturalWidth", trimScript);
        Assert.Contains("image.naturalHeight", trimScript);
        Assert.Contains("context.getImageData", trimScript);
        Assert.Contains("+ 3] === 0", trimScript);
        Assert.Contains("visibleWidth", trimScript);
        Assert.Contains("visibleHeight", trimScript);
        Assert.Contains("artworkSize / visibleWidth", trimScript);
        Assert.Contains("artworkSize / visibleHeight", trimScript);
        Assert.Contains("image.dataset.alphaTrimmed = \"true\"", trimScript);
        Assert.Contains("MutationObserver", trimScript);

        Assert.Contains("<value>Досягнення</value>", ukrainian);
        Assert.Contains("<value>Досягнення гравця</value>", ukrainian);
        Assert.Contains("<value>Велика гра</value>", ukrainian);
        Assert.Contains("15 000", ukrainian);
        Assert.Contains("<value>З безодні</value>", ukrainian);
        Assert.Contains("<value>До зграї</value>", ukrainian);
        Assert.Contains("<value>Своя арена</value>", ukrainian);
        Assert.Contains("<value>Гра на виїзді</value>", ukrainian);
        Assert.Contains("<value>Проти машини</value>", ukrainian);
        Assert.Contains("<value>Господар кімнати</value>", ukrainian);
        Assert.Contains("<value>Під капотом</value>", ukrainian);
        Assert.Contains("<value>Суддя</value>", ukrainian);
        Assert.Contains("<value>Все на стіл</value>", ukrainian);
        Assert.Contains("<value>Ва-банк не зайшов</value>", ukrainian);
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
