using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace BadWolfQuiz.Web.Tests;

public sealed class WordRingsRoomUxBrandingRegressionTests
{
    [Fact]
    public void Minigames_catalog_launches_use_shared_busy_navigation()
    {
        var script = ReadWebFile("wwwroot", "js", "busy-indicators.js");
        Assert.Contains("minigames: \"/minigames\"", script, StringComparison.Ordinal);
        Assert.Contains("currentPath === routes.minigames", script, StringComparison.Ordinal);
        Assert.Contains("link.matches(\".minigames-catalog-card\")", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Room_creation_is_single_flight_until_navigation_or_error()
    {
        var normal = ReadWebFile("wwwroot", "js", "word-rings-room-entry.js");
        var hosted = ReadWebFile("wwwroot", "js", "word-rings-room-create-options.js");

        Assert.Contains("let createInFlight = false", normal, StringComparison.Ordinal);
        Assert.Contains("if (createInFlight) return", normal, StringComparison.Ordinal);
        Assert.Contains("navigationStarted = true", normal, StringComparison.Ordinal);
        Assert.Contains("if (!navigationStarted)", normal, StringComparison.Ordinal);
        Assert.Contains("let createInFlight = false", hosted, StringComparison.Ordinal);
        Assert.Contains("if (createInFlight) return", hosted, StringComparison.Ordinal);
        Assert.Contains("if (!navigationStarted)", hosted, StringComparison.Ordinal);
    }

    [Fact]
    public void Empty_room_start_surfaces_error_without_restoring_turn_status_row()
    {
        var page = ReadWebFile("Pages", "WordRings.cshtml");
        var coop = ReadWebFile("wwwroot", "js", "word-rings-coop.js");
        var host = ReadWebFile("wwwroot", "js", "word-rings-room-host-controls.js");

        Assert.Contains("data-room-feedback", page, StringComparison.Ordinal);
        Assert.Contains("@if (!isCooperativeRoom)", page, StringComparison.Ordinal);
        Assert.Contains("<p class=\"word-rings-status\" data-status", page, StringComparison.Ordinal);
        Assert.Contains("showRoomFeedback(", coop, StringComparison.Ordinal);
        Assert.Contains("payload.error === 'NeedMorePlayers'", coop, StringComparison.Ordinal);
        Assert.Contains("startButton.disabled = busy;", host, StringComparison.Ordinal);
        Assert.DoesNotContain("startButton.disabled = busy || hostState.canStart !== true", host, StringComparison.Ordinal);
    }

    [Fact]
    public void Finished_result_can_be_reopened_until_next_round()
    {
        var page = ReadWebFile("Pages", "WordRings.cshtml");
        var script = ReadWebFile("wwwroot", "js", "word-rings-end-dialog.js");

        Assert.Contains("data-reopen-result", page, StringComparison.Ordinal);
        Assert.Contains("let lastResult = null", script, StringComparison.Ordinal);
        Assert.Contains("show(lastResult, { repeat: true })", script, StringComparison.Ordinal);
        Assert.Contains("dialog.addEventListener('close', syncReopenButton)", script, StringComparison.Ordinal);
        Assert.Contains("!root.classList.contains('is-game-over')", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Room_creator_brand_logo_is_copied_and_exposed_to_room_header()
    {
        var root = Path.Combine(Path.GetTempPath(), $"badwolf-word-rings-brand-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var coordinator = WordRingsRoomHostCoordinator.Get(new TestEnvironment(root));
            var source = new byte[] { 1, 2, 3, 4 };
            var room = coordinator.CreateRoom(
                "Host",
                5,
                partialScoreEnabled: false,
                hostChoosesRules: false,
                brandLogoData: source,
                brandLogoContentType: "image/png");

            source[0] = 99;
            var logo = coordinator.GetBrandLogo(room.RoomCode);
            Assert.NotNull(logo);
            Assert.Equal("image/png", logo.ContentType);
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, logo.Data);

            var pageModel = ReadWebFile("Pages", "WordRings.cshtml.cs");
            var api = ReadWebFile("Pages", "WordRingsRoomApi.cshtml.cs");
            var layout = ReadWebFile("Pages", "Shared", "_Layout.cshtml");
            Assert.Contains("HeaderBrandLogoUrl", pageModel, StringComparison.Ordinal);
            Assert.Contains("OnGetRoomBrandLogo", api, StringComparison.Ordinal);
            Assert.Contains("headerBrandLogoUrl", layout, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static string ReadWebFile(params string[] parts) => File.ReadAllText(FindWebFile(parts));

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

        throw new FileNotFoundException($"Could not locate BadWolfQuiz.Web file: {Path.Combine(parts)}");
    }

    private sealed class TestEnvironment(string root) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "BadWolfQuiz.Web.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = root;
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(root);
    }
}
