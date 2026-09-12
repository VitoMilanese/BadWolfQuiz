using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using BadWolfQuiz.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Tests;

public sealed class AchievementResetRegressionTests
{
    [Fact]
    public async Task Reset_account_keeps_historical_metric_locked_until_it_is_earned_again()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<QuizDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new QuizDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var account = CreateHost("reset-account", "reset@example.test");
        db.Hosts.Add(account);
        db.PlayerAchievements.Add(new PlayerAchievement
        {
            AccountId = account.Id,
            AchievementCode = "Registered",
            UnlockedAtUtc = DateTime.UtcNow.AddDays(-1)
        });
        await db.SaveChangesAsync();

        var reset = await new PlayerAchievementResetService(db).ResetAccountAsync(
            account.Id,
            "Registered");

        Assert.NotNull(reset);
        Assert.True(reset!.Reset);
        Assert.False(await db.PlayerAchievements
            .AsNoTracking()
            .AnyAsync(item => item.AccountId == account.Id && item.AchievementCode == "Registered"));
        Assert.True(await db.PlayerAchievements
            .AsNoTracking()
            .AnyAsync(item =>
                item.AccountId == account.Id &&
                item.AchievementCode == PlayerAchievementResetService.BuildMarkerCode("Registered")));

        var progress = await new PlayerAchievementService(db).LoadForPlayerAsync(
            hostId: null,
            playerName: string.Empty,
            currentGameCode: null,
            accountId: account.Id);
        var registered = Assert.Single(progress, item => item.Code == "Registered");

        Assert.False(registered.IsUnlocked);
        Assert.False(registered.IsNewInCurrentGame);
        Assert.Equal(0, registered.Progress);
        Assert.False(await db.PlayerAchievements
            .AsNoTracking()
            .AnyAsync(item => item.AccountId == account.Id && item.AchievementCode == "Registered"));
    }

    [Fact]
    public async Task Explicit_unlock_can_earn_a_reset_direct_achievement_again()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<QuizDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new QuizDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var account = CreateHost("reset-direct", "direct@example.test");
        db.Hosts.Add(account);
        db.PlayerAchievements.Add(new PlayerAchievement
        {
            AccountId = account.Id,
            AchievementCode = "GitHubVisitor",
            UnlockedAtUtc = DateTime.UtcNow.AddDays(-1)
        });
        await db.SaveChangesAsync();

        var resetService = new PlayerAchievementResetService(db);
        var reset = await resetService.ResetAccountAsync(account.Id, "GitHubVisitor");
        Assert.True(reset?.Reset);

        var unlockedAgain = await new PlayerAchievementService(db).UnlockAccountAsync(
            account.Id,
            "GitHubVisitor");

        Assert.True(unlockedAgain);
        Assert.True(await db.PlayerAchievements
            .AsNoTracking()
            .AnyAsync(item => item.AccountId == account.Id && item.AchievementCode == "GitHubVisitor"));
        Assert.False(await db.PlayerAchievements
            .AsNoTracking()
            .AnyAsync(item =>
                item.AccountId == account.Id &&
                item.AchievementCode == PlayerAchievementResetService.BuildMarkerCode("GitHubVisitor")));
    }

    [Fact]
    public async Task Player_reset_clears_both_fallback_and_account_unlocks()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<QuizDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new QuizDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var host = CreateHost("reset-host", "host@example.test");
        var account = CreateHost("reset-player", "player@example.test");
        db.Hosts.AddRange(host, account);
        var playerKey = PlayerAchievementService.NormalizePlayerKey("Rose");
        db.PlayerAchievements.AddRange(
            new PlayerAchievement
            {
                HostId = host.Id,
                PlayerKey = playerKey,
                AchievementCode = "DoubleReward",
                UnlockedAtUtc = DateTime.UtcNow.AddDays(-1)
            },
            new PlayerAchievement
            {
                AccountId = account.Id,
                AchievementCode = "DoubleReward",
                UnlockedAtUtc = DateTime.UtcNow.AddDays(-1)
            });
        await db.SaveChangesAsync();

        var reset = await new PlayerAchievementResetService(db).ResetPlayerAsync(
            account.Id,
            host.Id,
            "Rose",
            "DoubleReward",
            sourceGameSessionId: null);

        Assert.True(reset?.Reset);
        var rows = await db.PlayerAchievements.AsNoTracking().ToListAsync();
        Assert.DoesNotContain(rows, item => item.AchievementCode == "DoubleReward");
        Assert.Contains(rows, item =>
            item.AccountId == account.Id &&
            item.AchievementCode == PlayerAchievementResetService.BuildMarkerCode("DoubleReward"));
        Assert.Contains(rows, item =>
            item.HostId == host.Id &&
            item.PlayerKey == playerKey &&
            item.AchievementCode == PlayerAchievementResetService.BuildMarkerCode("DoubleReward"));
    }

    [Fact]
    public void Achievement_pages_use_styled_reset_dialog_and_player_card_delete_controls()
    {
        var root = FindRepositoryRoot();
        var selfPage = Normalize(File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Achievements.cshtml")));
        var playerTagHelper = Normalize(File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "TagHelpers",
            "PlayerAchievementsTagHelper.cs")));
        var script = Normalize(File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "achievement-reset.js")));
        var styles = Normalize(File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "css",
            "player-achievements.css")));

        Assert.Contains("data-achievement-code=\"@achievement.Code\"", selfPage, StringComparison.Ordinal);
        Assert.Contains("data-achievement-reset-dialog", selfPage, StringComparison.Ordinal);
        Assert.Contains("asp-page-handler=\"ResetAchievement\"", selfPage, StringComparison.Ordinal);
        Assert.Contains("~/js/achievement-reset.js", selfPage, StringComparison.Ordinal);
        Assert.DoesNotContain("window.confirm", selfPage, StringComparison.Ordinal);

        Assert.Contains("data-achievement-code=\\\"", playerTagHelper, StringComparison.Ordinal);
        Assert.Contains("/Player/ResetAchievement", playerTagHelper, StringComparison.Ordinal);
        Assert.Contains("achievement-reset.js?v=1", playerTagHelper, StringComparison.Ordinal);
        Assert.Contains("IAntiforgery antiforgery", playerTagHelper, StringComparison.Ordinal);

        Assert.Contains("data-achievement-reset-button", script, StringComparison.Ordinal);
        Assert.Contains("dialog.showModal()", script, StringComparison.Ordinal);
        Assert.Contains("await fetch(form.action", script, StringComparison.Ordinal);
        Assert.DoesNotContain("window.confirm", script, StringComparison.Ordinal);

        Assert.Contains(".player-achievement-reset-button", styles, StringComparison.Ordinal);
        Assert.Contains(".self-achievements-page .player-achievement-card.is-unlocked:hover .player-achievement-reset-button", styles, StringComparison.Ordinal);
    }

    private static HostAccount CreateHost(string id, string email) => new()
    {
        Id = id,
        Email = email,
        NormalizedEmail = email.ToUpperInvariant(),
        PasswordHash = "test"
    };

    private static string Normalize(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal);

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
