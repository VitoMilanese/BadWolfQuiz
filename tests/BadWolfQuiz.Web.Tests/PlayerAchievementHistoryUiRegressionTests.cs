using System.Xml.Linq;

namespace BadWolfQuiz.Web.Tests;

public sealed class PlayerAchievementHistoryUiRegressionTests
{
    [Fact]
    public void Host_tools_and_history_page_are_wired_without_replacing_the_existing_player_dialog_endpoint()
    {
        var root = FindRepositoryRoot();
        var lobby = File.ReadAllText(Path.Combine(
            root, "src", "BadWolfQuiz.Web", "Pages", "Admin", "Games", "Lobby.cshtml"));
        var layout = File.ReadAllText(Path.Combine(
            root, "src", "BadWolfQuiz.Web", "Pages", "Shared", "_Layout.cshtml"));
        var page = File.ReadAllText(Path.Combine(
            root, "src", "BadWolfQuiz.Web", "Pages", "Admin", "Games", "PlayerAchievements.cshtml"));
        var model = File.ReadAllText(Path.Combine(
            root, "src", "BadWolfQuiz.Web", "Pages", "Admin", "Games", "PlayerAchievements.cshtml.cs"));
        var css = File.ReadAllText(Path.Combine(
            root, "src", "BadWolfQuiz.Web", "wwwroot", "css", "player-achievement-history.css"));
        var script = File.ReadAllText(Path.Combine(
            root, "src", "BadWolfQuiz.Web", "wwwroot", "js", "player-achievement-history.js"));

        Assert.Contains("asp-page=\"/Admin/Games/PlayerAchievements\"", lobby);
        Assert.Contains("History_Open", lobby);
        Assert.Contains("asp-page=\"/Admin/Games/PlayerAchievements\"", layout);
        Assert.Contains("History_Open", layout);

        Assert.Contains("History_FilterGame", page);
        Assert.Contains("History_FilterPlayers", page);
        Assert.Contains("asp-route-selected-player-id", page);
        Assert.Contains("History_NoGameUnlocks", page);
        Assert.Contains("History_NoPlayerUnlocks", page);
        Assert.Contains("History_NoPlayers", page);
        Assert.Contains("data-achievement-history-time", page);
        Assert.Contains("player-achievement-history.js", page);

        Assert.Contains("sessionRegistry.FindOwned", model);
        Assert.Contains("if (playerId.HasValue)", model);
        Assert.Contains("BuildPlayerDialogJsonAsync", model);
        Assert.Contains("LoadForPlayerAsync", model);
        Assert.Contains("LoadPersistedUnlocksAsync", model);
        Assert.Contains("SourceGameSessionId", model);
        Assert.Contains("definition is null", model);

        Assert.Contains("max-height: min(68vh, 720px)", css);
        Assert.Contains("overflow: auto", css);
        Assert.Contains("Intl.DateTimeFormat", script);
        Assert.Contains("data-achievement-history-time", script);
    }

    [Theory]
    [InlineData("PlayerAchievementHistoryResource.resx")]
    [InlineData("PlayerAchievementHistoryResource.uk.resx")]
    [InlineData("PlayerAchievementHistoryResource.it.resx")]
    [InlineData("PlayerAchievementHistoryResource.ru.resx")]
    public void History_resources_have_every_required_ui_key(string fileName)
    {
        var root = FindRepositoryRoot();
        var document = XDocument.Load(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Resources",
            "Localization",
            fileName));
        var values = document.Root!
            .Elements("data")
            .ToDictionary(
                element => element.Attribute("name")!.Value,
                element => element.Element("value")!.Value,
                StringComparer.Ordinal);
        var keys = new[]
        {
            "History_Open",
            "History_Title",
            "History_Eyebrow",
            "History_Description",
            "History_BackToGame",
            "History_FilterLabel",
            "History_FilterGame",
            "History_FilterPlayers",
            "History_PlayerSelector",
            "History_NoPlayers",
            "History_NoGameUnlocks",
            "History_NoPlayerUnlocks",
            "History_UnlockedAt",
            "History_ReadOnly",
            "History_UnknownDescription",
            "History_EntryCount",
            "History_CurrentGame"
        };

        Assert.All(keys, key => Assert.True(
            values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value),
            $"Missing or empty {key} in {fileName}."));

        if (fileName.EndsWith(".ru.resx", StringComparison.Ordinal))
        {
            Assert.All(keys, key => Assert.Equal("Україна", values[key]));
        }
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
