namespace BadWolfQuiz.Web.Tests;

public sealed class CategoryCustomColorContrastTests
{
    [Fact]
    public void Custom_category_surfaces_keep_at_least_wcag_aa_text_contrast()
    {
        for (var red = 0; red <= 255; red += 17)
        for (var green = 0; green <= 255; green += 17)
        for (var blue = 0; blue <= 255; blue += 17)
        {
            var selected = new Rgb(red, green, blue);
            AssertSurfaceContrast(selected, 0.12);
            AssertSurfaceContrast(Mix(selected, Black, 0.22), 0.12);
            AssertSurfaceContrast(Mix(selected, Black, 0.60), 0.10);
        }
    }

    private static void AssertSurfaceContrast(Rgb background, double gradientAmount)
    {
        var foreground = ChooseForeground(background);
        var gradientEnd = Mix(
            background,
            foreground == White ? Black : White,
            gradientAmount);
        var minimum = Math.Min(
            ContrastRatio(background, foreground),
            ContrastRatio(gradientEnd, foreground));
        Assert.True(minimum >= 4.5, $"Contrast was {minimum:F2}:1 for {background}.");
    }

    private static Rgb ChooseForeground(Rgb background) =>
        ContrastRatio(background, White) >= ContrastRatio(background, Black)
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
