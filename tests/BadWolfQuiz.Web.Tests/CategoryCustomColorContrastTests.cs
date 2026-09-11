namespace BadWolfQuiz.Web.Tests;

public sealed class CategoryCustomColorContrastTests
{
    [Fact]
    public void Custom_category_surfaces_keep_one_foreground_above_large_text_contrast()
    {
        for (var red = 0; red <= 255; red += 17)
        for (var green = 0; green <= 255; green += 17)
        for (var blue = 0; blue <= 255; blue += 17)
        {
            var selected = new Rgb(red, green, blue);
            var foreground = ChooseForeground(selected);
            var contrastTarget = foreground == White ? Black : White;
            var headerEnd = Mix(selected, contrastTarget, 0.12);
            var cell = Mix(selected, contrastTarget, 0.22);
            var cellEnd = Mix(cell, contrastTarget, 0.12);
            var resolved = Mix(selected, contrastTarget, 0.60);
            var resolvedEnd = Mix(resolved, contrastTarget, 0.10);

            AssertSurfaceContrast(selected, foreground);
            AssertSurfaceContrast(headerEnd, foreground);
            AssertSurfaceContrast(cell, foreground);
            AssertSurfaceContrast(cellEnd, foreground);
            AssertSurfaceContrast(resolved, foreground);
            AssertSurfaceContrast(resolvedEnd, foreground);
        }
    }

    private static void AssertSurfaceContrast(Rgb background, Rgb foreground)
    {
        var contrast = ContrastRatio(background, foreground);
        Assert.True(
            contrast >= 3.0,
            $"Contrast was {contrast:F2}:1 for {background} with {foreground} text.");
    }

    [Fact]
    public void Saturated_red_prefers_white_board_foreground()
    {
        Assert.Equal(White, ChooseForeground(new Rgb(235, 36, 36)));
        Assert.Equal(White, ChooseForeground(new Rgb(255, 71, 71)));
    }

    private static Rgb ChooseForeground(Rgb background) =>
        ContrastRatio(background, White) >= 3.0
            ? White
            : Black;

    private static Rgb Mix(Rgb source, Rgb target, double amount) => new(
        (int)Math.Round(source.R + ((target.R - source.R) * amount)),
        (int)Math.Round(source.G + ((target.G - source.G) * amount)),
        (int)Math.Round(source.B + ((target.B - source.B) * amount)));

    private static double ContrastRatio(Rgb first, Rgb second)
    {
        var high = Math.Max(Luminance(first), Luminance(second));
        var low = Math.Min(Luminance(first), Luminance(second));
        return (high + 0.05) / (low + 0.05);
    }

    private static double Luminance(Rgb color) =>
        (0.2126 * Linear(color.R)) +
        (0.7152 * Linear(color.G)) +
        (0.0722 * Linear(color.B));

    private static double Linear(int channel)
    {
        var value = channel / 255d;
        return value <= 0.04045
            ? value / 12.92
            : Math.Pow((value + 0.055) / 1.055, 2.4);
    }

    private static readonly Rgb White = new(255, 255, 255);
    private static readonly Rgb Black = new(0, 0, 0);
    private sealed record Rgb(int R, int G, int B);
}
