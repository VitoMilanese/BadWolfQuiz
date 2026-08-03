using BadWolfQuiz.Game.Runtime;
using Microsoft.AspNetCore.Hosting;
using BadWolfQuiz.Web.Services;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace BadWolfQuiz.Web.Tests;

public sealed class GameSettingsStoreTests : IDisposable
{
    private const string HostId = "host-a";
    private readonly string _contentRoot = Path.Combine(
        Path.GetTempPath(),
        "BadWolfQuiz.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Save_and_load_preserve_global_defaults()
    {
        var store = new GameSettingsStore(
            new TestWebHostEnvironment(_contentRoot));
        var expected = new GameSessionSettings(
            TimeSpan.FromSeconds(47),
            TimeSpan.FromSeconds(13),
            GamePhaseStartMode.Automatic,
            GamePhaseStartMode.Manual);

        await store.SaveAsync(HostId, expected);
        var actual = await store.LoadAsync(HostId);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task Save_and_load_preserve_disabled_negative_final_participation()
    {
        var store = new GameSettingsStore(
            new TestWebHostEnvironment(_contentRoot));
        var expected = new GameSessionSettings(
            TimeSpan.FromSeconds(47),
            TimeSpan.FromSeconds(13),
            GamePhaseStartMode.Automatic,
            GamePhaseStartMode.Manual,
            allowNegativeScoreFinalPlayers: false);

        await store.SaveAsync(HostId, expected);
        var actual = await store.LoadAsync(HostId);

        Assert.False(actual.AllowNegativeScoreFinalPlayers);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task Save_and_load_preserve_host_card_settings_and_image()
    {
        var store = new GameSettingsStore(
            new TestWebHostEnvironment(_contentRoot));
        var expectedImage = new byte[] { 1, 2, 3, 4 };
        var expectedLogo = new byte[] { 5, 6, 7 };
        var expectedTheme = new SiteThemeColors(
            "#010203",
            "#111213",
            "#212223",
            "#f1f2f3",
            "#a1a2a3",
            "#314159",
            "#abcdef",
            "#fedcba");
        var expected = new GameSessionSettings(
            TimeSpan.FromSeconds(47),
            TimeSpan.FromSeconds(13),
            GamePhaseStartMode.Automatic,
            GamePhaseStartMode.Manual,
            hostName: "Host",
            hostVisualSource: HostVisualSource.Image,
            hostImageData: expectedImage,
            hostImageContentType: "image/png",
            hostAvatarId: "F/17.png",
            brandLogoData: expectedLogo,
            brandLogoContentType: "image/webp",
            siteThemeId: "custom",
            customThemeColors: expectedTheme);

        await store.SaveAsync(HostId, expected);
        var actual = await store.LoadAsync(HostId);

        Assert.Equal("Host", actual.HostName);
        Assert.Equal(HostVisualSource.Image, actual.HostVisualSource);
        Assert.Equal(expectedImage, actual.HostImageData);
        Assert.Equal("image/png", actual.HostImageContentType);
        Assert.Equal("F/17.png", actual.HostAvatarId);
        Assert.Equal(expectedLogo, actual.BrandLogoData);
        Assert.Equal("image/webp", actual.BrandLogoContentType);
        Assert.Equal("custom", actual.SiteThemeId);
        Assert.Equal(expectedTheme.Background, actual.CustomThemeColors.Background);
        Assert.Equal(expectedTheme.AccentBright, actual.CustomThemeColors.AccentBright);
        Assert.True(actual.HasHostCard);
    }

    [Fact]
    public void Stored_image_does_not_show_host_card_when_source_is_none()
    {
        var settings = new GameSessionSettings(
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(10),
            GamePhaseStartMode.Manual,
            GamePhaseStartMode.Automatic,
            hostImageData: new byte[] { 1, 2, 3 },
            hostImageContentType: "image/png");

        Assert.False(settings.HasHostCard);
    }

    [Fact]
    public async Task Load_returns_engine_defaults_when_file_does_not_exist()
    {
        var store = new GameSettingsStore(
            new TestWebHostEnvironment(_contentRoot));

        var settings = await store.LoadAsync(HostId);

        Assert.Equal(GameSessionSettings.Default, settings);
    }

    [Fact]
    public async Task Settings_are_isolated_between_hosts()
    {
        var store = new GameSettingsStore(
            new TestWebHostEnvironment(_contentRoot));
        var first = new GameSessionSettings(
            TimeSpan.FromSeconds(17),
            TimeSpan.FromSeconds(10),
            GamePhaseStartMode.Manual,
            GamePhaseStartMode.Automatic,
            hostName: "First host");
        var second = new GameSessionSettings(
            TimeSpan.FromSeconds(43),
            TimeSpan.FromSeconds(10),
            GamePhaseStartMode.Manual,
            GamePhaseStartMode.Automatic,
            hostName: "Second host");

        await store.SaveAsync("host-one", first);
        await store.SaveAsync("host-two", second);

        Assert.Equal(first, await store.LoadAsync("host-one"));
        Assert.Equal(second, await store.LoadAsync("host-two"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_contentRoot))
        {
            Directory.Delete(_contentRoot, true);
        }
    }

    private sealed class TestWebHostEnvironment(string contentRootPath)
        : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "BadWolfQuiz.Web.Tests";

        public IFileProvider WebRootFileProvider { get; set; } =
            new NullFileProvider();

        public string WebRootPath { get; set; } = contentRootPath;

        public string EnvironmentName { get; set; } =
            Environments.Development;

        public string ContentRootPath { get; set; } = contentRootPath;

        public IFileProvider ContentRootFileProvider { get; set; } =
            new NullFileProvider();
    }
}
