namespace BadWolfQuiz.Web.Tests;

public sealed class HostRunningGameLayoutRegressionTests
{
    [Fact]
    public void Running_question_tiles_stay_inside_fractional_grid_rows_at_browser_zoom()
    {
        var css = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "host-running-game.css"));

        var selector = "body.gameplay-layout:has(.host-game-board[data-game-status=\"running\"]) .host-board-question {";
        var blockStart = css.IndexOf(selector, StringComparison.Ordinal);
        var blockEnd = css.IndexOf('}', blockStart);
        var block = css[blockStart..blockEnd];

        Assert.True(blockStart >= 0);
        Assert.Contains("min-height: 0;", block);
        Assert.Contains("height: 100%;", block);
        Assert.Contains("max-height: 100%;", block);
        Assert.DoesNotContain("min-height: clamp(", block);
    }

    [Fact]
    public void Running_question_hover_does_not_move_the_tile_into_neighbouring_rows()
    {
        var css = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "host-running-game.css"));

        var selector = "body.gameplay-layout:has(.host-game-board[data-game-status=\"running\"]) .host-board-question.status-available:hover,";
        var blockStart = css.IndexOf(selector, StringComparison.Ordinal);
        var blockEnd = css.IndexOf('}', blockStart);
        var block = css[blockStart..blockEnd];

        Assert.True(blockStart >= 0);
        Assert.DoesNotContain("translateY(", block);
        Assert.Contains("border-color:", block);
        Assert.Contains("box-shadow:", block);
    }

    [Fact]
    public void Category_color_overrides_change_paint_only_and_not_board_geometry()
    {
        var css = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "host-running-game.css"))
            .Replace("\r\n", "\n");

        Assert.Contains("var(--board-category-header-bg,", css);
        Assert.Contains("var(--board-category-cell-bg,", css);
        Assert.Contains("var(--board-category-resolved-bg,", css);
        Assert.Contains("var(--board-category-header-foreground, var(--board-category-foreground, var(--text)))", css);
        Assert.Contains("var(--board-category-cell-foreground, var(--board-category-foreground, var(--gold)))", css);

        var colorLines = css.Split('\n')
            .Where(line => line.Contains("--board-category-", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(colorLines);
        Assert.All(colorLines, line =>
        {
            Assert.DoesNotContain("grid-template", line, StringComparison.Ordinal);
            Assert.DoesNotContain("width:", line, StringComparison.Ordinal);
            Assert.DoesNotContain("height:", line, StringComparison.Ordinal);
            Assert.DoesNotContain("padding:", line, StringComparison.Ordinal);
            Assert.DoesNotContain("margin:", line, StringComparison.Ordinal);
            Assert.DoesNotContain("gap:", line, StringComparison.Ordinal);
            Assert.DoesNotContain("position:", line, StringComparison.Ordinal);
            Assert.DoesNotContain("transform:", line, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Narrow_viewport_does_not_reintroduce_width_based_question_minimum_height()
    {
        var css = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "host-running-game.css"));

        var mediaStart = css.IndexOf("@media (max-width: 900px)", StringComparison.Ordinal);
        var mediaEnd = css.IndexOf("@media (prefers-reduced-motion: reduce)", mediaStart, StringComparison.Ordinal);
        var mediaBlock = css[mediaStart..mediaEnd];

        Assert.True(mediaStart >= 0);
        Assert.DoesNotContain(".host-board-question", mediaBlock);
        Assert.DoesNotContain("min-height: clamp(3.2rem, 10vw, 4.8rem);", mediaBlock);
    }

    private static string FindWebFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src",
                "BadWolfQuiz.Web",
                Path.Combine(parts));

            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate BadWolfQuiz.Web file: {Path.Combine(parts)}");
    }
}
