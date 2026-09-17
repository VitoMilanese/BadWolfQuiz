using System.Xml.Linq;
using BadWolfQuiz.Web.Services;

namespace BadWolfQuiz.Web.Tests;

public sealed class AchievementCategoryFilterRegressionTests
{
    private static readonly string[] OutOfGameAchievementCodes =
    [
        "Registered",
        "GitHubVisitor",
        "PasswordChanged",
        "DeveloperContacted",
        "DeveloperReplied",
        "Contributor"
    ];

    [Fact]
    public void Built_in_achievement_catalog_is_partitioned_into_requested_categories()
    {
        var guessWhatIPlay = PlayerAchievementService.Catalog
            .Where(definition =>
                PlayerAchievementCategoryCatalog.GetCategory(definition.Code) ==
                PlayerAchievementCategory.GuessWhatIPlay)
            .Select(definition => definition.Code)
            .ToArray();
        Assert.Equal(["SoloAi", "RoomCreatorWin"], guessWhatIPlay);

        var wordRings = PlayerAchievementService.Catalog
            .Where(definition => definition.Code.StartsWith("WordRings", StringComparison.Ordinal))
            .ToArray();
        Assert.NotEmpty(wordRings);
        Assert.All(
            wordRings,
            definition => Assert.Equal(
                PlayerAchievementCategory.WordRings,
                PlayerAchievementCategoryCatalog.GetCategory(definition.Code)));

        var outOfGame = PlayerAchievementService.Catalog
            .Where(definition =>
                PlayerAchievementCategoryCatalog.GetCategory(definition.Code) ==
                PlayerAchievementCategory.OutOfGame)
            .Select(definition => definition.Code)
            .ToArray();
        Assert.Equal(OutOfGameAchievementCodes, outOfGame);

        var quizAchievements = PlayerAchievementService.Catalog
            .Where(definition =>
                !definition.Code.StartsWith("WordRings", StringComparison.Ordinal) &&
                definition.Code is not "SoloAi" and not "RoomCreatorWin" &&
                !OutOfGameAchievementCodes.Contains(definition.Code, StringComparer.Ordinal))
            .ToArray();
        Assert.NotEmpty(quizAchievements);
        Assert.All(
            quizAchievements,
            definition => Assert.Equal(
                PlayerAchievementCategory.Quizzes,
                PlayerAchievementCategoryCatalog.GetCategory(definition.Code)));
    }

    [Theory]
    [InlineData("FirstGame", "quizzes")]
    [InlineData("SoloAi", "guess-what-i-play")]
    [InlineData("RoomCreatorWin", "guess-what-i-play")]
    [InlineData("WordRingsSoloAi", "word-rings")]
    [InlineData("WordRingsActionShieldAvoid", "word-rings")]
    [InlineData("Registered", "out-of-game")]
    [InlineData("GitHubVisitor", "out-of-game")]
    [InlineData("PasswordChanged", "out-of-game")]
    [InlineData("DeveloperContacted", "out-of-game")]
    [InlineData("DeveloperReplied", "out-of-game")]
    [InlineData("Contributor", "out-of-game")]
    public void Category_filter_values_match_client_contract(string code, string expected)
    {
        Assert.Equal(expected, PlayerAchievementCategoryCatalog.GetFilterValue(code));
    }

