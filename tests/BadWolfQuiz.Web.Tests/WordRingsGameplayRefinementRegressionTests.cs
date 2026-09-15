using BadWolfQuiz.Web.Pages;
using BadWolfQuiz.Web.Services;
using System.Reflection;

namespace BadWolfQuiz.Web.Tests;

public sealed class WordRingsGameplayRefinementRegressionTests
{
    [Fact]
    public void Gameplay_uses_full_viewport_toolbar_streaming_bank_and_twenty_word_solo_limit()
    {
        var page = ReadWebFile("Pages", "WordRings.cshtml");
        var model = ReadWebFile("Pages", "WordRings.cshtml.cs");
        var styles = ReadWebFile("wwwroot", "css", "word-rings-refinements.css");
        var script = ReadWebFile("wwwroot", "js", "word-rings.js");

        Assert.Contains("~/css/minigames.css", page, StringComparison.Ordinal);
        Assert.Contains("minigames-toolbar word-rings-toolbar", page, StringComparison.Ordinal);
        Assert.Contains("data-game-mode=\"@(isCooperativeRoom ? \"cooperative\" : \"solo\")\"", page, StringComparison.Ordinal);
        Assert.Contains("minigames-refresh-button", page, StringComparison.Ordinal);
        Assert.Contains("<span aria-hidden=\"true\">↻</span>", page, StringComparison.Ordinal);
        Assert.Contains("Model.InitialWords", page, StringComparison.Ordinal);
        Assert.Contains("data-word-rings-queue", page, StringComparison.Ordinal);
        Assert.Contains("data-bank-word-limit", page, StringComparison.Ordinal);
        Assert.Contains("data-game-word-limit", page, StringComparison.Ordinal);
        Assert.Contains("data-correct-word-target", page, StringComparison.Ordinal);
        Assert.Contains("MaximumBankWords = 10", model, StringComparison.Ordinal);
        Assert.Contains("MaximumGameWords = 20", model, StringComparison.Ordinal);
        Assert.Contains("CorrectWordsToWin = 10", model, StringComparison.Ordinal);
        Assert.Contains("InitialWords = DisplayedWords.Take(MaximumBankWords)", model, StringComparison.Ordinal);
        Assert.Contains("QueuedWords = DisplayedWords.Skip(MaximumBankWords)", model, StringComparison.Ordinal);
        Assert.Contains("Math.Ceiling(targetCount * 0.8)", model, StringComparison.Ordinal);
        Assert.Contains("InterleaveOutsideWords", model, StringComparison.Ordinal);
        Assert.Contains("SelectBalancedMatchingWords", model, StringComparison.Ordinal);
        Assert.Contains("underrepresentedRings * 1000.0", model, StringComparison.Ordinal);
        Assert.Contains(">= 3 => 750.0", model, StringComparison.Ordinal);
        Assert.Contains("2 => 450.0", model, StringComparison.Ordinal);
        Assert.Contains("ScoreDisplayedWords", model, StringComparison.Ordinal);
        Assert.Contains("tripleCount * 1500", model, StringComparison.Ordinal);
        Assert.Contains("dualCount * 900", model, StringComparison.Ordinal);
        Assert.Contains("spread * 350", model, StringComparison.Ordinal);
        Assert.Contains("PickFreshPuzzle", model, StringComparison.Ordinal);
        Assert.Contains("PuzzleSelectionAttempts = 32", model, StringComparison.Ordinal);
        Assert.Contains("previousWords", model, StringComparison.Ordinal);
        Assert.Contains("replenishWordBank", script, StringComparison.Ordinal);
        Assert.Contains("regularVisibleBankWords().length < maximumBankWords", script, StringComparison.Ordinal);
        Assert.Contains("queuedWords.shift()", script, StringComparison.Ordinal);
        Assert.Contains("trimWordBankToLimit", script, StringComparison.Ordinal);
        Assert.Contains("correctCount()", script, StringComparison.Ordinal);
        Assert.Contains("finishGameIfNeeded", script, StringComparison.Ordinal);
        Assert.Contains("successful < correctWordTarget && attempts < maximumAttempts", script, StringComparison.Ordinal);
        Assert.Contains("requestUrl.searchParams.set('handler', 'NewPuzzle');", script, StringComparison.Ordinal);
        Assert.Contains("window.history.replaceState", script, StringComparison.Ordinal);
        Assert.DoesNotContain("window.location.assign", script, StringComparison.Ordinal);
        Assert.Contains("previousBlueRule", script, StringComparison.Ordinal);
        Assert.Contains("previousYellowRule", script, StringComparison.Ordinal);
        Assert.Contains("previousRedRule", script, StringComparison.Ordinal);
        Assert.Contains("data-check disabled", page, StringComparison.Ordinal);
        Assert.Contains("data-word-rings-players hidden", page, StringComparison.Ordinal);
        Assert.True(
            page.IndexOf("word-rings-stage-wrap", StringComparison.Ordinal) <
            page.IndexOf("word-rings-bank", StringComparison.Ordinal));

        Assert.Contains("height: calc(100dvh", styles, StringComparison.Ordinal);
        Assert.Contains("grid-template-areas: \"stage bank\"", styles, StringComparison.Ordinal);
        Assert.Contains("grid-area: bank", styles, StringComparison.Ordinal);
        Assert.Contains("height: 100%", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void Solo_word_selection_prefers_overlap_and_balances_ring_coverage()
    {
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["abc1"] = "ABC",
            ["abc2"] = "ABC",
            ["ab1"] = "AB",
            ["ab2"] = "AB",
            ["ac1"] = "AC",
            ["ac2"] = "AC",
            ["bc1"] = "BC",
            ["bc2"] = "BC",
            ["a1"] = "A",
            ["a2"] = "A",
            ["a3"] = "A",
            ["b1"] = "B",
            ["b2"] = "B",
            ["b3"] = "B",
            ["c1"] = "C",
            ["c2"] = "C",
            ["c3"] = "C",
            ["outside1"] = string.Empty,
            ["outside2"] = string.Empty
        };
        var puzzle = new WordRingsPuzzle(
            "blue",
            "yellow",
            "red",
            expected.Keys.ToArray(),
            expected);

        var selected = InvokeBuildDisplayedWords(puzzle);
        var memberships = selected
            .Select(word => expected[word])
            .Where(membership => membership.Length > 0)
            .ToArray();

        Assert.Equal(19, selected.Count);
        Assert.Equal(17, memberships.Length);
        Assert.True(
            memberships.Count(membership => membership.Length >= 2) >= 8,
            "Expected the solo selector to strongly prefer words that belong to multiple rings.");
        Assert.All(
            new[] { 'A', 'B', 'C' },
            ring => Assert.Contains(memberships, membership => membership.Contains(ring)));
    }

    [Fact]
    public void Solo_word_selection_balances_single_ring_words_when_overlaps_do_not_exist()
    {
        var expected = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var ring in new[] { 'A', 'B', 'C' })
        {
            for (var index = 1; index <= 8; index++)
            {
                expected[$"{char.ToLowerInvariant(ring)}{index}"] = ring.ToString();
            }
        }
        expected["outside1"] = string.Empty;
        expected["outside2"] = string.Empty;

        var puzzle = new WordRingsPuzzle(
            "blue",
            "yellow",
            "red",
            expected.Keys.ToArray(),
            expected);
        var selected = InvokeBuildDisplayedWords(puzzle);
        var ringCounts = new Dictionary<char, int>
        {
            ['A'] = 0,
            ['B'] = 0,
            ['C'] = 0
        };

        foreach (var word in selected)
        {
            var membership = expected[word];
            foreach (var ring in membership)
            {
                ringCounts[ring]++;
            }
        }

        Assert.Equal(20, selected.Count);
        Assert.Equal(18, ringCounts.Values.Sum());
        Assert.True(
            ringCounts.Values.Max() - ringCounts.Values.Min() <= 1,
            $"Expected near-even single-ring coverage, got A={ringCounts['A']}, B={ringCounts['B']}, C={ringCounts['C']}.");
    }

