using BadWolfQuiz.Web.Services;

namespace BadWolfQuiz.Web.Tests;

public sealed class WordRingsLatestGameplayRegressionTests
{
    private static readonly string[] ActionAchievementCodes =
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
    public void First_turn_roulette_is_one_shot_for_the_room_round()
    {
        var script = ReadWebFile("wwwroot", "js", "word-rings-gameplay-options.js");

        Assert.Contains("firstTurnRouletteDone()", script, StringComparison.Ordinal);
        Assert.Contains("markFirstTurnRouletteDone();", script, StringComparison.Ordinal);
        Assert.Contains("sessionStorage.setItem(firstTurnRouletteStorageKey, 'done')", script, StringComparison.Ordinal);
        Assert.Contains("some(item => item.isSeed !== true)", script, StringComparison.Ordinal);
        Assert.DoesNotContain("`${roomCode}:${state.version}:${state.currentPlayerId}`", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Host_player_card_actions_fire_on_pointer_down_before_polling_can_replace_the_card()
    {
        var script = ReadWebFile("wwwroot", "js", "word-rings-room-host-controls.js");

        Assert.Contains("const wirePlayerAction", script, StringComparison.Ordinal);
        Assert.Contains("button.addEventListener('pointerdown'", script, StringComparison.Ordinal);
        Assert.Contains("wirePlayerAction(pass", script, StringComparison.Ordinal);
        Assert.Contains("wirePlayerAction(kick", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Pending_word_temporarily_reduces_the_visible_hand_and_refills_after_non_pending_check()
    {
        var script = ReadWebFile("wwwroot", "js", "word-rings-gameplay-options.js");
        var refill = ReadWebFile("wwwroot", "js", "word-rings-pending-hand-refill.js");
        var assets = ReadWebFile("TagHelpers", "WordRingsActionCardsAssetsTagHelper.cs");

        Assert.Contains("const pendingHandDeficit", script, StringComparison.Ordinal);
        Assert.Contains("root.classList.contains('has-pending-word')", script, StringComparison.Ordinal);
        Assert.Contains("effectiveHandSize(currentOptions, values.target, values.correct) -", script, StringComparison.Ordinal);
        Assert.Contains("attributeFilter: ['class']", script, StringComparison.Ordinal);
        Assert.Contains("statePendingHandDeficit(state)", script, StringComparison.Ordinal);

        Assert.Contains("handler !== 'SubmitRoomWord'", refill, StringComparison.Ordinal);
        Assert.Contains("result?.isPending === true", refill, StringComparison.Ordinal);
        Assert.Contains("state.bankWords = [...state.bankWords, state.queuedWords[0]]", refill, StringComparison.Ordinal);
        Assert.Contains("word-rings-pending-hand-refill.js?v=1", assets, StringComparison.Ordinal);
        Assert.True(
            assets.IndexOf("word-rings-gameplay-options.js?v=2", StringComparison.Ordinal) <
            assets.IndexOf("word-rings-pending-hand-refill.js?v=1", StringComparison.Ordinal));
    }

    [Fact]
    public void Action_card_achievement_catalog_contains_all_nine_new_entries_and_total_is_ninety_nine()
    {
        Assert.Equal(99, PlayerAchievementService.Catalog.Count);
        foreach (var code in ActionAchievementCodes)
        {
            var definition = Assert.Single(PlayerAchievementService.Catalog, item => item.Code == code);
            Assert.Equal(PlayerAchievementMetric.DirectUnlock, definition.Metric);
            Assert.False(definition.IsSecret);
        }
    }

    private static string ReadWebFile(params string[] parts)
    {
        var path = Path.Combine(new[] { FindRepositoryRoot(), "src", "BadWolfQuiz.Web" }.Concat(parts).ToArray());
        return File.ReadAllText(path);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "BadWolfQuiz.sln")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the BadWolfQuiz repository root.");
    }
}