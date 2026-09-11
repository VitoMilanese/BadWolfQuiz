using System.Text.Json;
using System.Text.Json.Nodes;
using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Services;

namespace BadWolfQuiz.Web.Tests;

public sealed class CategoryColorsSettingsTests
{
    [Fact]
    public void Category_colors_are_enabled_by_default_and_round_trip_through_settings_input()
    {
        Assert.True(GameSessionSettings.Default.CategoryColorsEnabled);

        var input = GameSettingsInput.From(GameSessionSettings.Default);
        Assert.True(input.CategoryColorsEnabled);

        input.CategoryColorsEnabled = false;
        var settings = input.ToRuntimeSettings();
        Assert.False(settings.CategoryColorsEnabled);
        Assert.False(GameSettingsInput.From(settings).CategoryColorsEnabled);
    }

    [Fact]
    public void Legacy_game_session_settings_json_defaults_category_colors_to_enabled()
    {
        var settings = new GameSessionSettings(
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(10),
            GamePhaseStartMode.Manual,
            GamePhaseStartMode.Automatic,
            categoryColorsEnabled: false);

        var json = JsonSerializer.Serialize(settings);
        var roundTrip = JsonSerializer.Deserialize<GameSessionSettings>(json);
        Assert.NotNull(roundTrip);
        Assert.False(roundTrip.CategoryColorsEnabled);

        var legacy = JsonNode.Parse(json)!.AsObject();
        Assert.True(legacy.Remove(nameof(GameSessionSettings.CategoryColorsEnabled)));
        var legacyRoundTrip = JsonSerializer.Deserialize<GameSessionSettings>(legacy.ToJsonString());
        Assert.NotNull(legacyRoundTrip);
        Assert.True(legacyRoundTrip.CategoryColorsEnabled);
    }

    [Fact]
    public void Global_and_game_settings_expose_category_colors_and_board_supports_live_preview()
    {
        var globalSettings = ReadWebFile("Pages", "Admin", "Settings", "Index.cshtml");
        var gameSettings = ReadWebFile("Pages", "Admin", "Games", "_GameSettingsFields.cshtml");
        var lobby = ReadWebFile("Pages", "Admin", "Games", "Lobby.cshtml");
        var boardScript = ReadWebFile("wwwroot", "js", "board-header-layout.js");

        Assert.Contains("asp-for=\"Input.CategoryColorsEnabled\"", globalSettings, StringComparison.Ordinal);
        Assert.Contains("asp-for=\"SettingsInput.CategoryColorsEnabled\"", gameSettings, StringComparison.Ordinal);
        Assert.Contains("data-category-colors-enabled-toggle", gameSettings, StringComparison.Ordinal);
        Assert.Contains("document.currentScript?.closest(\"form\")", gameSettings, StringComparison.Ordinal);
        Assert.Contains("syncCategoryColorsToggleFromPersisted", gameSettings, StringComparison.Ordinal);
        Assert.Contains("data-open-game-settings", gameSettings, StringComparison.Ordinal);
        Assert.Contains("grid.dataset.categoryColorsPreview = categoryColorsToggle.checked", gameSettings, StringComparison.Ordinal);
        Assert.Contains("grid.removeAttribute(\"data-category-colors-preview\")", gameSettings, StringComparison.Ordinal);
        Assert.Contains("data-category-colors-enabled=\"@Model.Game.Session.Settings.CategoryColorsEnabled", lobby, StringComparison.Ordinal);
        Assert.Contains("nextGrid.dataset.categoryColorsEnabled", lobby, StringComparison.Ordinal);
        Assert.Contains("grid.dataset.categoryColorsPreview ??", boardScript, StringComparison.Ordinal);
        Assert.Contains("grid.dataset.categoryColorsEnabled ??", boardScript, StringComparison.Ordinal);
        Assert.Contains("badwolf:category-colors-enabled-changed", boardScript, StringComparison.Ordinal);

        var disabledGate = boardScript.IndexOf("if (!colorsEnabled)", StringComparison.Ordinal);
        var perCategoryMode = boardScript.IndexOf("const mode = column.dataset.categoryColorMode", StringComparison.Ordinal);
        Assert.True(disabledGate >= 0 && perCategoryMode > disabledGate);
    }

    private static string ReadWebFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                new[] { directory.FullName, "src", "BadWolfQuiz.Web" }
                    .Concat(parts)
                    .ToArray());
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate).Replace("\r\n", "\n", StringComparison.Ordinal);
            }
            directory = directory.Parent;
        }

        throw new FileNotFoundException(Path.Combine(parts));
    }
}
