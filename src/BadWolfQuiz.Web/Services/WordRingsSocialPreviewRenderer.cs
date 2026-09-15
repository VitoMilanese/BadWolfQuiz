using SkiaSharp;

namespace BadWolfQuiz.Web.Services;

public static class WordRingsSocialPreviewRenderer
{
    private const int Width = SocialPreviewImageRenderer.Width;
    private const int Height = SocialPreviewImageRenderer.Height;

    private static readonly SKColor Background = SKColor.Parse("#061529");
    private static readonly SKColor BackgroundSecondary = SKColor.Parse("#12385e");
    private static readonly SKColor Text = SKColor.Parse("#f4f7fb");
    private static readonly SKColor Muted = SKColor.Parse("#9bb6d3");
    private static readonly SKColor Gold = SKColor.Parse("#ffd700");
    private static readonly SKColor Blue = SKColor.Parse("#5f86ee");
    private static readonly SKColor Yellow = SKColor.Parse("#e0b43c");
    private static readonly SKColor Red = SKColor.Parse("#e85d5d");

    public static byte[] Render(string? variant)
    {
        var copy = ResolveCopy(variant);

        using var bitmap = new SKBitmap(new SKImageInfo(
            Width,
            Height,
            SKColorType.Bgra8888,
            SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);

        DrawBackground(canvas);
        DrawRings(canvas);
        DrawBranding(canvas, copy);

        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 92);
        return encoded.ToArray();
    }

    private static void DrawBackground(SKCanvas canvas)
    {
        using var backgroundPaint = new SKPaint { IsAntialias = true };
        using var shader = SKShader.CreateLinearGradient(
            new SKPoint(0, 0),
            new SKPoint(Width, Height),
            [Background, BackgroundSecondary, Background],
            [0f, 0.58f, 1f],
            SKShaderTileMode.Clamp);
        backgroundPaint.Shader = shader;
        canvas.DrawRect(0, 0, Width, Height, backgroundPaint);

        using var leftGlowPaint = new SKPaint
        {
            Color = WithAlpha(Blue, 26),
            IsAntialias = true
        };
        canvas.DrawCircle(340, 330, 300, leftGlowPaint);

        using var rightGlowPaint = new SKPaint
        {
            Color = WithAlpha(Gold, 14),
            IsAntialias = true
        };
        canvas.DrawCircle(1025, 110, 250, rightGlowPaint);

        using var linePaint = new SKPaint
        {
            Color = WithAlpha(Text, 10),
            StrokeWidth = 1,
            IsAntialias = true
        };
        for (var x = -220; x < Width + 220; x += 52)
        {
            canvas.DrawLine(x, Height, x + 360, 0, linePaint);
        }
    }

    private static void DrawRings(SKCanvas canvas)
    {
        DrawRing(canvas, 250, 250, 150, Blue);
        DrawRing(canvas, 425, 250, 150, Yellow);
        DrawRing(canvas, 338, 405, 150, Red);

        using var centerPaint = new SKPaint
        {
            Color = WithAlpha(Text, 18),
            IsAntialias = true
        };
        canvas.DrawCircle(338, 330, 46, centerPaint);
    }

    private static void DrawRing(
        SKCanvas canvas,
        float centerX,
        float centerY,
        float radius,
        SKColor color)
    {
        using var outerGlow = new SKPaint
        {
            Color = WithAlpha(color, 28),
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 34
        };
        canvas.DrawCircle(centerX, centerY, radius, outerGlow);

        using var midGlow = new SKPaint
        {
            Color = WithAlpha(color, 88),
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 24
        };
        canvas.DrawCircle(centerX, centerY, radius, midGlow);

        using var core = new SKPaint
        {
            Color = color,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 13
        };
        canvas.DrawCircle(centerX, centerY, radius, core);
    }

    private static void DrawBranding(SKCanvas canvas, PreviewCopy copy)
    {
        using var boldTypeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold);
        using var regularTypeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Normal);

        using var eyebrowPaint = new SKPaint
        {
            Color = Gold,
            IsAntialias = true,
            TextSize = 25,
            Typeface = boldTypeface ?? SKTypeface.Default
        };
        canvas.DrawText("BAD WOLF QUIZ  /  MINIGAME 02", 650, 120, eyebrowPaint);

        using var titlePaint = new SKPaint
        {
            Color = Text,
            IsAntialias = true,
            TextSize = 66,
            Typeface = boldTypeface ?? SKTypeface.Default
        };
        canvas.DrawText(copy.TitleLine1, 650, 245, titlePaint);
        if (!string.IsNullOrWhiteSpace(copy.TitleLine2))
        {
            canvas.DrawText(copy.TitleLine2, 650, 320, titlePaint);
        }

        using var dividerPaint = new SKPaint
        {
            Color = Blue,
            IsAntialias = true,
            StrokeWidth = 4
        };
        canvas.DrawLine(652, 368, 1110, 368, dividerPaint);

        using var subtitlePaint = new SKPaint
        {
            Color = Muted,
            IsAntialias = true,
            TextSize = 27,
            Typeface = regularTypeface ?? SKTypeface.Default
        };
        canvas.DrawText(copy.Subtitle, 652, 426, subtitlePaint);

        DrawLegendDot(canvas, 654, 474, Blue);
        DrawLegendDot(canvas, 695, 474, Yellow);
        DrawLegendDot(canvas, 736, 474, Red);

        using var domainPaint = new SKPaint
        {
            Color = Muted,
            IsAntialias = true,
            TextSize = 24,
            Typeface = regularTypeface ?? SKTypeface.Default
        };
        canvas.DrawText("badwolf.buzz/minigames/word-rings", 652, 548, domainPaint);
    }

    private static void DrawLegendDot(SKCanvas canvas, float x, float y, SKColor color)
    {
        using var paint = new SKPaint
        {
            Color = color,
            IsAntialias = true
        };
        canvas.DrawCircle(x, y, 9, paint);
    }

    private static PreviewCopy ResolveCopy(string? variant)
    {
        var normalized = variant?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "word-rings-uk" => new PreviewCopy(
                "СЛІВЦЕ",
                "В КІЛЬЦЕ",
                "3 ПРИХОВАНІ ПРАВИЛА  •  3 КІЛЬЦЯ"),
            "word-rings-it" => new PreviewCopy(
                "PAROLA NEL",
                "CERCHIO",
                "3 REGOLE NASCOSTE  •  3 CERCHI"),
            "word-rings-ru" => new PreviewCopy(
                "УКРАЇНА",
                string.Empty,
                "УКРАЇНА"),
            _ => new PreviewCopy(
                "WORD INTO",
                "THE RING",
                "3 HIDDEN RULES  •  3 RINGS")
        };
    }

    private static SKColor WithAlpha(SKColor color, byte alpha) =>
        new(color.Red, color.Green, color.Blue, alpha);

    private readonly record struct PreviewCopy(
        string TitleLine1,
        string TitleLine2,
        string Subtitle);
}
