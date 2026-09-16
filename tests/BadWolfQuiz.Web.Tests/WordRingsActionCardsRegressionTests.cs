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
    public void Mask_clears_after_any_check_and_anagram_clears_only_after_a_correct_check()
    {
        var root = Path.Combine(Path.GetTempPath(), $"badwolf-word-rings-obfuscation-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var environment = new TestWebHostEnvironment(root);
            var rooms = WordRingsRoomHostCoordinator.Get(environment);
            var cards = WordRingsActionCardCoordinator.Get(environment);
            var patch = WordRingsActionCardPatchCoordinator.Get(environment);
            var host = rooms.CreateRoom("Host", 15, partialScoreEnabled: false, hostChoosesRules: false);
            _ = rooms.JoinRoom(host.RoomCode, "Guest");
            cards.RegisterRoom(host, enabled: true, correctWordsPerCard: 2, maximumCards: 3);
            _ = rooms.StartGame(host.RoomCode, host.PlayerToken);
            cards.BeginRound(host.RoomCode, host.PlayerToken);

            var method = typeof(WordRingsActionCardCoordinator).GetMethod(
                "ApplyWordObfuscation",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(method);

            method.Invoke(cards, [host.RoomCode, host.State.PlayerId, true]);
            Assert.True(cards.GetState(host.RoomCode, host.PlayerToken).MaskedWords.Count >= 2);

            patch.ApplySubmissionLifecycle(host.RoomCode, host.State.PlayerId, fullyCorrect: false);
            Assert.Empty(cards.GetState(host.RoomCode, host.PlayerToken).MaskedWords);

            method.Invoke(cards, [host.RoomCode, host.State.PlayerId, false]);
            var anagrammed = cards.GetState(host.RoomCode, host.PlayerToken).AnagrammedWords;
            Assert.True(anagrammed.Count >= 2);

            patch.ApplySubmissionLifecycle(host.RoomCode, host.State.PlayerId, fullyCorrect: false);
            Assert.Equal(anagrammed.Count, cards.GetState(host.RoomCode, host.PlayerToken).AnagrammedWords.Count);

            patch.ApplySubmissionLifecycle(host.RoomCode, host.State.PlayerId, fullyCorrect: true);
            Assert.Empty(cards.GetState(host.RoomCode, host.PlayerToken).AnagrammedWords);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Debug_grant_is_server_authoritative_and_rejects_duplicates_and_full_hands()
    {
        var root = Path.Combine(Path.GetTempPath(), $"badwolf-word-rings-debug-cards-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var environment = new TestWebHostEnvironment(root);
            var rooms = WordRingsRoomHostCoordinator.Get(environment);
            var cards = WordRingsActionCardCoordinator.Get(environment);
            var patch = WordRingsActionCardPatchCoordinator.Get(environment);
            var host = rooms.CreateRoom("Host", 15, partialScoreEnabled: false, hostChoosesRules: false);
            var guest = rooms.JoinRoom(host.RoomCode, "Guest");
            cards.RegisterRoom(host, enabled: true, correctWordsPerCard: 2, maximumCards: 2);
            _ = rooms.StartGame(host.RoomCode, host.PlayerToken);
            cards.BeginRound(host.RoomCode, host.PlayerToken);

            patch.GrantDebugCard(host.RoomCode, host.PlayerToken, guest.State.PlayerId, (int)WordRingsActionCardKind.Hint);
            Assert.Contains(cards.GetState(host.RoomCode, guest.PlayerToken).Cards, card => card.Id == (int)WordRingsActionCardKind.Hint);

            var duplicate = Assert.Throws<InvalidOperationException>(() =>
                patch.GrantDebugCard(host.RoomCode, host.PlayerToken, guest.State.PlayerId, (int)WordRingsActionCardKind.Hint));
            Assert.Equal("ActionCardDuplicate", duplicate.Message);

            patch.GrantDebugCard(host.RoomCode, host.PlayerToken, guest.State.PlayerId, (int)WordRingsActionCardKind.Immunity);
            var full = Assert.Throws<InvalidOperationException>(() =>
                patch.GrantDebugCard(host.RoomCode, host.PlayerToken, guest.State.PlayerId, (int)WordRingsActionCardKind.Cleanse));
            Assert.Equal("ActionCardHandFull", full.Message);
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
    public void Latest_action_card_patch_covers_notices_immunity_debug_replenishment_and_seed_flicker()
    {
        var patchScript = ReadWebFile("wwwroot", "js", "word-rings-action-cards-patch.js");
        var patchCss = ReadWebFile("wwwroot", "css", "word-rings-action-cards-patch.css");
        var patchService = ReadWebFile("Services", "WordRingsActionCardPatchCoordinator.cs");
        var patchApi = ReadWebFile("Pages", "WordRingsActionCardsPatchApi.cshtml.cs");
        var tagHelper = ReadWebFile("TagHelpers", "WordRingsActionCardsAssetsTagHelper.cs");

        Assert.Contains("handler === 'UseActionCard'", patchScript, StringComparison.Ordinal);
        Assert.Contains("TryQueueHostedImmunity", patchScript, StringComparison.Ordinal);
        Assert.Contains("ResolveImmunityDecision", patchScript, StringComparison.Ordinal);
        Assert.Contains("GrantDebugActionCard", patchScript, StringComparison.Ordinal);
        Assert.Contains("optimisticSeedWords", patchScript, StringComparison.Ordinal);
        Assert.Contains("}, 3100);", patchScript, StringComparison.Ordinal);
        Assert.Contains("maskDescription", patchScript, StringComparison.Ordinal);
        Assert.Contains("anagramDescription", patchScript, StringComparison.Ordinal);

        Assert.Contains("word-rings-action-effect-pop 3s", patchCss, StringComparison.Ordinal);
        Assert.Contains("is-positive", patchCss, StringComparison.Ordinal);
        Assert.Contains("is-negative", patchCss, StringComparison.Ordinal);
        Assert.Contains("is-neutral", patchCss, StringComparison.Ordinal);
        Assert.DoesNotContain("is-action-temporary-word", patchCss, StringComparison.Ordinal);
        Assert.Contains("align-self: end;", patchCss, StringComparison.Ordinal);
        Assert.Contains(".word-rings-toolbar-progress[hidden]", patchCss, StringComparison.Ordinal);

        Assert.Contains("PendingImmunityDecisions", patchService, StringComparison.Ordinal);
        Assert.Contains("neededPoints", patchService, StringComparison.Ordinal);
        Assert.Contains("scoringRemaining", patchService, StringComparison.Ordinal);
        Assert.Contains("requiredScoringWords = neededPoints + 1", patchService, StringComparison.Ordinal);
        Assert.Contains("MaskedWords", patchService, StringComparison.Ordinal);
        Assert.Contains("AnagrammedWords", patchService, StringComparison.Ordinal);
        Assert.Contains("ActionCardDuplicate", patchService, StringComparison.Ordinal);
        Assert.Contains("ActionCardHandFull", patchService, StringComparison.Ordinal);

        Assert.Contains("configuration.GetValue<bool>(\"DebugMode\")", patchApi, StringComparison.Ordinal);
        Assert.Contains("OnPostTryQueueHostedImmunity", patchApi, StringComparison.Ordinal);
        Assert.Contains("OnPostResolveImmunityDecision", patchApi, StringComparison.Ordinal);
        Assert.Contains("OnPostEnsureTargetWords", patchApi, StringComparison.Ordinal);

        Assert.Contains("word-rings-action-cards-patch.css?v=3", tagHelper, StringComparison.Ordinal);
        Assert.Contains("word-rings-action-cards-v2.css?v=6", tagHelper, StringComparison.Ordinal);
        Assert.Contains("word-rings-action-cards-patch.js?v=2", tagHelper, StringComparison.Ordinal);
        Assert.True(
            tagHelper.IndexOf("word-rings-action-cards-patch.js?v=2", StringComparison.Ordinal) <
            tagHelper.IndexOf("word-rings-action-cards-v2.js?v=5", StringComparison.Ordinal));
    }

    [Fact]
    public void Action_card_ui_covers_defaults_replace_word_selection_carousel_and_discard_flow()
    {
        var script = ReadWebFile("wwwroot", "js", "word-rings-action-cards-v2.js");
        var css = ReadWebFile("wwwroot", "css", "word-rings-action-cards-v2.css");
        var pointerDrag = ReadWebFile("wwwroot", "js", "word-rings-pointer-drag.js");
        var tuningApi = ReadWebFile("Pages", "WordRingsRoomTuningApi.cshtml.cs");
        var coopScript = ReadWebFile("wwwroot", "js", "word-rings-coop.js");
        var soloScript = ReadWebFile("wwwroot", "js", "word-rings.js");
        var page = ReadWebFile("Pages", "WordRings.cshtml");

        Assert.Contains("const fallback = { enabled: false, kps: 2, max: 2 };", script, StringComparison.Ordinal);
        Assert.Contains("kps.value = '2';", script, StringComparison.Ordinal);
        Assert.Contains("option.selected = count === 2;", script, StringComparison.Ordinal);
        Assert.Contains("targetScore.max = '20';", script, StringComparison.Ordinal);
        Assert.Contains("const needsWord = cardId === 1 || (cardId === 2 && selectedTargetIsSelf);", script, StringComparison.Ordinal);
        Assert.Contains("word-rings-action-settings-button", script, StringComparison.Ordinal);
        Assert.Contains("word-rings-action-carousel-nav", script, StringComparison.Ordinal);
        Assert.Contains("cardId: -id", script, StringComparison.Ordinal);
        Assert.Contains("randomPlayer: 'Випадковий гравець'", script, StringComparison.Ordinal);
        Assert.Contains("const passiveCards = new Set([10]);", script, StringComparison.Ordinal);
        Assert.Contains("const outOfTurn = new Set([8, 13]);", script, StringComparison.Ordinal);
        Assert.Contains("const soloPool = [2, 5, 6, 8];", script, StringComparison.Ordinal);
        Assert.Contains("04-theft.png", script, StringComparison.Ordinal);
        Assert.Contains("ActionCardTheftPreview", script, StringComparison.Ordinal);
        Assert.Contains("theftNoPlayers", script, StringComparison.Ordinal);
        Assert.Contains("closest('.word-rings-word.is-action-blocked')", script, StringComparison.Ordinal);
        Assert.Contains("lastRenderedCardSignature", script, StringComparison.Ordinal);
        Assert.Contains("new MutationObserver", pointerDrag, StringComparison.Ordinal);
        Assert.Contains("soundEffectsEnabled", coopScript, StringComparison.Ordinal);
        Assert.Contains("regularVisibleBankWords", coopScript, StringComparison.Ordinal);
        Assert.Contains("data-action-overflow-word=\"true\"", coopScript, StringComparison.Ordinal);
        Assert.Contains("wordrings:bank-rendered", coopScript, StringComparison.Ordinal);
        Assert.Contains("dedicatedHostCard", coopScript, StringComparison.Ordinal);
        Assert.Contains("progress.hidden = dedicatedHostViewer", coopScript, StringComparison.Ordinal);
        Assert.Contains("regularVisibleBankWords", soloScript, StringComparison.Ordinal);
        Assert.Contains("data-room-sound-enabled", page, StringComparison.Ordinal);

        Assert.Contains("border: 0;", css, StringComparison.Ordinal);
        Assert.Contains("overflow: visible;", css, StringComparison.Ordinal);
        Assert.Contains("transform: translateY(-5px) scale(1.09)", css, StringComparison.Ordinal);
        Assert.Contains("word-rings-action-timeout-pop 2.05s", css, StringComparison.Ordinal);
        Assert.Contains("width: auto !important;", css, StringComparison.Ordinal);
        Assert.Contains("overflow-x: hidden;", css, StringComparison.Ordinal);
        Assert.Contains("max-width: 136px;", css, StringComparison.Ordinal);
        Assert.Contains("object-fit: cover;", css, StringComparison.Ordinal);
        Assert.Contains("word-rings-action-card-name,", css, StringComparison.Ordinal);
        Assert.Contains("width: min(92vw, 430px);", css, StringComparison.Ordinal);
        Assert.Contains("width: min(100%, 280px);", css, StringComparison.Ordinal);

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
        Assert.Contains("Theft = 4", service, StringComparison.Ordinal);
        Assert.DoesNotContain("Temporary = 4", service, StringComparison.Ordinal);
        Assert.Contains("GetTheftPreview", service, StringComparison.Ordinal);
        Assert.Contains("TheftReveals", service, StringComparison.Ordinal);
        Assert.Contains("InvalidateTheftReveals", service, StringComparison.Ordinal);
        Assert.Contains("target.Cards.Remove(revealed)", service, StringComparison.Ordinal);
        Assert.Contains("TimeoutNoticeRevision", service, StringComparison.Ordinal);
        Assert.Contains("TimeoutNoticePlayerName", service, StringComparison.Ordinal);
        Assert.Contains("actorMeta.BlockedWords.Remove(actorWord);", service, StringComparison.Ordinal);
        Assert.Contains("if (state.DedicatedHostMode)", service, StringComparison.Ordinal);
        Assert.Contains("return null;", service, StringComparison.Ordinal);
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
