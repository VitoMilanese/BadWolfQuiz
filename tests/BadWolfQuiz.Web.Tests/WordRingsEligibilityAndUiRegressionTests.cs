using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Tests;

public sealed class WordRingsEligibilityAndUiRegressionTests
{
    [Fact]
    public async Task Word_Rings_achievements_require_a_registered_host_or_registered_player()
    {
        await using var fixture = await AchievementFixture.CreateAsync();
        var wordRings = new WordRingsAchievementService(fixture.Db);
        var achievements = new PlayerAchievementService(fixture.Db);

        Assert.False(await wordRings.UnlockSoloAiAsync(null));
        await wordRings.RecordPlacementAsync(null, null, "Guest", "guest-scope", "1", true, "ABC");
        await wordRings.RecordHostedGameAsync(null, null, "Guest host", "guest-room:1");
        Assert.False(await achievements.UnlockPlayerAsync(
            null,
            null,
            "Guest host",
            "WordRingsPlayingHostWin"));
        Assert.Empty(await fixture.Db.PlayerAchievements.ToListAsync());

        await wordRings.RecordPlacementAsync(
            null,
            "registered-host",
            "Host guest",
            "host-scope",
            "1",
            true,
            "ABC");
        Assert.True(await fixture.Db.PlayerAchievements.AnyAsync(item =>
            item.AccountId == null &&
            item.HostId == "registered-host" &&
            item.AchievementCode == "WordRingsTripleCorrect"));
        Assert.True(await achievements.UnlockPlayerAsync(
            null,
            "registered-host",
            "Host guest",
            "WordRingsPlayingHostWin"));

        await wordRings.RecordPlacementAsync(
            "registered-player",
            null,
            "Registered player",
            "player-scope",
            "1",
            true,
            "ABC");
        Assert.True(await fixture.Db.PlayerAchievements.AnyAsync(item =>
            item.AccountId == "registered-player" &&
            item.AchievementCode == "WordRingsTripleCorrect"));
        Assert.True(await achievements.UnlockPlayerAsync(
            "registered-player-2",
            null,
            "Registered player 2",
            "WordRingsPlayingHostWin"));
    }

    [Fact]
    public void Achievement_rows_stretch_cards_and_Word_Rings_catalog_art_is_vertically_centered()
    {
        var root = FindRepositoryRoot();
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "css",
            "player-achievements.css"));
        var svg = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "images",
            "minigames",
            "word-rings.svg"));

        Assert.Contains(".player-achievements-grid", css, StringComparison.Ordinal);
        Assert.Contains("align-items: stretch", css, StringComparison.Ordinal);
        Assert.Contains("transform=\"translate(0 -38)\"", svg, StringComparison.Ordinal);
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

    private sealed class AchievementFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        public QuizDbContext Db { get; }

        private AchievementFixture(SqliteConnection connection, QuizDbContext db)
        {
            this.connection = connection;
            Db = db;
        }

        public static async Task<AchievementFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<QuizDbContext>()
                .UseSqlite(connection)
                .Options;
            var db = new QuizDbContext(options);
            await db.Database.EnsureCreatedAsync();
            return new AchievementFixture(connection, db);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
