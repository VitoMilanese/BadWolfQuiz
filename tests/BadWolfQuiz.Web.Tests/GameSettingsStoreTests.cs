using BadWolfQuiz.Game.Runtime;
using Microsoft.AspNetCore.Hosting;
using BadWolfQuiz.Web.Services;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace BadWolfQuiz.Web.Tests;

public sealed class GameSettingsStoreTests : IDisposable
{
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

        await store.SaveAsync(expected);
        var actual = await store.LoadAsync();

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

        await store.SaveAsync(expected);
        var actual = await store.LoadAsync();

        Assert.False(actual.AllowNegativeScoreFinalPlayers);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task Save_and_load_preserve_disabled_late_joining()
    {
        var store = new GameSettingsStore(
            new TestWebHostEnvironment(_contentRoot));
        var expected = new GameSessionSettings(
            TimeSpan.FromSeconds(47),
            TimeSpan.FromSeconds(13),
            GamePhaseStartMode.Automatic,
            GamePhaseStartMode.Manual,
            allowNegativeScoreFinalPlayers: true,
            allowNewPlayersAfterStart: false);

        await store.SaveAsync(expected);
        var actual = await store.LoadAsync();

        Assert.False(actual.AllowNewPlayersAfterStart);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task Load_returns_engine_defaults_when_file_does_not_exist()
    {
        var store = new GameSettingsStore(
            new TestWebHostEnvironment(_contentRoot));

        var settings = await store.LoadAsync();

        Assert.Equal(GameSessionSettings.Default, settings);
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
