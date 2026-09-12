namespace BadWolfQuiz.Web.Tests;

public sealed class DebugAchievementUnlockRegressionTests
{
    [Fact]
    public void Debug_mode_exposes_random_achievement_unlock_for_first_player()
    {
        var root = FindRepositoryRoot();
        string Read(params string[] parts) => File.ReadAllText(Path.Combine([root, .. parts]));

        var endpoint = Read(
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "Games",
            "DebugRandomAchievement.cshtml.cs");
        var assets = Read(
            "src",
            "BadWolfQuiz.Web",
            "TagHelpers",
            "DebugAchievementUnlockAssetsTagHelper.cs");
        var script = Read(
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "debug-achievement-unlock.js");
        var imports = Read(
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "_ViewImports.cshtml");

        Assert.Contains("configuration.GetValue<bool>(\"DebugMode\")", endpoint, StringComparison.Ordinal);
        Assert.Contains("game.Session.Players.FirstOrDefault()", endpoint, StringComparison.Ordinal);
        Assert.Contains("PlayerAchievementService.Catalog", endpoint, StringComparison.Ordinal);
        Assert.Contains("Random.Shared.Next", endpoint, StringComparison.Ordinal);
        Assert.Contains("LoadPersistedUnlocksAsync", endpoint, StringComparison.Ordinal);
        Assert.Contains("GetPendingAchievementCodes", endpoint, StringComparison.Ordinal);
        Assert.Contains("UnlockPlayerAsync", endpoint, StringComparison.Ordinal);
        Assert.Contains("Group(GameHub.GroupName(game.PublicCode))", endpoint, StringComparison.Ordinal);
        Assert.Contains("\"AchievementUnlocked\"", endpoint, StringComparison.Ordinal);

        Assert.Contains("configuration.GetValue<bool>(\"DebugMode\")", assets, StringComparison.Ordinal);
        Assert.Contains("/Admin/Games/Lobby", assets, StringComparison.Ordinal);
        Assert.Contains("data-debug-achievement-antiforgery", assets, StringComparison.Ordinal);
        Assert.Contains("debug-achievement-unlock.js", assets, StringComparison.Ordinal);

        Assert.Contains(".game-header-context", script, StringComparison.Ordinal);
        Assert.Contains("data-debug-achievement-unlock", script, StringComparison.Ordinal);
        Assert.Contains("/Admin/Games/DebugRandomAchievement", script, StringComparison.Ordinal);
        Assert.Contains("__RequestVerificationToken", script, StringComparison.Ordinal);
        Assert.Contains("🏆", script, StringComparison.Ordinal);

        Assert.Contains(
            "@addTagHelper BadWolfQuiz.Web.TagHelpers.DebugAchievementUnlockAssetsTagHelper, BadWolfQuiz.Web",
            imports,
            StringComparison.Ordinal);
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