    [Fact]
    public void Achievements_page_wires_category_combobox_client_filter_and_visible_count()
    {
        var root = FindRepositoryRoot();
        var page = Read(root, "src", "BadWolfQuiz.Web", "Pages", "Achievements.cshtml");
        var script = Read(root, "src", "BadWolfQuiz.Web", "wwwroot", "js", "achievement-category-filter.js");
        var styles = Read(root, "src", "BadWolfQuiz.Web", "wwwroot", "css", "achievement-category-filter.css");

        Assert.Contains("data-achievement-category-filter-scope", page, StringComparison.Ordinal);
        Assert.Contains("data-achievement-category-filter", page, StringComparison.Ordinal);
        Assert.Contains("<option value=\"all\">", page, StringComparison.Ordinal);
        Assert.Contains("<option value=\"quizzes\">", page, StringComparison.Ordinal);
        Assert.Contains("<option value=\"guess-what-i-play\">", page, StringComparison.Ordinal);
        Assert.Contains("<option value=\"word-rings\">", page, StringComparison.Ordinal);
        Assert.Contains("<option value=\"out-of-game\">", page, StringComparison.Ordinal);
        Assert.Contains("PlayerAchievementCategoryCatalog.GetFilterValue(achievement.Code)", page, StringComparison.Ordinal);
        Assert.Contains("data-achievement-category=\"@category\"", page, StringComparison.Ordinal);
        Assert.Contains("data-achievement-visible-count", page, StringComparison.Ordinal);
        Assert.Contains("~/js/achievement-category-filter.js", page, StringComparison.Ordinal);
        Assert.Contains("~/css/achievement-category-filter.css", page, StringComparison.Ordinal);

        Assert.Contains("initializedScopes = new WeakSet()", script, StringComparison.Ordinal);
        Assert.Contains("data-achievement-category-filter-scope", script, StringComparison.Ordinal);
        Assert.Contains("selectedCategory === \"all\"", script, StringComparison.Ordinal);
        Assert.Contains("card.dataset.achievementCategory === selectedCategory", script, StringComparison.Ordinal);
        Assert.Contains("card.hidden = !isVisible", script, StringComparison.Ordinal);
        Assert.Contains("card.classList.contains(\"is-unlocked\")", script, StringComparison.Ordinal);
        Assert.Contains("window.BadWolfAchievementCategoryFilter = { initialize }", script, StringComparison.Ordinal);
        Assert.Contains("new MutationObserver", script, StringComparison.Ordinal);
        Assert.Contains("attributeFilter: [\"class\"]", script, StringComparison.Ordinal);
        Assert.Contains("[data-achievement-category-filter-scope] .player-achievement-card[hidden]", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void Player_and_host_achievement_dialogs_use_the_same_category_filter()
    {
        var root = FindRepositoryRoot();
        var playerTagHelper = Read(root, "src", "BadWolfQuiz.Web", "TagHelpers", "PlayerAchievementsTagHelper.cs");
        var hostTagHelper = Read(root, "src", "BadWolfQuiz.Web", "TagHelpers", "HostPlayerAchievementsAssetsTagHelper.cs");
        var hostScript = Read(root, "src", "BadWolfQuiz.Web", "wwwroot", "js", "host-player-achievements.js");
        var hostEndpoint = Read(root, "src", "BadWolfQuiz.Web", "Pages", "Admin", "Games", "PlayerAchievements.cshtml.cs");

        Assert.Contains("IStringLocalizer<AchievementCategoryFilterResource> categoryFilterLocalizer", playerTagHelper, StringComparison.Ordinal);
        Assert.Contains("data-achievement-category-filter-scope", playerTagHelper, StringComparison.Ordinal);
        Assert.Contains("AppendCategoryOption(html, \"all\", \"All\")", playerTagHelper, StringComparison.Ordinal);
        Assert.Contains("AppendCategoryOption(html, \"quizzes\", \"Quizzes\")", playerTagHelper, StringComparison.Ordinal);
        Assert.Contains("AppendCategoryOption(html, \"guess-what-i-play\", \"GuessWhatIPlay\")", playerTagHelper, StringComparison.Ordinal);
        Assert.Contains("AppendCategoryOption(html, \"word-rings\", \"WordRings\")", playerTagHelper, StringComparison.Ordinal);
        Assert.Contains("AppendCategoryOption(html, \"out-of-game\", \"OutOfGame\")", playerTagHelper, StringComparison.Ordinal);
        Assert.Contains("PlayerAchievementCategoryCatalog.GetFilterValue(achievement.Code)", playerTagHelper, StringComparison.Ordinal);
        Assert.Contains("/css/achievement-category-filter.css?v=2", playerTagHelper, StringComparison.Ordinal);
        Assert.Contains("/js/achievement-category-filter.js?v=2", playerTagHelper, StringComparison.Ordinal);

        Assert.Contains("/css/achievement-category-filter.css?v=2", hostTagHelper, StringComparison.Ordinal);
        Assert.Contains("/js/achievement-category-filter.js?v=2", hostTagHelper, StringComparison.Ordinal);
        Assert.Contains("/js/host-player-achievements.js?v=8", hostTagHelper, StringComparison.Ordinal);

        Assert.Contains("data-achievement-category-filter-scope", hostScript, StringComparison.Ordinal);
        Assert.Contains("<option value=\"out-of-game\">", hostScript, StringComparison.Ordinal);
        Assert.Contains("data-achievement-category=\"${escapeHtml(item.category)}\"", hostScript, StringComparison.Ordinal);
        Assert.Contains("BadWolfAchievementCategoryFilter?.initialize(targetDialog)", hostScript, StringComparison.Ordinal);

        Assert.Contains("IStringLocalizer<AchievementCategoryFilterResource> categoryFilterLocalizer", hostEndpoint, StringComparison.Ordinal);
        Assert.Contains("outOfGame = categoryFilterLocalizer[\"OutOfGame\"].Value", hostEndpoint, StringComparison.Ordinal);
        Assert.Contains("category = PlayerAchievementCategoryCatalog.GetFilterValue(item.Code)", hostEndpoint, StringComparison.Ordinal);
        Assert.Contains("unlockedCountTemplate = localizer[\"Achievements_UnlockedCount\"].Value", hostEndpoint, StringComparison.Ordinal);
    }

    [Fact]
    public void Category_filter_resources_cover_supported_languages_and_requested_ukrainian_labels()
    {
        var root = FindRepositoryRoot();
        var localizationRoot = Path.Combine(root, "src", "BadWolfQuiz.Web", "Resources", "Localization");
        var fileNames = new[]
        {
            "AchievementCategoryFilterResource.resx",
            "AchievementCategoryFilterResource.uk.resx",
            "AchievementCategoryFilterResource.it.resx",
            "AchievementCategoryFilterResource.ru.resx"
        };
        var requiredKeys = new[] { "Label", "All", "Quizzes", "GuessWhatIPlay", "WordRings", "OutOfGame" };

        foreach (var fileName in fileNames)
        {
            var document = XDocument.Load(Path.Combine(localizationRoot, fileName));
            var keys = document.Root!
                .Elements("data")
                .Select(element => (string?)element.Attribute("name"))
                .Where(name => name is not null)
                .ToHashSet(StringComparer.Ordinal);
            Assert.All(requiredKeys, key => Assert.Contains(key, keys));
        }

        var ukrainian = XDocument.Load(Path.Combine(
            localizationRoot,
            "AchievementCategoryFilterResource.uk.resx"));
        Assert.Equal("Усі", GetValue(ukrainian, "All"));
        Assert.Equal("Квізи", GetValue(ukrainian, "Quizzes"));
        Assert.Equal("У що я граю?", GetValue(ukrainian, "GuessWhatIPlay"));
        Assert.Equal("Слівце в кільце", GetValue(ukrainian, "WordRings"));
        Assert.Equal("Поза грою", GetValue(ukrainian, "OutOfGame"));

        var russian = XDocument.Load(Path.Combine(
            localizationRoot,
            "AchievementCategoryFilterResource.ru.resx"));
        Assert.All(requiredKeys, key => Assert.Equal("Україна", GetValue(russian, key)));
    }

    private static string GetValue(XDocument document, string key) =>
        document.Root!
            .Elements("data")
            .Single(element => string.Equals((string?)element.Attribute("name"), key, StringComparison.Ordinal))
            .Element("value")!
            .Value;

    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(Path.Combine([root, .. parts])).ReplaceLineEndings("\n");

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
