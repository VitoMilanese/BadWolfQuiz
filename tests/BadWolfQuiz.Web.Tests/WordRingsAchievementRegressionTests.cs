using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using BadWolfQuiz.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Tests;

public sealed class WordRingsAchievementRegressionTests
{
    [Fact]
    public void Catalog_contains_the_ten_Word_Rings_achievements_with_expected_targets()
    {
        var expected = new Dictionary<string, int>
        {
            ["WordRingsSoloAi"] = 1,
            ["WordRingsPlayingHostWin"] = 1,
            ["WordRingsReferee"] = 1,
            ["WordRingsTripleCorrect"] = 1,
            ["WordRingsOutsideThree"] = 1,
            ["WordRingsCorrect50"] = 50,
            ["WordRingsBlue50"] = 50,
            ["WordRingsYellow50"] = 50,
            ["WordRingsRed50"] = 50,
            ["WordRingsHost10"] = 10
        };

        foreach (var pair in expected)
        {
            var achievement = Assert.Single(PlayerAchievementService.Catalog, item => item.Code == pair.Key);
            Assert.Equal(pair.Value, achievement.Target);
            Assert.False(achievement.IsSecret);
        }
    }

    [Fact]
    public async Task Triple_ring_and_three_consecutive_outside_correct_placements_unlock_direct_achievements()
    {
        await using var fixture = await AchievementFixture.CreateAsync();
        var service = new WordRingsAchievementService(fixture.Db);
        const string accountId = "word-rings-player";

        await service.RecordPlacementAsync(accountId, null, "Wolf", "scope-one", "1", true, "ABC");
        await service.RecordPlacementAsync(accountId, null, "Wolf", "scope-one", "2", true, "");
        await service.RecordPlacementAsync(accountId, null, "Wolf", "scope-one", "3", true, "");
        await service.RecordPlacementAsync(accountId, null, "Wolf", "scope-one", "4", true, "");

        var codes = await fixture.Db.PlayerAchievements
            .Where(item => item.AccountId == accountId && !item.AchievementCode.StartsWith("__"))
            .Select(item => item.AchievementCode)
            .ToListAsync();
        Assert.Contains("WordRingsTripleCorrect", codes);
        Assert.Contains("WordRingsOutsideThree", codes);
    }

    [Fact]
    public async Task Outside_correct_streak_resets_when_an_intervening_placement_is_not_fully_correct()
    {
        await using var fixture = await AchievementFixture.CreateAsync();
        var service = new WordRingsAchievementService(fixture.Db);
        const string accountId = "outside-streak-player";

        await service.RecordPlacementAsync(accountId, null, "Wolf", "scope-two", "1", true, "");
        await service.RecordPlacementAsync(accountId, null, "Wolf", "scope-two", "2", false, "A");
        await service.RecordPlacementAsync(accountId, null, "Wolf", "scope-two", "3", true, "");
        await service.RecordPlacementAsync(accountId, null, "Wolf", "scope-two", "4", true, "");
        Assert.False(await fixture.Db.PlayerAchievements.AnyAsync(item =>
            item.AccountId == accountId && item.AchievementCode == "WordRingsOutsideThree"));

        await service.RecordPlacementAsync(accountId, null, "Wolf", "scope-two", "5", true, "");
        Assert.True(await fixture.Db.PlayerAchievements.AnyAsync(item =>
            item.AccountId == accountId && item.AchievementCode == "WordRingsOutsideThree"));
    }

