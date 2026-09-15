using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace BadWolfQuiz.Web.Tests;

public sealed class WordRingsSeedAndPresenceRegressionTests
{
    [Fact]
    public void Automatic_seed_selector_prefers_three_single_rings_outside_and_triple_overlap()
    {
        var puzzle = new WordRingsPuzzle(
            "A", "B", "C",
            ["a", "b", "c", "outside", "triple", "pair"],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["a"] = "A",
                ["b"] = "B",
                ["c"] = "C",
                ["outside"] = "",
                ["triple"] = "ABC",
                ["pair"] = "AB"
            });

        var seeds = WordRingsSeedWordSelector.SelectAutomatic(puzzle);

        Assert.Equal(5, seeds.Count);
        Assert.Contains(seeds, item => item.Membership == "A");
        Assert.Contains(seeds, item => item.Membership == "B");
        Assert.Contains(seeds, item => item.Membership == "C");
        Assert.Contains(seeds, item => item.Membership == "");
        Assert.Contains(seeds, item => item.Membership == "ABC");
    }

    [Fact]
    public void Automatic_seed_selector_falls_back_to_two_ring_overlap()
    {
        var puzzle = new WordRingsPuzzle(
            "A", "B", "C",
            ["a", "b", "c", "outside", "pair"],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["a"] = "A",
                ["b"] = "B",
                ["c"] = "C",
                ["outside"] = "",
                ["pair"] = "BC"
            });

        var seeds = WordRingsSeedWordSelector.SelectAutomatic(puzzle);
        Assert.Equal(5, seeds.Count);
        Assert.Contains(seeds, item => item.Membership == "BC");
    }

    [Fact]
    public void Automatic_multiplayer_round_exposes_non_scoring_seed_examples()
    {
        var root = Path.Combine(Path.GetTempPath(), $"badwolf-word-rings-seeds-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var store = WordRingsRoomStore.Get(new TestWebHostEnvironment(root));
            var host = store.CreateRoom("Host", 5, false);
            _ = store.JoinRoom(host.RoomCode, "Guest");
            var state = store.StartGame(host.RoomCode, host.PlayerToken);

            var seeds = state.Placements.Where(item => item.IsSeed).ToArray();
            Assert.True(seeds.Length >= 4);
            Assert.All(seeds, seed => Assert.Equal(0, seed.PointsAwarded));
            Assert.DoesNotContain(state.BankWords, word => seeds.Any(seed => string.Equals(seed.Word, word, StringComparison.OrdinalIgnoreCase)));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Dedicated_host_must_place_and_confirm_four_examples_before_first_turn()
    {
        var root = Path.Combine(Path.GetTempPath(), $"badwolf-word-rings-host-seeds-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var coordinator = WordRingsRoomHostCoordinator.Get(new TestWebHostEnvironment(root));
            var host = coordinator.CreateRoom("Host", 5, false, hostChoosesRules: true);
            var guest = coordinator.JoinRoom(host.RoomCode, "Guest");
            var setup = coordinator.GetHostState(host.RoomCode, host.PlayerToken);
            foreach (var group in setup.RuleSelections)
            {
                _ = coordinator.SelectRule(host.RoomCode, host.PlayerToken, group.Ring, group.Options[0].Id);
            }

            var started = coordinator.StartGame(host.RoomCode, host.PlayerToken);
            Assert.True(started.SeedSetupPending);
            Assert.Null(started.CurrentPlayerId);
            Assert.Equal(4, started.BankWords.Count + started.QueuedWords.Count);

            foreach (var word in started.BankWords.Concat(started.QueuedWords).ToArray())
            {
                started = coordinator.PlaceSeedWord(host.RoomCode, host.PlayerToken, word, "A", 26.5, 27.25);
            }

            Assert.True(started.SeedSetupPending);
            var confirmedHost = coordinator.ConfirmSeedSetup(host.RoomCode, host.PlayerToken);
            Assert.False(confirmedHost.SeedSetupPending);
            Assert.Equal(guest.State.PlayerId, confirmedHost.CurrentPlayerId);
            var confirmed = coordinator.GetRoomState(host.RoomCode, host.PlayerToken);
            Assert.Equal(4, confirmed.Placements.Count(item => item.IsSeed));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Multiplayer_client_collapses_turn_status_and_reports_departure_on_pagehide()
    {
        var script = ReadWebFile("wwwroot", "js", "word-rings-coop.js");
        var styles = ReadWebFile("wwwroot", "css", "word-rings-refinements.css");
        var page = ReadWebFile("Pages", "WordRings.cshtml");

        Assert.Contains("window.addEventListener('pagehide', sendDepartureBeacon)", script, StringComparison.Ordinal);
        Assert.Contains("navigator.sendBeacon", script, StringComparison.Ordinal);
        Assert.Contains("PrepareLeaveRoom", script, StringComparison.Ordinal);
        var store = ReadWebFile("Services", "WordRingsRoomStore.cs");
        Assert.Contains("PlayerDepartureGracePeriod = TimeSpan.FromSeconds(5)", store, StringComparison.Ordinal);
        Assert.Contains("DepartureRequestedUtc", store, StringComparison.Ordinal);
        Assert.Contains("PlaceRoomSeed", script, StringComparison.Ordinal);
        Assert.Contains("seedSetupPending", script, StringComparison.Ordinal);
        Assert.Contains("@if (!isCooperativeRoom)", page, StringComparison.Ordinal);
        Assert.Contains("data-status", page, StringComparison.Ordinal);
        Assert.Contains("data-room-host-seed-confirm", page, StringComparison.Ordinal);
    }

    private static string ReadWebFile(params string[] segments) =>
        File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "BadWolfQuiz.Web", Path.Combine(segments)));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "BadWolfQuiz.sln"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate repository root.");
    }

    private sealed class TestWebHostEnvironment(string root) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "BadWolfQuiz.Web.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = root;
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(root);
    }
}
