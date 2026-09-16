using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace BadWolfQuiz.Web.Tests;

public sealed class WordRingsActionAchievementAndThemeRegressionTests
{
    private static readonly string[] AchievementCodes =
    [
        "WordRingsCleanseImmediate",
        "WordRingsShieldSave",
        "WordRingsMaskedCorrect",
        "WordRingsAnagramCorrect",
        "WordRingsHintTripleCorrect",
        "WordRingsStealMaskedWord",
        "WordRingsStealAnagramWord",
        "WordRingsStealBlockedWord",
        "WordRingsFirstEmptyActionHand"
    ];

    [Fact]
    public void Catalog_contains_the_nine_action_card_achievements()
    {
        foreach (var code in AchievementCodes)
        {
            var definition = Assert.Single(PlayerAchievementService.Catalog, item => item.Code == code);
            Assert.Equal(PlayerAchievementMetric.DirectUnlock, definition.Metric);
            Assert.Equal(1, definition.Target);
            Assert.False(definition.IsSecret);
        }
    }

    [Fact]
    public void Action_tracker_and_client_hooks_cover_requested_conditions()
    {
        var tracker = ReadWebFile("Services", "WordRingsActionAchievementTracker.cs");
        var api = ReadWebFile("Pages", "WordRingsActionCardsPatchApi.cshtml.cs");
        var script = ReadWebFile("wwwroot", "js", "word-rings-action-achievements.js");
        var tagHelper = ReadWebFile("TagHelpers", "WordRingsActionCardsAssetsTagHelper.cs");

        foreach (var code in AchievementCodes)
        {
            Assert.Contains(code, tracker, StringComparison.Ordinal);
        }
        Assert.Contains("result.BlockedByShield", tracker, StringComparison.Ordinal);
        Assert.Contains("LastNegativeByActor", tracker, StringComparison.Ordinal);
        Assert.Contains("MaskedWordsByPlayer", tracker, StringComparison.Ordinal);
        Assert.Contains("AnagrammedWordsByPlayer", tracker, StringComparison.Ordinal);
        Assert.Contains("BlockedWordsByPlayer", tracker, StringComparison.Ordinal);
        Assert.Contains("CanonicalMembership(placement.Membership), \"ABC\"", tracker, StringComparison.Ordinal);
        Assert.Contains("FirstEmptyActionHandAwarded", tracker, StringComparison.Ordinal);
        Assert.Contains("CaptureAction", api, StringComparison.Ordinal);
        Assert.Contains("RecordActionAsync", api, StringComparison.Ordinal);
        Assert.Contains("CapturePlacementAchievement", api, StringComparison.Ordinal);
        Assert.Contains("FinalizePlacementAchievement", api, StringComparison.Ordinal);
        Assert.Contains("handler !== 'SubmitRoomWord' && handler !== 'ResolveRoomPlacement'", script, StringComparison.Ordinal);
        Assert.Contains("word-rings-action-achievements.js?v=1", tagHelper, StringComparison.Ordinal);
    }

    [Fact]
    public void Joined_player_receives_the_host_word_rings_theme()
    {
        var root = CreateRoot();
        try
        {
            var environment = new TestWebHostEnvironment(root);
            var rooms = WordRingsRoomHostCoordinator.Get(environment);
            var themes = WordRingsThemeSyncCoordinator.Get(environment);
            var host = rooms.CreateRoom("Host", 5, partialScoreEnabled: false, hostChoosesRules: false);
            var guest = rooms.JoinRoom(host.RoomCode, "Guest");

            var hostTheme = themes.Synchronize(
                host.RoomCode,
                host.PlayerToken,
                "ukrainian-sky",
                "{\"--bg\":\"#001122\",\"--gold\":\"#ffd700\",\"--not-allowed\":\"red\"}");
            var guestTheme = themes.Synchronize(
                host.RoomCode,
                guest.PlayerToken,
                "orange",
                "{\"--bg\":\"#ffffff\"}");

            Assert.NotNull(hostTheme);
            Assert.NotNull(guestTheme);
            Assert.Equal("ukrainian-sky", guestTheme!.ThemeId);
            Assert.Equal("#001122", guestTheme.Variables["--bg"]);
            Assert.Equal("#ffd700", guestTheme.Variables["--gold"]);
            Assert.DoesNotContain("--not-allowed", guestTheme.Variables.Keys);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Theme_sync_is_loaded_for_word_rings_rooms()
    {
        var script = ReadWebFile("wwwroot", "js", "word-rings-theme-sync.js");
        var api = ReadWebFile("Pages", "WordRingsGameplayOptionsApi.cshtml.cs");
        var tagHelper = ReadWebFile("TagHelpers", "WordRingsActionCardsAssetsTagHelper.cs");

        Assert.Contains("SynchronizeTheme", script, StringComparison.Ordinal);
        Assert.Contains("window.setInterval(synchronize, 1000)", script, StringComparison.Ordinal);
        Assert.Contains("OnPostSynchronizeTheme", api, StringComparison.Ordinal);
        Assert.Contains("word-rings-theme-sync.js?v=1", tagHelper, StringComparison.Ordinal);
    }

    private static string ReadWebFile(params string[] pathParts) =>
        File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "BadWolfQuiz.Web", Path.Combine(pathParts)));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "BadWolfQuiz.sln"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("BadWolfQuiz repository root was not found.");
    }

    private static string CreateRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"badwolf-word-rings-theme-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    private sealed class TestWebHostEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "BadWolfQuiz.Web.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = contentRootPath;
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(contentRootPath);
    }
}