    [Fact]
    public async Task Fifty_correct_ring_placements_persist_and_unlock_total_and_per_ring_achievements()
    {
        await using var fixture = await AchievementFixture.CreateAsync();
        var service = new WordRingsAchievementService(fixture.Db);
        const string accountId = "fifty-player";

        for (var index = 0; index < 50; index++)
        {
            await service.RecordPlacementAsync(accountId, null, "Wolf", "scope-blue", $"b{index}", true, "A");
            await service.RecordPlacementAsync(accountId, null, "Wolf", "scope-yellow", $"y{index}", true, "B");
            await service.RecordPlacementAsync(accountId, null, "Wolf", "scope-red", $"r{index}", true, "C");
        }

        var progress = await new PlayerAchievementService(fixture.Db).LoadForPlayerAsync(
            null,
            "Wolf",
            currentGameCode: null,
            accountId: accountId);
        foreach (var code in new[] { "WordRingsCorrect50", "WordRingsBlue50", "WordRingsYellow50", "WordRingsRed50" })
        {
            var achievement = Assert.Single(progress, item => item.Code == code);
            Assert.True(achievement.IsUnlocked);
            Assert.Equal(achievement.Target, achievement.Progress);
        }
    }

    [Fact]
    public async Task Hosted_round_progress_unlocks_first_referee_game_and_tenth_hosted_game()
    {
        await using var fixture = await AchievementFixture.CreateAsync();
        var service = new WordRingsAchievementService(fixture.Db);
        const string accountId = "referee-player";

        for (var round = 1; round <= 10; round++)
        {
            await service.RecordHostedGameAsync(accountId, null, "Host", $"room:ABC123:round:{round}");
        }

        var progress = await new PlayerAchievementService(fixture.Db).LoadForPlayerAsync(
            null,
            "Host",
            currentGameCode: null,
            accountId: accountId);
        Assert.True(Assert.Single(progress, item => item.Code == "WordRingsReferee").IsUnlocked);
        var ten = Assert.Single(progress, item => item.Code == "WordRingsHost10");
        Assert.True(ten.IsUnlocked);
        Assert.Equal(10, ten.Progress);
    }

    [Fact]
    public void Word_Rings_page_and_room_api_wire_solo_multiplayer_and_host_achievement_events()
    {
        var root = FindRepositoryRoot();
        var page = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "Pages", "WordRings.cshtml"));
        var solo = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "wwwroot", "js", "word-rings.js"));
        var api = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "Pages", "WordRingsRoomApi.cshtml.cs"));
        var coordinator = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "Services", "WordRingsRoomHostCoordinator.cs"));

        Assert.Contains("data-word-rings-achievement-session", page, StringComparison.Ordinal);
        Assert.Contains("RecordSoloAchievement", solo, StringComparison.Ordinal);
        Assert.Contains("RecordPlacementAchievementAsync", api, StringComparison.Ordinal);
        Assert.Contains("WordRingsPlayingHostWin", api, StringComparison.Ordinal);
        Assert.Contains("RecordHostedGameAsync", api, StringComparison.Ordinal);
        Assert.Contains("TryClaimCompletedRoundAchievement", coordinator, StringComparison.Ordinal);
        Assert.Contains("PlayerIdentities", coordinator, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_Word_Rings_achievement_has_localized_resources_and_artwork()
    {
        var root = FindRepositoryRoot();
        var codes = PlayerAchievementService.Catalog
            .Where(item => item.Code.StartsWith("WordRings", StringComparison.Ordinal))
            .Select(item => item.Code)
            .ToArray();
        Assert.Equal(10, codes.Length);

        foreach (var suffix in new[] { "AchievementResource.resx", "AchievementResource.uk.resx", "AchievementResource.it.resx", "AchievementResource.ru.resx" })
        {
            var resource = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "Resources", "Localization", suffix));
            foreach (var code in codes)
            {
                Assert.Contains($"name=\"{code}_Name\"", resource, StringComparison.Ordinal);
                Assert.Contains($"name=\"{code}_Description\"", resource, StringComparison.Ordinal);
            }
        }
        foreach (var code in codes)
        {
            Assert.True(File.Exists(Path.Combine(root, "src", "BadWolfQuiz.Web", "wwwroot", "images", "achievements", $"{code}.png")));
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
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
