namespace BadWolfQuiz.Web.Tests;

public sealed class SelfAchievementsPageRegressionTests
{
    [Fact]
    public void Authenticated_main_menu_links_to_self_achievements_page()
    {
        var root = FindRepositoryRoot();
        var layout = Normalize(File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Shared",
            "_Layout.cshtml")));

        Assert.Contains(
            "<a class=\"header-side-menu-item\" asp-page=\"/Admin/Games/Statistics\">@Localizer[\"Menu_PlayerStatistics\"]</a>\n" +
            "                        <a class=\"header-side-menu-item\" asp-page=\"/Achievements\">@AchievementLocalizer[\"Achievements_Title\"]</a>",
            layout,
            StringComparison.Ordinal);
        Assert.Contains(
            "@inject IStringLocalizer<AchievementResource> AchievementLocalizer",
            layout,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Self_achievements_page_uses_account_identity_and_existing_achievement_evaluation()
    {
        var root = FindRepositoryRoot();
        var pageModel = Normalize(File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Achievements.cshtml.cs")));

        Assert.Contains("[Authorize]", pageModel, StringComparison.Ordinal);
        Assert.Contains("var accountId = currentHost.RequiredId;", pageModel, StringComparison.Ordinal);
        Assert.Contains("ContributorRecognition.IsContributor(", pageModel, StringComparison.Ordinal);
        Assert.Contains("new PlayerAchievementService(db).LoadForPlayerAsync(", pageModel, StringComparison.Ordinal);
        Assert.Contains("hostId: null", pageModel, StringComparison.Ordinal);
        Assert.Contains("playerName: string.Empty", pageModel, StringComparison.Ordinal);
        Assert.Contains("currentGameCode: null", pageModel, StringComparison.Ordinal);
        Assert.Contains("accountId: accountId", pageModel, StringComparison.Ordinal);
    }

    [Fact]
    public void Self_achievements_page_reuses_buzzer_dialog_card_markup_and_assets()
    {
        var root = FindRepositoryRoot();
        var page = Normalize(File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Achievements.cshtml")));

        Assert.Contains("~/css/player-achievements.css", page, StringComparison.Ordinal);
        Assert.Contains("~/js/achievement-image-trim.js", page, StringComparison.Ordinal);
        Assert.Contains("class=\"player-achievements-grid\"", page, StringComparison.Ordinal);
        Assert.Contains("player-achievement-card is-unlocked", page, StringComparison.Ordinal);
        Assert.Contains("player-achievement-card is-locked", page, StringComparison.Ordinal);
        Assert.Contains("class=\"player-achievement-card-top\"", page, StringComparison.Ordinal);
        Assert.Contains("class=\"player-achievement-image\"", page, StringComparison.Ordinal);
        Assert.Contains("class=\"player-achievement-progress\"", page, StringComparison.Ordinal);
        Assert.Contains("Achievements_SecretTitle", page, StringComparison.Ordinal);
        Assert.Contains("Achievements_SecretDescription", page, StringComparison.Ordinal);
    }

    [Fact]
    public void Self_achievements_page_uses_new_full_width_portal_styling()
    {
        var root = FindRepositoryRoot();
        var page = Normalize(File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Achievements.cshtml")));
        var styles = Normalize(File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "css",
            "achievements-page.css")));

        Assert.Contains("class=\"self-achievements-page\"", page, StringComparison.Ordinal);
        Assert.Contains("class=\"self-achievements-hero\"", page, StringComparison.Ordinal);
        Assert.Contains("body.portal-layout:has(.self-achievements-page) > .page-shell", styles, StringComparison.Ordinal);
        Assert.Contains(".self-achievements-hero::after", styles, StringComparison.Ordinal);
        Assert.Contains(".self-achievements-content", styles, StringComparison.Ordinal);
    }

    private static string Normalize(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal);

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
