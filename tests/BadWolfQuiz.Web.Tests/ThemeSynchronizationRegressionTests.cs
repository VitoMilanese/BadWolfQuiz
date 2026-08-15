namespace BadWolfQuiz.Web.Tests;

public sealed class ThemeSynchronizationRegressionTests
{
    [Fact]
    public void Player_page_loads_current_host_theme_instead_of_stale_game_snapshot()
    {
        var source = File.ReadAllText(FindWebFile("Pages", "Player", "Lobby.cshtml.cs"));
        Assert.Contains("settingsStore.LoadAsync(game.HostId, cancellationToken)", source);
        Assert.Contains("ViewData[\"GameThemeSettings\"] = themeSettings;", source);
    }

    [Fact]
    public void Connected_player_applies_live_site_theme_updates()
    {
        var source = File.ReadAllText(FindWebFile("Pages", "Player", "Lobby.cshtml"));
        Assert.Contains("connection.on(\"SiteThemeChanged\", applySiteTheme);", source);
        Assert.Contains("root.dataset.theme = update.siteThemeId;", source);
        Assert.Contains("root.style.cssText = update.customThemeStyle || \"\";", source);
    }

    [Fact]
    public void Saving_host_settings_broadcasts_theme_to_active_games()
    {
        var source = File.ReadAllText(FindWebFile("Pages", "Admin", "Settings", "Index.cshtml.cs"));
        Assert.Contains("GameHub.CreateThemeUpdate(savedSettings)", source);
        Assert.Contains(".Group(GameHub.GroupName(game.PublicCode))", source);
        Assert.Contains(".SendAsync(\"SiteThemeChanged\", themeUpdate, cancellationToken)", source);
    }

    [Fact]
    public void Theme_payload_supports_builtin_and_custom_themes()
    {
        var source = File.ReadAllText(FindWebFile("Hubs", "GameHub.cs"));
        Assert.Contains("SiteThemeCatalog.Normalize(settings.SiteThemeId)", source);
        Assert.Contains("SiteThemeCatalog.BuildCssVariables(settings.CustomThemeColors)", source);
    }

    private static string FindWebFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                new[] { directory.FullName, "src", "BadWolfQuiz.Web" }
                    .Concat(parts)
                    .ToArray());
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate BadWolfQuiz.Web file: {Path.Combine(parts)}");
    }
}
