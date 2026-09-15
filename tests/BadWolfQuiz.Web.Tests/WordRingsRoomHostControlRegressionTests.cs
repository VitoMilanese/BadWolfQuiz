using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace BadWolfQuiz.Web.Tests;

public sealed class WordRingsRoomHostControlRegressionTests
{
    [Fact]
    public void Dedicated_host_selects_rules_and_controls_room()
    {
        var root = Path.Combine(Path.GetTempPath(), $"badwolf-word-rings-host-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var env = new TestEnvironment(root);
            var coordinator = WordRingsRoomHostCoordinator.Get(env);
            var store = WordRingsRoomStore.Get(env);
            var host = coordinator.CreateRoom("Host", 5, false, true);
            var state = coordinator.GetHostState(host.RoomCode, host.PlayerToken);
            Assert.True(state.HostChoosesRules);
            Assert.Equal(5, state.HandLimit);
            Assert.Equal(3, state.RuleSelections.Count);
            Assert.False(Assert.Single(state.Players, p => p.Id == state.PlayerId).IsPlayingParticipant);
            Assert.All(state.RuleSelections, group => Assert.InRange(group.Options.Count, 1, 6));

            state = coordinator.RefreshRules(host.RoomCode, host.PlayerToken, "A");
            Assert.False(Assert.Single(state.RuleSelections, x => x.Ring == "A").CanRefresh);
            Assert.Throws<InvalidOperationException>(() => coordinator.RefreshRules(host.RoomCode, host.PlayerToken, "A"));

            var selectedTexts = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var ring in new[] { "A", "B", "C" })
            {
                var group = Assert.Single(state.RuleSelections, x => x.Ring == ring);
                var option = group.Options.First();
                selectedTexts[ring] = option.Text;
                state = coordinator.SelectRule(host.RoomCode, host.PlayerToken, ring, option.Id);
            }

            var first = coordinator.JoinRoom(host.RoomCode, "First");
            var second = coordinator.JoinRoom(host.RoomCode, "Second");
            Assert.True(coordinator.SetJoinLocked(host.RoomCode, host.PlayerToken, true).JoinLocked);
            Assert.Throws<WordRingsRoomException>(() => coordinator.JoinRoom(host.RoomCode, "Blocked"));
            _ = coordinator.SetJoinLocked(host.RoomCode, host.PlayerToken, false);

            var started = coordinator.StartGame(host.RoomCode, host.PlayerToken);
            Assert.Equal("playing", started.Phase);
            Assert.NotEqual(host.State.PlayerId, started.CurrentPlayerId);
            Assert.Empty(started.BankWords);
            Assert.Equal(selectedTexts["A"], started.BlueRuleText);
            Assert.Equal(selectedTexts["B"], started.YellowRuleText);
            Assert.Equal(selectedTexts["C"], started.RedRuleText);

            var supplemental = coordinator.GetHostState(host.RoomCode, host.PlayerToken);
            Assert.Equal(first.State.PlayerId, supplemental.CurrentPlayerId);
            Assert.False(Assert.Single(supplemental.Players, p => p.Id == host.State.PlayerId).IsPlayingParticipant);
            Assert.InRange(store.GetState(host.RoomCode, first.PlayerToken).BankWords.Count, 1, WordRingsRoomStore.MaximumBankWords);

            Assert.Equal(second.State.PlayerId, coordinator.SetCurrentPlayer(host.RoomCode, host.PlayerToken, second.State.PlayerId).CurrentPlayerId);
            Assert.DoesNotContain(coordinator.KickPlayer(host.RoomCode, host.PlayerToken, first.State.PlayerId).Players, p => p.Id == first.State.PlayerId);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Dedicated_host_can_start_with_one_playing_participant_after_rules_are_selected()
    {
        var root = Path.Combine(Path.GetTempPath(), $"badwolf-word-rings-one-player-host-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var coordinator = WordRingsRoomHostCoordinator.Get(new TestEnvironment(root));
            var host = coordinator.CreateRoom("Host", 5, false, true);
            var state = coordinator.GetHostState(host.RoomCode, host.PlayerToken);
            foreach (var ring in new[] { "A", "B", "C" })
            {
                var group = Assert.Single(state.RuleSelections, item => item.Ring == ring);
                state = coordinator.SelectRule(host.RoomCode, host.PlayerToken, ring, group.Options.First().Id);
            }

            var player = coordinator.JoinRoom(host.RoomCode, "Player");
            state = coordinator.GetHostState(host.RoomCode, host.PlayerToken);
            Assert.True(state.CanStart);
            Assert.Single(state.Players, item => item.IsPlayingParticipant);

            var started = coordinator.StartGame(host.RoomCode, host.PlayerToken);
            Assert.Equal("playing", started.Phase);
            Assert.Equal(player.State.PlayerId, started.CurrentPlayerId);
            Assert.Empty(started.BankWords);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Creating_replacement_room_removes_previous_owned_room_immediately()
    {
        var root = Path.Combine(Path.GetTempPath(), $"badwolf-word-rings-room-replace-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var env = new TestEnvironment(root);
            var coordinator = WordRingsRoomHostCoordinator.Get(env);
            var store = WordRingsRoomStore.Get(env);
            var first = coordinator.CreateRoom("Host", 5, false, false);
            var replacement = coordinator.CreateRoom(
                "Host",
                5,
                false,
                false,
                first.RoomCode,
                first.PlayerToken);

            var exception = Assert.Throws<WordRingsRoomException>(() =>
                store.GetState(first.RoomCode, first.PlayerToken));
            Assert.Equal(WordRingsRoomError.RoomNotFound, exception.Error);
            Assert.NotEqual(first.RoomCode, replacement.RoomCode);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Playing_creator_uses_target_as_visible_hand_limit_below_ten()
    {
        var root = Path.Combine(Path.GetTempPath(), $"badwolf-word-rings-hand-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var coordinator = WordRingsRoomHostCoordinator.Get(new TestEnvironment(root));
            var host = coordinator.CreateRoom("Host", 7, false, false);
            _ = coordinator.JoinRoom(host.RoomCode, "Guest");
            var waiting = coordinator.GetHostState(host.RoomCode, host.PlayerToken);
            Assert.False(waiting.HostChoosesRules);
            Assert.Empty(waiting.RuleSelections);
            Assert.Equal(7, waiting.HandLimit);
            Assert.True(Assert.Single(waiting.Players, p => p.Id == waiting.PlayerId).IsPlayingParticipant);
            Assert.Equal(host.State.PlayerId, coordinator.StartGame(host.RoomCode, host.PlayerToken).CurrentPlayerId);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Ui_wires_check_result_rules_and_host_controls()
    {
        var page = Read("Pages", "WordRings.cshtml");
        var create = Read("wwwroot", "js", "word-rings-room-create-options.js");
        var host = Read("wwwroot", "js", "word-rings-room-host-controls.js");
        var result = Read("wwwroot", "js", "word-rings-result-rules.js");
        var coop = Read("wwwroot", "js", "word-rings-coop.js");
        var styles = Read("wwwroot", "css", "word-rings-host-controls.css");
        var roomStyles = Read("wwwroot", "css", "word-rings-room.css");
        var entry = Read("wwwroot", "js", "word-rings-room-entry.js");
        var solo = Read("wwwroot", "js", "word-rings.js");
        var endDialog = Read("wwwroot", "js", "word-rings-end-dialog.js");
        var pageModel = Read("Pages", "WordRings.cshtml.cs");
        var roomText = Read("Localization", "WordRingsRoomText.cs");
        var hostText = Read("Localization", "WordRingsRoomHostText.cs");
        var coordinator = Read("Services", "WordRingsRoomHostCoordinator.cs");
        Assert.Contains("data-create-room-role", page);
        Assert.Contains("data-room-entry-tab=\"create\"", page);
        Assert.Contains("data-room-entry-tab=\"join\"", page);
        Assert.Contains("data-room-entry-panel=\"join\" hidden", page);
        Assert.Contains("data-room-rule-picker-dialog", page);
        Assert.Contains("data-confirm-room-rule-picker", page);
        Assert.Contains("@hostText.ConfirmRules", page);
        Assert.DoesNotContain("data-close-room-rule-picker", page);
        Assert.Contains("data-toggle-room-lock", page);
        Assert.Contains("data-result-rule-a", page);
        Assert.Equal(2, Count(page, "data-reveal-rules"));
        Assert.True(page.IndexOf("data-word-bank", StringComparison.Ordinal) < page.IndexOf("word-rings-check-button", StringComparison.Ordinal));
        Assert.Contains("hostChoosesRules: true", create);
        foreach (var value in new[] { "RoomHostState", "SelectRoomRule", "RefreshRoomRules", "SetRoomTurn", "KickRoomPlayer", "SetRoomJoinLock", "hostState.handLimit" }) Assert.Contains(value, host);
        Assert.Contains("data-result-rule-a", result);
        Assert.Contains("startButton.hidden = hostState.isHost !== true || hostState.phase === 'playing';", host);
        Assert.Contains("const canChooseRules = () => isHostController()", host);
        Assert.Contains("if (!canChooseRules())", host);
        Assert.Contains("chooseRulesButton.classList.toggle('is-hidden', !canOpenRules);", host);
        Assert.Contains("lockButton.hidden = !(hostState.isHost === true && hostState.phase === 'waiting');", host);
        Assert.Contains("lockButton.textContent = locked ? '🔒' : '🔓';", host);
        Assert.Contains("const needsRebuild = existingButtons.length !== ruleOptions.length", host);
        Assert.Contains("{ renderBusy: false }", host);
        Assert.DoesNotContain("startButton.hidden = state?.isHost", coop);
        Assert.Contains("flex-direction: column;", styles);
        Assert.Contains("background: #b4232f;", styles);
        Assert.DoesNotContain(".word-rings-room-role-option:has(input:checked)", styles);
        Assert.Contains("grid-template-columns: minmax(0, 1fr);", styles);
        Assert.DoesNotContain("grid-template-columns: repeat(2, minmax(0, 1fr));", styles);
        Assert.Contains("input[type=\"radio\"]:focus", styles);
        Assert.Contains("box-shadow: none;", styles);
        Assert.Contains("font-size: clamp(1rem, .85vw, 1.12rem);", styles);
        Assert.Contains("word-rings-room-entry-tabs", roomStyles);
        Assert.Contains("activateEntryTab", entry);
        Assert.Contains("CreateTab = \"Нова гра\"", roomText);
        Assert.Contains("JoinTab = \"Приєднання\"", roomText);
        Assert.Contains("ConfirmRules = \"Підтвердити\"", hostText);
        Assert.DoesNotContain("дев’ятку", hostText);
        Assert.Contains("private const int RuleOptionCount = 6;", coordinator);
        Assert.Contains("Math.Clamp(state.TargetScore, 5, 10)", coordinator);
        Assert.Contains("IsPlayingParticipant) >= 1", coordinator);
        Assert.Contains("!player.IsHost) < 1", coordinator);
        Assert.True(
            coop.IndexOf("main.append(badge);", StringComparison.Ordinal) <
            coop.IndexOf("main.append(name);", StringComparison.Ordinal));
        Assert.Contains("font-size: 1.12rem;", roomStyles);
        Assert.Contains("font-size: .95rem;", styles);
        Assert.Contains("requestUrl.searchParams.set('handler', 'NewPuzzle');", solo);
        Assert.Contains("window.history.replaceState", solo);
        Assert.Contains("wordrings:game-reset", solo);
        Assert.DoesNotContain("window.location.assign(refreshUrl.toString())", solo);
        Assert.Contains("wordrings:game-reset", endDialog);
        Assert.Contains("OnGetNewPuzzle", pageModel);
    }

    private static int Count(string value, string needle)
    {
        var count = 0;
        for (var offset = 0; (offset = value.IndexOf(needle, offset, StringComparison.Ordinal)) >= 0; offset += needle.Length) count++;
        return count;
    }

    private static string Read(params string[] parts) => File.ReadAllText(Find(parts));
    private static string Find(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var path = Path.Combine(new[] { directory.FullName, "src", "BadWolfQuiz.Web" }.Concat(parts).ToArray());
            if (File.Exists(path)) return path;
            directory = directory.Parent;
        }
        throw new FileNotFoundException(Path.Combine(parts));
    }

    private sealed class TestEnvironment(string root) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "BadWolfQuiz.Web.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = root;
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
