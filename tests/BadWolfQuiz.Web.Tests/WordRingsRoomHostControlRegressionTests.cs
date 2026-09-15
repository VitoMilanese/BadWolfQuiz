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

            var waitingRules = coordinator.GetRoomState(host.RoomCode, host.PlayerToken);
            Assert.Equal(selectedTexts["A"], waitingRules.BlueRuleText);
            Assert.Equal(selectedTexts["B"], waitingRules.YellowRuleText);
            Assert.Equal(selectedTexts["C"], waitingRules.RedRuleText);

            var first = coordinator.JoinRoom(host.RoomCode, "First");
            var second = coordinator.JoinRoom(host.RoomCode, "Second");
            Assert.Equal(string.Empty, first.State.BlueRuleText);
            Assert.Equal(string.Empty, first.State.YellowRuleText);
            Assert.Equal(string.Empty, first.State.RedRuleText);
            Assert.True(coordinator.SetJoinLocked(host.RoomCode, host.PlayerToken, true).JoinLocked);
            Assert.Throws<WordRingsRoomException>(() => coordinator.JoinRoom(host.RoomCode, "Blocked"));
            _ = coordinator.SetJoinLocked(host.RoomCode, host.PlayerToken, false);

            var started = coordinator.StartGame(host.RoomCode, host.PlayerToken);
            Assert.Equal("playing", started.Phase);
            Assert.Null(started.CurrentPlayerId);
            Assert.Equal(4, started.BankWords.Count + started.QueuedWords.Count);
            Assert.True(started.SeedSetupPending);
            started = CompleteHostSeedSetup(coordinator, host);
            Assert.Equal(first.State.PlayerId, started.CurrentPlayerId);
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
            Assert.Null(started.CurrentPlayerId);
            Assert.Equal(4, started.BankWords.Count + started.QueuedWords.Count);
            started = CompleteHostSeedSetup(coordinator, host);
            Assert.Equal(player.State.PlayerId, started.CurrentPlayerId);
            Assert.Empty(started.BankWords);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Dedicated_host_manually_judges_pending_words_and_players_never_receive_rules()
    {
        var root = Path.Combine(Path.GetTempPath(), $"badwolf-word-rings-host-judge-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var coordinator = WordRingsRoomHostCoordinator.Get(new TestEnvironment(root));
            var host = coordinator.CreateRoom("Host", 15, false, true);
            var setup = coordinator.GetHostState(host.RoomCode, host.PlayerToken);
            foreach (var ring in new[] { "A", "B", "C" })
            {
                var group = Assert.Single(setup.RuleSelections, item => item.Ring == ring);
                setup = coordinator.SelectRule(host.RoomCode, host.PlayerToken, ring, group.Options.First().Id);
            }

            var first = coordinator.JoinRoom(host.RoomCode, "First");
            var second = coordinator.JoinRoom(host.RoomCode, "Second");
            var hostRoom = coordinator.StartGame(host.RoomCode, host.PlayerToken);
            Assert.True(hostRoom.DedicatedHostMode);
            Assert.False(string.IsNullOrWhiteSpace(hostRoom.BlueRuleText));
            hostRoom = CompleteHostSeedSetup(coordinator, host);

            var firstState = coordinator.GetRoomState(host.RoomCode, first.PlayerToken);
            Assert.True(firstState.DedicatedHostMode);
            Assert.Equal(string.Empty, firstState.BlueRuleText);
            Assert.Equal(string.Empty, firstState.YellowRuleText);
            Assert.Equal(string.Empty, firstState.RedRuleText);

            var firstWord = Assert.Single(firstState.BankWords.Take(1));
            var submitted = coordinator.SubmitPlacement(
                host.RoomCode, first.PlayerToken, firstWord, "A", 27, 28);
            Assert.True(submitted.IsPending);
            Assert.Equal(0, submitted.PointsAwarded);
            var pending = Assert.Single(submitted.State.Placements, item => item.IsPending);
            Assert.Equal("A", pending.Membership);
            Assert.Equal("A", pending.SubmittedMembership);
            Assert.Equal(0, Assert.Single(submitted.State.Players, item => item.Id == first.State.PlayerId).Score);
            Assert.Throws<WordRingsRoomException>(() => coordinator.SubmitPlacement(
                host.RoomCode,
                first.PlayerToken,
                submitted.State.BankWords.First(),
                "B",
                73,
                28));

            var hostPending = coordinator.GetHostState(host.RoomCode, host.PlayerToken).PendingPlacement;
            Assert.NotNull(hostPending);
            Assert.False(hostPending.WasMoved);
            Assert.Throws<WordRingsRoomException>(() => coordinator.MovePlacement(
                host.RoomCode, first.PlayerToken, pending.Id, "AB", 50, 19));

            var moved = coordinator.MovePlacement(host.RoomCode, host.PlayerToken, pending.Id, "AB", 50, 19);
            Assert.Equal("AB", Assert.Single(moved.Placements, item => item.IsPending).Membership);
            Assert.True(coordinator.GetHostState(host.RoomCode, host.PlayerToken).PendingPlacement!.WasMoved);
            _ = coordinator.ResolvePlacement(host.RoomCode, host.PlayerToken, pending.Id);

            firstState = coordinator.GetRoomState(host.RoomCode, first.PlayerToken);
            Assert.Equal(0.5, Assert.Single(firstState.Players, item => item.Id == first.State.PlayerId).Score);
            Assert.Equal(second.State.PlayerId, firstState.CurrentPlayerId);
            var partial = Assert.Single(firstState.Placements, item => item.Id == pending.Id);
            Assert.False(partial.IsPending);
            Assert.True(partial.IsPartial);
            Assert.False(partial.IsCorrect);

            var secondState = coordinator.GetRoomState(host.RoomCode, second.PlayerToken);
            var secondWord = Assert.Single(secondState.BankWords.Take(1));
            var wrong = coordinator.SubmitPlacement(host.RoomCode, second.PlayerToken, secondWord, "A", 27, 28);
            var wrongPending = Assert.Single(wrong.State.Placements, item => item.IsPending);
            _ = coordinator.MovePlacement(host.RoomCode, host.PlayerToken, wrongPending.Id, "C", 50, 76);
            _ = coordinator.ResolvePlacement(host.RoomCode, host.PlayerToken, wrongPending.Id);
            secondState = coordinator.GetRoomState(host.RoomCode, second.PlayerToken);
            Assert.Equal(0, Assert.Single(secondState.Players, item => item.Id == second.State.PlayerId).Score);
            Assert.Equal(first.State.PlayerId, secondState.CurrentPlayerId);

            firstState = coordinator.GetRoomState(host.RoomCode, first.PlayerToken);
            var outsideOne = coordinator.SubmitPlacement(
                host.RoomCode, first.PlayerToken, firstState.BankWords.First(), string.Empty, 8, 88);
            var outsideOnePending = Assert.Single(outsideOne.State.Placements, item => item.IsPending);
            _ = coordinator.ResolvePlacement(host.RoomCode, host.PlayerToken, outsideOnePending.Id);
            firstState = coordinator.GetRoomState(host.RoomCode, first.PlayerToken);
            Assert.Equal(1.5, Assert.Single(firstState.Players, item => item.Id == first.State.PlayerId).Score);
            Assert.Equal(first.State.PlayerId, firstState.CurrentPlayerId);

            var outsideTwo = coordinator.SubmitPlacement(
                host.RoomCode, first.PlayerToken, firstState.BankWords.First(), string.Empty, 8, 88);
            var outsideTwoPending = Assert.Single(outsideTwo.State.Placements, item => item.IsPending);
            _ = coordinator.ResolvePlacement(host.RoomCode, host.PlayerToken, outsideTwoPending.Id);
            firstState = coordinator.GetRoomState(host.RoomCode, first.PlayerToken);
            Assert.Equal(1.5, Assert.Single(firstState.Players, item => item.Id == first.State.PlayerId).Score);
            Assert.Equal(first.State.PlayerId, firstState.CurrentPlayerId);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Dedicated_host_reveals_completed_round_rules_to_players_after_finish()
    {
        var root = Path.Combine(Path.GetTempPath(), $"badwolf-word-rings-host-finished-rules-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var coordinator = WordRingsRoomHostCoordinator.Get(new TestEnvironment(root));
            var host = coordinator.CreateRoom("Host", 5, false, true);
            var setup = coordinator.GetHostState(host.RoomCode, host.PlayerToken);
            var selectedTexts = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var ring in new[] { "A", "B", "C" })
            {
                var group = Assert.Single(setup.RuleSelections, item => item.Ring == ring);
                var option = group.Options.First();
                selectedTexts[ring] = option.Text;
                setup = coordinator.SelectRule(host.RoomCode, host.PlayerToken, ring, option.Id);
            }

            var player = coordinator.JoinRoom(host.RoomCode, "Player");
            _ = coordinator.StartGame(host.RoomCode, host.PlayerToken);
            _ = CompleteHostSeedSetup(coordinator, host);
            var playerState = coordinator.GetRoomState(host.RoomCode, player.PlayerToken);
            Assert.Equal(string.Empty, playerState.BlueRuleText);
            Assert.Equal(string.Empty, playerState.YellowRuleText);
            Assert.Equal(string.Empty, playerState.RedRuleText);

            for (var i = 0; i < 5; i++)
            {
                var word = playerState.BankWords.First();
                var submitted = coordinator.SubmitPlacement(host.RoomCode, player.PlayerToken, word, "A", 27, 28);
                var pending = Assert.Single(submitted.State.Placements, item => item.IsPending);
                _ = coordinator.ResolvePlacement(host.RoomCode, host.PlayerToken, pending.Id);
                playerState = coordinator.GetRoomState(host.RoomCode, player.PlayerToken);
            }

            Assert.Equal("finished", playerState.Phase);
            Assert.Equal(selectedTexts["A"], playerState.BlueRuleText);
            Assert.Equal(selectedTexts["B"], playerState.YellowRuleText);
            Assert.Equal(selectedTexts["C"], playerState.RedRuleText);
        }
        finally { Directory.Delete(root, true); }
    }

    private static WordRingsRoomSnapshot CompleteHostSeedSetup(
        WordRingsRoomHostCoordinator coordinator,
        WordRingsRoomConnection host)
    {
        var state = coordinator.GetRoomState(host.RoomCode, host.PlayerToken);
        foreach (var word in state.BankWords.Concat(state.QueuedWords).ToArray())
        {
            state = coordinator.PlaceSeedWord(host.RoomCode, host.PlayerToken, word, "A", 26.5, 27.25);
        }
        _ = coordinator.ConfirmSeedSetup(host.RoomCode, host.PlayerToken);
        return coordinator.GetRoomState(host.RoomCode, host.PlayerToken);
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
    public void Dedicated_host_correct_verdict_restarts_the_full_turn_timer()
    {
        var root = Path.Combine(Path.GetTempPath(), $"badwolf-word-rings-host-timer-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var coordinator = WordRingsRoomHostCoordinator.Get(new TestEnvironment(root));
            var host = coordinator.CreateRoom("Host", 15, false, true, turnDurationSeconds: 60);
            var setup = coordinator.GetHostState(host.RoomCode, host.PlayerToken);
            foreach (var ring in new[] { "A", "B", "C" })
            {
                var group = Assert.Single(setup.RuleSelections, item => item.Ring == ring);
                setup = coordinator.SelectRule(host.RoomCode, host.PlayerToken, ring, group.Options.First().Id);
            }

            var first = coordinator.JoinRoom(host.RoomCode, "First");
            _ = coordinator.JoinRoom(host.RoomCode, "Second");
            _ = coordinator.StartGame(host.RoomCode, host.PlayerToken);
            _ = CompleteHostSeedSetup(coordinator, host);

            var before = coordinator.GetRoomState(host.RoomCode, first.PlayerToken);
            Assert.Equal(first.State.PlayerId, before.CurrentPlayerId);
            Assert.NotNull(before.TurnDeadlineUtc);
            System.Threading.Thread.Sleep(2500);

            var word = before.BankWords.First();
            var submitted = coordinator.SubmitPlacement(host.RoomCode, first.PlayerToken, word, "A", 27, 28);
            Assert.True(submitted.IsPending);
            Assert.Null(submitted.State.TurnDeadlineUtc);
            var pending = Assert.Single(submitted.State.Placements, item => item.IsPending);

            _ = coordinator.ResolvePlacement(host.RoomCode, host.PlayerToken, pending.Id);
            var after = coordinator.GetRoomState(host.RoomCode, first.PlayerToken);
            Assert.Equal(first.State.PlayerId, after.CurrentPlayerId);
            Assert.NotNull(after.TurnDeadlineUtc);
            Assert.True((after.TurnDeadlineUtc.Value - DateTimeOffset.UtcNow).TotalSeconds > 58.5);
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
        Assert.Contains("data-close-room-rule-picker", page);
        Assert.Contains("data-toggle-room-lock", page);
        Assert.Contains("data-result-rule-a", page);
        Assert.Equal(3, Count(page, "data-reveal-rules"));
        Assert.Contains("data-room-host-judge", page);
        Assert.Contains("data-room-host-resolve", page);
        Assert.True(page.IndexOf("data-word-bank", StringComparison.Ordinal) < page.IndexOf("word-rings-check-button", StringComparison.Ordinal));
        Assert.Contains("hostChoosesRules: true", create);
        foreach (var value in new[] { "RoomHostState", "SelectRoomRule", "RefreshRoomRules", "SetRoomTurn", "KickRoomPlayer", "SetRoomJoinLock", "ResolveRoomPlacement", "hostState.handLimit", "hostState.pendingPlacement" }) Assert.Contains(value, host);
        Assert.Contains("data-result-rule-a", result);
        Assert.Contains("startButton.hidden = hostState.isHost !== true || hostState.phase === 'playing';", host);
        Assert.Contains("const canChooseRules = () => isHostController()", host);
        Assert.Contains("if (!canChooseRules())", host);
        Assert.Contains("chooseRulesButton.classList.toggle('is-hidden', !canOpenRules);", host);
        Assert.Contains("lockButton.hidden = !(hostState.isHost === true && hostState.phase !== 'playing');", host);
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
        Assert.Contains("is-host-judgement-pending", coop);
        Assert.Contains("placement.isPending === true", coop);
        Assert.Contains("state?.dedicatedHostMode === true", coop);
        Assert.Contains("nextState.isHost === true && nextState.phase !== 'finished'", coop);
        Assert.Contains("hostState.phase !== 'finished'", host);
        Assert.Contains("SelectedRuleText", coordinator);
        Assert.Contains("!state.IsHost && !finished", coordinator);
        Assert.Contains("result?.isPending === true", coop);
        Assert.Contains("data-room-awaiting-host", page);
        Assert.Contains("CorrectPlacement = \"Правильно\"", hostText);
        Assert.Contains("MovePlacement = \"Перемістити\"", hostText);
        Assert.Contains("AwaitingHostDecision = \"Очікування рішення хоста.\"", hostText);
        Assert.Contains("partialRow.hidden = dedicatedHost", create);
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
