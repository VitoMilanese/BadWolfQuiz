namespace BadWolfQuiz.Web.Tests;

public sealed class WordRingsGameplayRefinementRegressionTests
{
    [Fact]
    public void Gameplay_uses_full_viewport_toolbar_right_word_bank_and_at_most_ten_words()
    {
        var page = ReadWebFile("Pages", "WordRings.cshtml");
        var styles = ReadWebFile("wwwroot", "css", "word-rings-refinements.css");

        Assert.Contains("~/css/minigames.css", page, StringComparison.Ordinal);
        Assert.Contains("minigames-toolbar word-rings-toolbar", page, StringComparison.Ordinal);
        Assert.Contains("data-game-mode=\"solo\"", page, StringComparison.Ordinal);
        Assert.Contains("Math.Min(8, matchingWords.Count)", page, StringComparison.Ordinal);
        Assert.Contains("selectedMatchingCount / 4", page, StringComparison.Ordinal);
        Assert.Contains("Take(10)", page, StringComparison.Ordinal);
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
    public void Rings_are_thicker_outlined_and_checked_words_keep_contrast_feedback_fill()
    {
        var styles = ReadWebFile("wwwroot", "css", "word-rings-refinements.css");

        Assert.Contains("border-width: clamp(4px, .56vw, 8px)", styles, StringComparison.Ordinal);
        Assert.Contains("outline: 1px solid", styles, StringComparison.Ordinal);
        Assert.Contains("inset 0 0 0 1px", styles, StringComparison.Ordinal);
        Assert.Contains("#33c46f 30%", styles, StringComparison.Ordinal);
        Assert.Contains("#e85d5d 30%", styles, StringComparison.Ordinal);
        Assert.Contains("left 240ms ease", styles, StringComparison.Ordinal);
        Assert.Contains("top 240ms ease", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void Gameplay_checks_one_new_word_at_a_time_auto_corrects_solo_errors_and_raises_clicked_words()
    {
        var script = ReadWebFile("wwwroot", "js", "word-rings.js");
        var pointerDrag = ReadWebFile("wwwroot", "js", "word-rings-pointer-drag.js");

        Assert.Contains("let pendingWord = null", script, StringComparison.Ordinal);
        Assert.Contains("const verdicts = new Map()", script, StringComparison.Ordinal);
        Assert.Contains("const lockedMemberships = new Map()", script, StringComparison.Ordinal);
        Assert.Contains("const isSoloMode", script, StringComparison.Ordinal);
        Assert.Contains("checkButton.disabled = pendingWord === null", script, StringComparison.Ordinal);
        Assert.Contains("const findBestPlacement", script, StringComparison.Ordinal);
        Assert.Contains("const moveWordToCorrectMembership", script, StringComparison.Ordinal);
        Assert.Contains("membershipFromGeometry", script, StringComparison.Ordinal);
        Assert.Contains("overlapArea", script, StringComparison.Ordinal);
        Assert.Contains("moveWordToCorrectMembership(word, expectedMembership)", script, StringComparison.Ordinal);
        Assert.Contains("lockedMemberships.set(word, expectedMembership)", script, StringComparison.Ordinal);
        Assert.Contains("lockedMemberships.get(word) === normalized", script, StringComparison.Ordinal);
        Assert.Contains("canReturnToBank = word => !verdicts.has(word)", script, StringComparison.Ordinal);
        Assert.Contains("bringToFront", script, StringComparison.Ordinal);

        Assert.Contains("canBegin", pointerDrag, StringComparison.Ordinal);
        Assert.Contains("canDropStage", pointerDrag, StringComparison.Ordinal);
        Assert.Contains("canReturnToBank", pointerDrag, StringComparison.Ordinal);
        Assert.Contains("bringToFront(word)", pointerDrag, StringComparison.Ordinal);
        Assert.Contains("!drag.moved", pointerDrag, StringComparison.Ordinal);
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
