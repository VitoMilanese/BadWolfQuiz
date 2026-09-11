namespace BadWolfQuiz.Web.Tests;

public sealed class BoardHeaderLayoutRegressionTests
{
    [Fact]
    public void Host_board_header_layout_synchronizes_immediately_before_follow_up_frame()
    {
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "board-header-layout.js"))
            .Replace("\r\n", "\n");

        Assert.Contains("grid.getClientRects().length === 0", script);
        Assert.Contains(
            "document.addEventListener(\n        \"badwolf:host-gameplay-updated\",\n        syncBeforePaint);",
            script);
        Assert.Contains(
            "const syncBeforePaint = () => {\n        syncHeaderHeights();",
            script);

        var immediateSync = script.IndexOf(
            "const syncBeforePaint = () => {\n        syncHeaderHeights();",
            StringComparison.Ordinal);
        var followUpFrame = script.IndexOf(
            "window.requestAnimationFrame(() => {",
            immediateSync,
            StringComparison.Ordinal);

        Assert.True(immediateSync >= 0);
        Assert.True(followUpFrame > immediateSync);
    }

    [Fact]
    public void Host_board_applies_stable_automatic_category_colors_before_header_measurement()
    {
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "board-header-layout.js"))
            .Replace("\r\n", "\n");

        Assert.Contains(
            "const baseCategoryHues = [215, 32, 145, 266, 330, 185, 52, 8, 105, 295, 165, 245];",
            script);
        Assert.Contains("const cycle = Math.floor(index / baseCategoryHues.length);", script);
        Assert.Contains("(baseCategoryHues[index % baseCategoryHues.length] + (cycle * 17)) % 360", script);
        Assert.Contains("const mode = column.dataset.categoryColorMode || \"automatic\";", script);
        Assert.Contains("if (mode === \"theme\")", script);
        Assert.Contains("mode === \"custom\"", script);
        Assert.Contains("--board-category-header-bg", script);
        Assert.Contains("--board-category-cell-bg", script);
        Assert.Contains("--board-category-foreground", script);
        Assert.Contains("applyCategoryColors();", script);
    }

    [Theory]
    [InlineData(0.76, 0.27)]
    [InlineData(0.66, 0.20)]
    public void Automatic_category_backgrounds_keep_white_text_above_wcag_contrast(
        double saturation,
        double lightness)
    {
        var minimumContrast = Enumerable.Range(0, 360)
            .Select(hue => ContrastWithWhite(HslToRgb(hue, saturation, lightness)))
            .Min();

        Assert.True(
            minimumContrast >= 4.5,
            $"Minimum white-text contrast was {minimumContrast:F2}:1.");
    }

    [Fact]
    public void Host_board_supports_custom_and_theme_category_color_modes()
    {
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "board-header-layout.js"));

        Assert.Contains("column.dataset.categoryCustomColor", script, StringComparison.Ordinal);
        Assert.Contains("applyCustomCategoryColor", script, StringComparison.Ordinal);
        Assert.Contains("gradientEndForForeground", script, StringComparison.Ordinal);
        Assert.Contains("chooseForeground", script, StringComparison.Ordinal);
        Assert.Contains("--board-category-header-foreground", script, StringComparison.Ordinal);
        Assert.Contains("--board-category-cell-foreground", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Custom_category_color_uses_one_foreground_for_the_entire_column()
    {
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "board-header-layout.js"));

        Assert.Contains("const foreground = chooseForeground(base);", script, StringComparison.Ordinal);
        Assert.Contains(@"const contrastTarget = foreground === ""#ffffff"" ? black : white;", script, StringComparison.Ordinal);
        Assert.Contains(@"""--board-category-header-foreground"", foreground", script, StringComparison.Ordinal);
        Assert.Contains(@"""--board-category-cell-foreground"", foreground", script, StringComparison.Ordinal);
        Assert.Contains(@"""--board-category-resolved-foreground"", foreground", script, StringComparison.Ordinal);
        Assert.DoesNotContain("const cellForeground =", script, StringComparison.Ordinal);
        Assert.DoesNotContain("const resolvedForeground =", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Host_gameplay_bootstrap_loads_board_header_layout()
    {
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "gameplay-escape-shortcuts.js"));

        Assert.Contains(
            "loadSharedScript(\"/js/board-header-layout.js\"",
            script);
        Assert.Contains("const ensureBoardHeaderLayout = () =>", script, StringComparison.Ordinal);
        Assert.Contains("\"badwolf:host-shell-mounted\"", script, StringComparison.Ordinal);
        Assert.Contains("document.addEventListener(eventName, ensureBoardHeaderLayout);", script, StringComparison.Ordinal);
    }

    private static (double R, double G, double B) HslToRgb(
        int hue,
        double saturation,
        double lightness)
    {
        var chroma = (1 - Math.Abs((2 * lightness) - 1)) * saturation;
        var segment = hue / 60d;
        var secondary = chroma * (1 - Math.Abs((segment % 2) - 1));
        var (r1, g1, b1) = segment switch
        {
            < 1 => (chroma, secondary, 0d),
            < 2 => (secondary, chroma, 0d),
            < 3 => (0d, chroma, secondary),
            < 4 => (0d, secondary, chroma),
            < 5 => (secondary, 0d, chroma),
            _ => (chroma, 0d, secondary)
        };
        var match = lightness - (chroma / 2);
        return (r1 + match, g1 + match, b1 + match);
    }

    private static double ContrastWithWhite((double R, double G, double B) color)
    {
        var luminance =
            (0.2126 * ToLinear(color.R)) +
            (0.7152 * ToLinear(color.G)) +
            (0.0722 * ToLinear(color.B));
        return 1.05 / (luminance + 0.05);
    }

    private static double ToLinear(double channel) =>
        channel <= 0.04045
            ? channel / 12.92
            : Math.Pow((channel + 0.055) / 1.055, 2.4);

    private static string FindWebFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var pathParts = new string[parts.Length + 3];
            pathParts[0] = directory.FullName;
            pathParts[1] = "src";
            pathParts[2] = "BadWolfQuiz.Web";
            Array.Copy(parts, 0, pathParts, 3, parts.Length);

            var candidate = Path.Combine(pathParts);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not find web file: {string.Join('/', parts)}.");
    }
}