    [Fact]
    public void Rings_are_transparent_gapless_outlined_and_stage_has_no_inner_frame()
    {
        var styles = ReadWebFile("wwwroot", "css", "word-rings-refinements.css");

        Assert.Contains("border: 0;", styles, StringComparison.Ordinal);
        Assert.Contains("background: transparent;", styles, StringComparison.Ordinal);
        Assert.Contains("border-width: clamp(4px, .56vw, 8px)", styles, StringComparison.Ordinal);
        Assert.Contains("outline: 0;", styles, StringComparison.Ordinal);
        Assert.Contains("0 0 0 1px color-mix", styles, StringComparison.Ordinal);
        Assert.Contains("inset 0 0 0 1px", styles, StringComparison.Ordinal);
        Assert.DoesNotContain("outline-offset: 1px", styles, StringComparison.Ordinal);
        Assert.Contains("#33c46f 30%", styles, StringComparison.Ordinal);
        Assert.Contains("#e85d5d 30%", styles, StringComparison.Ordinal);
        Assert.Contains("left 240ms ease", styles, StringComparison.Ordinal);
        Assert.Contains("top 240ms ease", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void Gameplay_checks_one_new_word_at_a_time_auto_corrects_solo_errors_and_keeps_drag_preview_under_pointer()
    {
        var script = ReadWebFile("wwwroot", "js", "word-rings.js");
        var pointerDrag = ReadWebFile("wwwroot", "js", "word-rings-pointer-drag.js");

        Assert.Contains("let pendingWord = null", script, StringComparison.Ordinal);
        Assert.Contains("const verdicts = new Map()", script, StringComparison.Ordinal);
        Assert.Contains("const lockedMemberships = new Map()", script, StringComparison.Ordinal);
        Assert.Contains("const isSoloMode", script, StringComparison.Ordinal);
        Assert.Contains("checkButton.disabled = gameOver || pendingWord === null", script, StringComparison.Ordinal);
        Assert.Contains("const findBestPlacement", script, StringComparison.Ordinal);
        Assert.Contains("const moveWordToCorrectMembership", script, StringComparison.Ordinal);
        Assert.Contains("membershipFromGeometry", script, StringComparison.Ordinal);
        Assert.Contains("overlapArea", script, StringComparison.Ordinal);
        Assert.Contains("moveWordToCorrectMembership(word, expectedMembership)", script, StringComparison.Ordinal);
        Assert.Contains("lockedMemberships.set(word, expectedMembership)", script, StringComparison.Ordinal);
        Assert.Contains("lockedMemberships.get(word) === normalized", script, StringComparison.Ordinal);
        Assert.Contains("canReturnToBank = (word, source) =>", script, StringComparison.Ordinal);
        Assert.Contains("bringToFront", script, StringComparison.Ordinal);

        Assert.Contains("canBegin", pointerDrag, StringComparison.Ordinal);
        Assert.Contains("canDropStage", pointerDrag, StringComparison.Ordinal);
        Assert.Contains("canReturnToBank", pointerDrag, StringComparison.Ordinal);
        Assert.Contains("bringToFront(word)", pointerDrag, StringComparison.Ordinal);
        Assert.Contains("!drag.moved", pointerDrag, StringComparison.Ordinal);
        Assert.Contains("const clickRatioX", pointerDrag, StringComparison.Ordinal);
        Assert.Contains("const clickRatioY", pointerDrag, StringComparison.Ordinal);
        Assert.Contains("previewRect.width * clickRatioX", pointerDrag, StringComparison.Ordinal);
        Assert.Contains("previewRect.height * clickRatioY", pointerDrag, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_cover_uses_blue_yellow_red_ring_order_without_word_pills()
    {
        var svg = ReadWebFile("wwwroot", "images", "minigames", "word-rings.svg");

        Assert.Contains("cx=\"610\" cy=\"365\" r=\"255\" stroke=\"#5f86ee\"", svg, StringComparison.Ordinal);
        Assert.Contains("cx=\"990\" cy=\"365\" r=\"255\" stroke=\"#e0b43c\"", svg, StringComparison.Ordinal);
        Assert.Contains("cx=\"800\" cy=\"610\" r=\"255\" stroke=\"#e85d5d\"", svg, StringComparison.Ordinal);
        Assert.DoesNotContain("fill=\"#f4f5f7\"", svg, StringComparison.Ordinal);
        Assert.DoesNotContain("rx=\"29\"", svg, StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> InvokeBuildDisplayedWords(WordRingsPuzzle puzzle)
    {
        var method = typeof(WordRingsModel).GetMethod(
            "BuildDisplayedWords",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        return Assert.IsAssignableFrom<IReadOnlyList<string>>(
            method!.Invoke(null, [puzzle, Array.Empty<string>()]));
    }

    private static string ReadWebFile(params string[] parts) => File.ReadAllText(FindWebFile(parts));

    private static string FindWebFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName, "src", "BadWolfQuiz.Web" }.Concat(parts).ToArray());
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate BadWolfQuiz.Web file: {Path.Combine(parts)}");
    }
}
