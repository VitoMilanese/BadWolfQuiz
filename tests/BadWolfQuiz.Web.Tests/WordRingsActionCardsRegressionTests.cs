using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace BadWolfQuiz.Web.Tests;

public sealed class WordRingsActionCardsRegressionTests
{
    [Fact]
    public void Fully_correct_words_award_unique_cards_up_to_the_configured_limit()
    {
        var root = Path.Combine(Path.GetTempPath(), $"badwolf-word-rings-action-cards-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var environment = new TestWebHostEnvironment(root);
            var rooms = WordRingsRoomHostCoordinator.Get(environment);
            var cards = WordRingsActionCardCoordinator.Get(environment);
            var host = rooms.CreateRoom("Host", 15, partialScoreEnabled: false, hostChoosesRules: false);
            _ = rooms.JoinRoom(host.RoomCode, "Guest");
            cards.RegisterRoom(host, enabled: true, correctWordsPerCard: 1, maximumCards: 2);
            _ = rooms.StartGame(host.RoomCode, host.PlayerToken);
            cards.BeginRound(host.RoomCode, host.PlayerToken);

            cards.RecordPlacementAttempt(host.RoomCode, host.State.PlayerId, "one", fullyCorrect: true);
            var first = cards.GetState(host.RoomCode, host.PlayerToken);
            Assert.True(first.Active);
            Assert.Single(first.Cards);

            cards.RecordPlacementAttempt(host.RoomCode, host.State.PlayerId, "two", fullyCorrect: true);
            cards.RecordPlacementAttempt(host.RoomCode, host.State.PlayerId, "three", fullyCorrect: true);
            var capped = cards.GetState(host.RoomCode, host.PlayerToken);

            Assert.Equal(2, capped.Cards.Count);
            Assert.Equal(2, capped.Cards.Select(card => card.Id).Distinct().Count());
            Assert.Equal(0, capped.CorrectProgress);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Non_full_placement_does_not_advance_kps_progress()
    {
        var root = Path.Combine(Path.GetTempPath(), $"badwolf-word-rings-action-kps-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var environment = new TestWebHostEnvironment(root);
            var rooms = WordRingsRoomHostCoordinator.Get(environment);
            var cards = WordRingsActionCardCoordinator.Get(environment);
            var host = rooms.CreateRoom("Host", 15, partialScoreEnabled: true, hostChoosesRules: false);
            _ = rooms.JoinRoom(host.RoomCode, "Guest");
            cards.RegisterRoom(host, enabled: true, correctWordsPerCard: 2, maximumCards: 3);
            _ = rooms.StartGame(host.RoomCode, host.PlayerToken);
            cards.BeginRound(host.RoomCode, host.PlayerToken);

            cards.RecordPlacementAttempt(host.RoomCode, host.State.PlayerId, "partial", fullyCorrect: false);
            var state = cards.GetState(host.RoomCode, host.PlayerToken);

            Assert.Empty(state.Cards);
            Assert.Equal(0, state.CorrectProgress);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Room_tuning_allows_the_host_to_raise_target_score_to_twenty()
    {
        var root = Path.Combine(Path.GetTempPath(), $"badwolf-word-rings-target-score-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var environment = new TestWebHostEnvironment(root);
            var rooms = WordRingsRoomHostCoordinator.Get(environment);
            var host = rooms.CreateRoom("Host", 15, partialScoreEnabled: false, hostChoosesRules: false);
            var page = new BadWolfQuiz.Web.Pages.WordRingsRoomTuningApiModel(environment);

            _ = page.OnPostSetTargetScore(host.RoomCode, host.PlayerToken, 20);
            var state = rooms.GetRoomState(host.RoomCode, host.PlayerToken);

            Assert.Equal(20, state.TargetScore);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Action_card_ui_covers_new_defaults_settings_carousel_and_discard_flow()
    {
        var script = ReadWebFile("wwwroot", "js", "word-rings-action-cards-v2.js");
        var css = ReadWebFile("wwwroot", "css", "word-rings-action-cards-v2.css");
        var pointerDrag = ReadWebFile("wwwroot", "js", "word-rings-pointer-drag.js");
        var tagHelper = ReadWebFile("TagHelpers", "WordRingsActionCardsAssetsTagHelper.cs");
        var tuningApi = ReadWebFile("Pages", "WordRingsRoomTuningApi.cshtml.cs");
        var coopScript = ReadWebFile("wwwroot", "js", "word-rings-coop.js");
        var soloScript = ReadWebFile("wwwroot", "js", "word-rings.js");
        var page = ReadWebFile("Pages", "WordRings.cshtml");

        Assert.Contains("const fallback = { enabled: false, kps: 2, max: 2 };", script, StringComparison.Ordinal);
        Assert.Contains("kps.value = '2';", script, StringComparison.Ordinal);
        Assert.Contains("option.selected = count === 2;", script, StringComparison.Ordinal);
        Assert.Contains("targetScore.max = '20';", script, StringComparison.Ordinal);
        Assert.Contains("kps: 'Правильних слів на картку'", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Правильних слів на картку (КПС)", script, StringComparison.Ordinal);
        Assert.Contains("word-rings-action-settings-button", script, StringComparison.Ordinal);
        Assert.Contains("word-rings-action-carousel-nav", script, StringComparison.Ordinal);
        Assert.Contains("cardId: -id", script, StringComparison.Ordinal);
        Assert.Contains("randomPlayer: 'Випадковий гравець'", script, StringComparison.Ordinal);
        Assert.Contains("const passiveCards = new Set([10]);", script, StringComparison.Ordinal);
        Assert.Contains("const outOfTurn = new Set([8, 13]);", script, StringComparison.Ordinal);
        Assert.Contains("closest('.word-rings-word.is-action-blocked')", script, StringComparison.Ordinal);
        Assert.Contains("if (token.textContent !== display) token.textContent = display;", script, StringComparison.Ordinal);
        Assert.Contains("word-rings-action-timeout-notice", script, StringComparison.Ordinal);
        Assert.Contains("syncMultiplayerVisibleWords", script, StringComparison.Ordinal);
        Assert.Contains("lastRenderedCardSignature", script, StringComparison.Ordinal);
        Assert.Contains("if (!token.disabled) token.disabled = true;", script, StringComparison.Ordinal);
        Assert.Contains("maskedWords.has(normalized)", script, StringComparison.Ordinal);
        Assert.Contains("anagrammedWords.has(normalized)", script, StringComparison.Ordinal);
        Assert.Contains("}, 2150);", script, StringComparison.Ordinal);
        Assert.Contains("new MutationObserver", pointerDrag, StringComparison.Ordinal);
        Assert.Contains("soundEffectsEnabled", coopScript, StringComparison.Ordinal);
        Assert.Contains("data-room-sound-enabled", page, StringComparison.Ordinal);
        Assert.Contains("state?.seedSetupPending === true", coopScript, StringComparison.Ordinal);
        Assert.Contains("source.dataset.seedExample === 'true'", soloScript, StringComparison.Ordinal);

        Assert.Contains("border: 0;", css, StringComparison.Ordinal);
        Assert.Contains("overflow: visible;", css, StringComparison.Ordinal);
        Assert.Contains("word-rings-action-carousel-nav", css, StringComparison.Ordinal);
        Assert.Contains("transform: translateY(-5px) scale(1.09)", css, StringComparison.Ordinal);
        Assert.Contains("word-rings-action-pending-pulse", css, StringComparison.Ordinal);
        Assert.Contains("word-rings-action-timeout-pop 2.05s", css, StringComparison.Ordinal);
        Assert.Contains("word-rings-sound-toggle", css, StringComparison.Ordinal);
        Assert.Contains("word-rings-room-dialog[data-create-room-dialog]", css, StringComparison.Ordinal);
        Assert.Contains("max-width: 760px;", css, StringComparison.Ordinal);

        Assert.Contains("word-rings-action-cards-v2.css?v=3", tagHelper, StringComparison.Ordinal);
        Assert.Contains("word-rings-action-cards-v2.js?v=3", tagHelper, StringComparison.Ordinal);
        Assert.Contains("targetScore is < 5 or > 20", tuningApi, StringComparison.Ordinal);
        Assert.Contains("if (!state.IsHost)", tuningApi, StringComparison.Ordinal);
        Assert.Contains("k__BackingField", tuningApi, StringComparison.Ordinal);
    }

    [Fact]
    public void Corrected_action_card_semantics_are_guarded_in_the_server_runtime()
    {
        var service = ReadWebFile("Services", "WordRingsActionCardCoordinator.cs");

        Assert.Contains("case WordRingsActionCardKind.Immunity:", service, StringComparison.Ordinal);
        Assert.Contains("ActivatedFailureImmunity = true", service, StringComparison.Ordinal);
        Assert.Contains("if (kind == WordRingsActionCardKind.Shield)", service, StringComparison.Ordinal);
        Assert.Contains("target.Cards.Contains(WordRingsActionCardKind.Shield)", service, StringComparison.Ordinal);
        Assert.Contains("WordRingsActionCardKind.Replace,", service, StringComparison.Ordinal);
        Assert.Contains("WordRingsActionCardKind.Shuffle,", service, StringComparison.Ordinal);
        Assert.Contains("MaskedWords", service, StringComparison.Ordinal);
        Assert.Contains("AnagrammedWords", service, StringComparison.Ordinal);
        Assert.Contains("SwapWordEffect", service, StringComparison.Ordinal);
        Assert.DoesNotContain("target.Shield = false", service, StringComparison.Ordinal);
        Assert.Contains("var discard = cardId < 0;", service, StringComparison.Ordinal);
        Assert.Contains("actor.Cards.Remove(kind)", service, StringComparison.Ordinal);
        Assert.Contains("if (requestedTargetId is null) return others[Random.Shared.Next(others.Length)];", service, StringComparison.Ordinal);
        Assert.Contains("if (!others.Contains(requestedTargetId.Value))", service, StringComparison.Ordinal);
        Assert.Contains("TemporaryWordLifetime.ExpireAtTurnEnd", service, StringComparison.Ordinal);
        Assert.DoesNotContain("ExpireOnNextTurnStart", service, StringComparison.Ordinal);
        Assert.Contains("regular.Concat(liveTemporary)", service, StringComparison.Ordinal);
        Assert.Contains("TimeoutNoticeRevision", service, StringComparison.Ordinal);
        Assert.Contains("TimeoutNoticePlayerName", service, StringComparison.Ordinal);
        Assert.Contains("actorMeta.BlockedWords.Remove(actorWord);", service, StringComparison.Ordinal);
        Assert.Contains("return false;", service, StringComparison.Ordinal);
    }

    private static string ReadWebFile(params string[] pathParts) =>
        File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "BadWolfQuiz.Web",
            Path.Combine(pathParts)));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src", "BadWolfQuiz.Web")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate repository root.");
    }

    private sealed class TestWebHostEnvironment(string root) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "BadWolfQuiz.Web.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = root;
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(root);
    }
}
