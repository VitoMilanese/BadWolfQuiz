using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace BadWolfQuiz.Web.Tests;

public sealed class BackgroundStarsRegressionTests : IDisposable
{
    private readonly string _contentRoot = Path.Combine(
        Path.GetTempPath(),
        "BadWolfQuiz.BackgroundStars.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Animated_stars_default_on_and_disabled_value_round_trips()
    {
        Assert.True(GameSessionSettings.Default.AnimatedStarsEnabled);

        var disabled = new GameSessionSettings(
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(10),
            GamePhaseStartMode.Manual,
            GamePhaseStartMode.Automatic,
            animatedStarsEnabled: false);
        var store = new GameSettingsStore(new TestWebHostEnvironment(_contentRoot));

        await store.SaveAsync("stars-host", disabled);
        var loaded = await store.LoadAsync("stars-host");
        var input = GameSettingsInput.From(loaded);

        Assert.False(loaded.AnimatedStarsEnabled);
        Assert.False(input.AnimatedStarsEnabled);
        Assert.False(input.ToRuntimeSettings().AnimatedStarsEnabled);
    }

    [Fact]
    public async Task Legacy_settings_without_star_field_keep_stars_enabled()
    {
        var appData = Path.Combine(_contentRoot, "App_Data");
        Directory.CreateDirectory(appData);
        await File.WriteAllTextAsync(
            Path.Combine(appData, "game-settings.json"),
            """
            {
              "BuzzerDuration": "00:00:30",
              "AnswerDuration": "00:00:10",
              "RegularQuestionBuzzerStartMode": "Manual",
              "WagerQuestionAnswerTimerStartMode": "Automatic",
              "AllowNegativeScoreFinalPlayers": true,
              "SiteThemeId": "classic-wolf"
            }
            """);
        var store = new GameSettingsStore(new TestWebHostEnvironment(_contentRoot));

        var loaded = await store.LoadAsync("legacy-host");

        Assert.True(loaded.AnimatedStarsEnabled);
    }

    [Fact]
    public void Global_starfield_assets_and_settings_toggle_are_wired()
    {
        var imports = ReadWebFile("Pages", "_ViewImports.cshtml");
        var tagHelper = ReadWebFile("TagHelpers", "BackgroundStarsTagHelper.cs");
        var css = ReadWebFile("wwwroot", "css", "background-stars.css");
        var script = ReadWebFile("wwwroot", "js", "background-stars.js");

        Assert.Contains("BackgroundStarsAssetsTagHelper", imports, StringComparison.Ordinal);
        Assert.Contains("BackgroundStarsBodyTagHelper", imports, StringComparison.Ordinal);
        Assert.Contains("BackgroundStarsSettingsTagHelper", imports, StringComparison.Ordinal);
        Assert.Contains("data-site-starfield", tagHelper, StringComparison.Ordinal);
        Assert.Contains("Input.AnimatedStarsEnabled", tagHelper, StringComparison.Ordinal);
        Assert.Contains("GameThemeSettings", tagHelper, StringComparison.Ordinal);
        Assert.Contains("background-stars.css?v=3", tagHelper, StringComparison.Ordinal);
        Assert.Contains("background-stars.js?v=3", tagHelper, StringComparison.Ordinal);
        Assert.Contains("@keyframes badwolf-star-pulse", css, StringComparison.Ordinal);
        Assert.Contains("@keyframes badwolf-bubble-float", css, StringComparison.Ordinal);
        Assert.Contains("prefers-reduced-motion", css, StringComparison.Ordinal);
        Assert.Contains("Math.random()", script, StringComparison.Ordinal);
        Assert.Contains("minimumDistance", script, StringComparison.Ordinal);
        Assert.Contains("maxStars = 52", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Light_themes_use_colored_bubbles_and_rings()
    {
        var css = ReadWebFile("wwwroot", "css", "background-stars.css");
        var script = ReadWebFile("wwwroot", "js", "background-stars.js");

        Assert.Contains("[data-starfield-tone=\"light\"]", css, StringComparison.Ordinal);
        Assert.Contains(".site-starfield-star.is-ring", css, StringComparison.Ordinal);
        Assert.Contains(".site-starfield-star.is-color-5", css, StringComparison.Ordinal);
        Assert.Contains("--bubble-size", css, StringComparison.Ordinal);
        Assert.Contains("ring ? 'is-ring' : 'is-orb'", script, StringComparison.Ordinal);
        Assert.Contains("`is-color-${colorIndex + 1}`", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Starfield_particles_are_built_once_and_rehosting_does_not_randomize_them()
    {
        var script = ReadWebFile("wwwroot", "js", "background-stars.js");

        Assert.Contains("let particlesBuilt = false", script, StringComparison.Ordinal);
        Assert.Contains("const buildParticles = () =>", script, StringComparison.Ordinal);
        Assert.Contains("if (particlesBuilt)", script, StringComparison.Ordinal);
        Assert.Contains("particlesBuilt = true", script, StringComparison.Ordinal);
        Assert.Contains("const scheduleHostRefresh = () =>", script, StringComparison.Ordinal);
        Assert.Contains("ensureStarfieldHost();", script, StringComparison.Ordinal);
        Assert.DoesNotContain("resizeTimer", script, StringComparison.Ordinal);
        Assert.DoesNotContain("new MutationObserver(() => renderStars())", script, StringComparison.Ordinal);
        Assert.DoesNotContain("renderStars();", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Global_starfield_uses_layout_safe_hosts_for_portal_and_gameplay_surfaces()
    {
        var css = ReadWebFile("wwwroot", "css", "background-stars.css");
        var script = ReadWebFile("wwwroot", "js", "background-stars.js");

        Assert.Contains(".site-starfield-host", css, StringComparison.Ordinal);
        Assert.Contains("position: fixed !important", css, StringComparison.Ordinal);
        Assert.Contains("body:has(.minigame-editor-heading) > .page-shell.site-starfield-host > .site-starfield", css, StringComparison.Ordinal);
        Assert.Contains(".answer-key-page.site-starfield-host > .site-starfield", css, StringComparison.Ordinal);
        Assert.Contains(".player-lobby.site-starfield-host:is(", css, StringComparison.Ordinal);
        Assert.Contains("resolveStarfieldHost", script, StringComparison.Ordinal);
        Assert.Contains("main.page-shell", script, StringComparison.Ordinal);
        Assert.Contains("[data-game-code][data-player-id][data-final-status]", script, StringComparison.Ordinal);
        Assert.Contains("[data-host-gameplay-view]", script, StringComparison.Ordinal);
        Assert.Contains(".answer-key-page", script, StringComparison.Ordinal);
        Assert.Contains("[data-host-gameplay-board]:not([hidden])", script, StringComparison.Ordinal);
        Assert.Contains("nextHost.append(field)", script, StringComparison.Ordinal);
        Assert.Contains("new MutationObserver(scheduleHostRefresh).observe(hostGameplayView", script, StringComparison.Ordinal);
        Assert.Contains("attributeFilter: ['data-theme']", script, StringComparison.Ordinal);
        Assert.DoesNotContain("attributeFilter: ['hidden', 'data-game-status']", script, StringComparison.Ordinal);
        Assert.DoesNotContain("pageShell?.firstElementChild", script, StringComparison.Ordinal);
        Assert.DoesNotContain("nextHost.prepend(field)", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Settings_toggle_updates_the_starfield_without_a_reload()
    {
        var script = ReadWebFile("wwwroot", "js", "background-stars.js");

        Assert.Contains("getElementById('Input_AnimatedStarsEnabled')", script, StringComparison.Ordinal);
        Assert.Contains("settingsToggle.addEventListener('change'", script, StringComparison.Ordinal);
        Assert.Contains("personalPreference = settingsToggle.checked", script, StringComparison.Ordinal);
        Assert.Contains("setEnabled(personalPreference)", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Quiz_and_both_minigames_synchronize_the_host_preference()
    {
        var api = ReadWebFile("Pages", "BackgroundStarsApi.cshtml.cs");
        var script = ReadWebFile("wwwroot", "js", "background-stars.js");

        Assert.Contains("sessionRegistry.Find(normalizedCode)", api, StringComparison.Ordinal);
        Assert.Contains("settings.AnimatedStarsEnabled", api, StringComparison.Ordinal);
        Assert.Contains(".PlayerNumber == 1", api, StringComparison.Ordinal);
        Assert.Contains("WordRingsRoomHostCoordinator", api, StringComparison.Ordinal);
        Assert.Contains(".IsHost", api, StringComparison.Ordinal);
        Assert.Contains("currentHost.Id", api, StringComparison.Ordinal);
        Assert.Contains("/minigames/guess-what-i-play", script, StringComparison.Ordinal);
        Assert.Contains("/minigames/word-rings", script, StringComparison.Ordinal);
        Assert.Contains("pathSegments[0] === 'player'", script, StringComparison.Ordinal);
        Assert.Contains("pathSegments[1] === 'lobby'", script, StringComparison.Ordinal);
        Assert.Contains("normalizeCode(pathSegments[2])", script, StringComparison.Ordinal);
        Assert.Contains("badwolf-minigame-player:", script, StringComparison.Ordinal);
        Assert.Contains("badwolf.wordrings.room.", script, StringComparison.Ordinal);
        Assert.Contains("setInterval(() => synchronizeAppearance(true), 12000)", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Background_star_localization_follows_supported_language_policy()
    {
        var english = ReadWebFile("Resources", "Localization", "BackgroundStarsResource.resx");
        var ukrainian = ReadWebFile("Resources", "Localization", "BackgroundStarsResource.uk.resx");
        var italian = ReadWebFile("Resources", "Localization", "BackgroundStarsResource.it.resx");
        var russian = ReadWebFile("Resources", "Localization", "BackgroundStarsResource.ru.resx");

        Assert.Contains("Animated night-sky stars", english, StringComparison.Ordinal);
        Assert.Contains("Анімовані зірки нічного неба", ukrainian, StringComparison.Ordinal);
        Assert.Contains("Stelle animate del cielo notturno", italian, StringComparison.Ordinal);
        Assert.DoesNotContain("Animated night-sky stars", russian, StringComparison.Ordinal);
        Assert.Contains("<value>Україна</value>", russian, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_contentRoot))
        {
            Directory.Delete(_contentRoot, true);
        }
    }

    private static string ReadWebFile(params string[] relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                new[] { directory.FullName, "src", "BadWolfQuiz.Web" }
                    .Concat(relativePath)
                    .ToArray());
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }
            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate BadWolfQuiz.Web/{string.Join('/', relativePath)}.");
    }

    private sealed class TestWebHostEnvironment(string contentRootPath)
        : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "BadWolfQuiz.Web.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = contentRootPath;
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
