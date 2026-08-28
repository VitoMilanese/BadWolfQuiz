using BadWolfQuiz.Game.Runtime;
using SkiaSharp;

namespace BadWolfQuiz.Web.Services;

public static class SocialPreviewImageRenderer
{
    public const int Width = 1200;
    public const int Height = 630;

    public static byte[] Render(
        string? variant,
        string? themeId = null,
        SiteThemeColors? customThemeColors = null)
    {
        var isJoin = string.Equals(variant, "join", StringComparison.OrdinalIgnoreCase);
        var palette = SocialPreviewThemePalette.Resolve(themeId, customThemeColors);

        var background = ParseColor(palette.Background);
        var panel = ParseColor(palette.Panel);
        var panelSecondary = ParseColor(palette.PanelSecondary);
        var text = ParseColor(palette.Text);
        var muted = ParseColor(palette.MutedText);
        var accent = ParseColor(palette.Accent);
        var accentBright = ParseColor(palette.AccentBright);
        var highlight = ParseColor(palette.Highlight);

        using var bitmap = new SKBitmap(new SKImageInfo(
            Width,
            Height,
            SKColorType.Bgra8888,
            SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);

        DrawBackground(canvas, background, panelSecondary, accentBright);
        DrawAccentGeometry(canvas, accent, accentBright);
        DrawWolfMark(canvas, text, panel, panelSecondary, accentBright, background);
        DrawBranding(canvas, isJoin, text, muted, accentBright, highlight);

        return Encode(bitmap);
    }

    public static byte[] RenderQuizDescription(
        string title,
        string? description,
        double? averageRating,
        int ratingCount)
    {
        var palette = SocialPreviewThemePalette.Resolve(null, null);
        var background = ParseColor(palette.Background);
        var panel = ParseColor(palette.Panel);
        var panelSecondary = ParseColor(palette.PanelSecondary);
        var text = ParseColor(palette.Text);
        var muted = ParseColor(palette.MutedText);
        var accent = ParseColor(palette.Accent);
        var accentBright = ParseColor(palette.AccentBright);

        using var bitmap = new SKBitmap(new SKImageInfo(
            Width,
            Height,
            SKColorType.Bgra8888,
            SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);

        DrawBackground(canvas, background, panelSecondary, accentBright);
        DrawAccentGeometry(canvas, accent, accentBright);
        DrawWolfMark(canvas, text, panel, panelSecondary, accentBright, background);
        DrawQuizDescriptionBranding(
            canvas,
            title,
            description,
            averageRating,
            ratingCount,
            text,
            muted,
            accentBright);

        return Encode(bitmap);
    }

    private static byte[] Encode(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 92);
        return encoded.ToArray();
    }

    private static void DrawBackground(
        SKCanvas canvas,
        SKColor background,
        SKColor panelSecondary,
        SKColor accentBright)
    {
        using var backgroundPaint = new SKPaint { IsAntialias = true };
        using var backgroundShader = SKShader.CreateLinearGradient(
            new SKPoint(0, 0),
            new SKPoint(Width, Height),
            [background, panelSecondary, background],
            [0f, 0.56f, 1f],
            SKShaderTileMode.Clamp);
        backgroundPaint.Shader = backgroundShader;
        canvas.DrawRect(0, 0, Width, Height, backgroundPaint);

        using var glowPaint = new SKPaint
        {
            Color = WithAlpha(accentBright, 74),
            IsAntialias = true
        };
        canvas.DrawCircle(290, 318, 285, glowPaint);

        using var secondaryGlowPaint = new SKPaint
        {
            Color = WithAlpha(accentBright, 28),
            IsAntialias = true
        };
        canvas.DrawCircle(1010, 90, 220, secondaryGlowPaint);
    }

    private static void DrawAccentGeometry(
        SKCanvas canvas,
        SKColor accent,
        SKColor accentBright)
    {
        using var accentPaint = new SKPaint
        {
            Color = accent,
            IsAntialias = true
        };
        using var leftSlash = new SKPath();
        leftSlash.MoveTo(0, 0);
        leftSlash.LineTo(94, 0);
        leftSlash.LineTo(34, Height);
        leftSlash.LineTo(0, Height);
        leftSlash.Close();
        canvas.DrawPath(leftSlash, accentPaint);

        using var linePaint = new SKPaint
        {
            Color = WithAlpha(accentBright, 150),
            StrokeWidth = 3,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke
        };
        canvas.DrawLine(70, 565, 420, 95, linePaint);
        canvas.DrawLine(410, 95, 545, 325, linePaint);
    }

    private static void DrawWolfMark(
        SKCanvas canvas,
        SKColor text,
        SKColor panel,
        SKColor panelSecondary,
        SKColor accentBright,
        SKColor background)
    {
        using var silhouettePaint = new SKPaint
        {
            Color = text,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        using var silhouette = new SKPath();
        silhouette.MoveTo(94, 415);
        silhouette.LineTo(70, 128);
        silhouette.LineTo(207, 218);
        silhouette.LineTo(292, 112);
        silhouette.LineTo(377, 218);
        silhouette.LineTo(514, 128);
        silhouette.LineTo(490, 415);
        silhouette.LineTo(405, 494);
        silhouette.LineTo(342, 529);
        silhouette.LineTo(292, 582);
        silhouette.LineTo(242, 529);
        silhouette.LineTo(179, 494);
        silhouette.Close();
        canvas.DrawPath(silhouette, silhouettePaint);

        using var foreheadPaint = new SKPaint
        {
            Color = panel,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };
        using var forehead = new SKPath();
        forehead.MoveTo(292, 174);
        forehead.LineTo(235, 300);
        forehead.LineTo(260, 443);
        forehead.LineTo(292, 510);
        forehead.LineTo(324, 443);
        forehead.LineTo(349, 300);
        forehead.Close();
        canvas.DrawPath(forehead, foreheadPaint);

        using var facetPaint = new SKPaint
        {
            Color = panelSecondary,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        using var leftFacet = new SKPath();
        leftFacet.MoveTo(112, 206);
        leftFacet.LineTo(222, 270);
        leftFacet.LineTo(184, 406);
        leftFacet.LineTo(112, 370);
        leftFacet.Close();
        canvas.DrawPath(leftFacet, facetPaint);

        using var rightFacet = new SKPath();
        rightFacet.MoveTo(472, 206);
        rightFacet.LineTo(362, 270);
        rightFacet.LineTo(400, 406);
        rightFacet.LineTo(472, 370);
        rightFacet.Close();
        canvas.DrawPath(rightFacet, facetPaint);

        using var eyePaint = new SKPaint
        {
            Color = accentBright,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        using var leftEye = new SKPath();
        leftEye.MoveTo(166, 326);
        leftEye.LineTo(255, 347);
        leftEye.LineTo(238, 383);
        leftEye.LineTo(190, 368);
        leftEye.Close();
        canvas.DrawPath(leftEye, eyePaint);

        using var rightEye = new SKPath();
        rightEye.MoveTo(418, 326);
        rightEye.LineTo(329, 347);
        rightEye.LineTo(346, 383);
        rightEye.LineTo(394, 368);
        rightEye.Close();
        canvas.DrawPath(rightEye, eyePaint);

        using var muzzlePaint = new SKPaint
        {
            Color = panel,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };
        using var muzzle = new SKPath();
        muzzle.MoveTo(225, 430);
        muzzle.LineTo(292, 510);
        muzzle.LineTo(359, 430);
        muzzle.LineTo(336, 520);
        muzzle.LineTo(292, 566);
        muzzle.LineTo(248, 520);
        muzzle.Close();
        canvas.DrawPath(muzzle, muzzlePaint);

        using var nosePaint = new SKPaint
        {
            Color = background,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };
        using var nose = new SKPath();
        nose.MoveTo(259, 493);
        nose.LineTo(325, 493);
        nose.LineTo(292, 527);
        nose.Close();
        canvas.DrawPath(nose, nosePaint);
    }

    private static void DrawBranding(
        SKCanvas canvas,
        bool isJoin,
        SKColor text,
        SKColor muted,
        SKColor accentBright,
        SKColor highlight)
    {
        using var boldTypeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold);
        using var regularTypeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Normal);

        using var brandPaint = new SKPaint
        {
            Color = text,
            IsAntialias = true,
            TextSize = 78,
            Typeface = boldTypeface ?? SKTypeface.Default
        };
        canvas.DrawText("BAD WOLF", 570, 270, brandPaint);

        using var quizPaint = new SKPaint
        {
            Color = accentBright,
            IsAntialias = true,
            TextSize = 118,
            Typeface = boldTypeface ?? SKTypeface.Default
        };
        canvas.DrawText("QUIZ", 570, 390, quizPaint);

        using var dividerPaint = new SKPaint
        {
            Color = accentBright,
            StrokeWidth = 4,
            IsAntialias = true
        };
        canvas.DrawLine(572, 430, 1060, 430, dividerPaint);

        using var subtitlePaint = new SKPaint
        {
            Color = isJoin ? highlight : text,
            IsAntialias = true,
            TextSize = isJoin ? 36 : 28,
            Typeface = isJoin
                ? boldTypeface ?? SKTypeface.Default
                : regularTypeface ?? SKTypeface.Default
        };
        canvas.DrawText(
            isJoin ? "JOIN THE GAME" : "PLAY  |  HOST  |  CREATE",
            572,
            492,
            subtitlePaint);

        using var domainPaint = new SKPaint
        {
            Color = muted,
            IsAntialias = true,
            TextSize = 24,
            Typeface = regularTypeface ?? SKTypeface.Default
        };
        canvas.DrawText("badwolf.buzz", 572, 548, domainPaint);
    }

    private static void DrawQuizDescriptionBranding(
        SKCanvas canvas,
        string title,
        string? description,
        double? averageRating,
        int ratingCount,
        SKColor text,
        SKColor muted,
        SKColor accentBright)
    {
        using var boldTypeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold);
        using var regularTypeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Normal);

        using var eyebrowPaint = new SKPaint
        {
            Color = accentBright,
            IsAntialias = true,
            TextSize = 28,
            Typeface = boldTypeface ?? SKTypeface.Default
        };
        canvas.DrawText("BAD WOLF QUIZ", 570, 92, eyebrowPaint);

        using var titlePaint = new SKPaint
        {
            Color = text,
            IsAntialias = true,
            TextSize = 52,
            Typeface = boldTypeface ?? SKTypeface.Default
        };
        var titleBottom = DrawWrappedText(
            canvas,
            string.IsNullOrWhiteSpace(title) ? "QUIZ" : title.Trim(),
            titlePaint,
            570,
            158,
            555,
            60,
            3);

        using var dividerPaint = new SKPaint
        {
            Color = accentBright,
            StrokeWidth = 4,
            IsAntialias = true
        };
        var dividerY = Math.Min(titleBottom + 16, 350);
        canvas.DrawLine(572, dividerY, 1095, dividerY, dividerPaint);

        if (!string.IsNullOrWhiteSpace(description))
        {
            using var descriptionPaint = new SKPaint
            {
                Color = muted,
                IsAntialias = true,
                TextSize = 27,
                Typeface = regularTypeface ?? SKTypeface.Default
            };
            DrawWrappedText(
                canvas,
                description.Trim(),
                descriptionPaint,
                572,
                dividerY + 46,
                530,
                36,
                3);
        }

        if (averageRating is { } rating && ratingCount > 0)
        {
            using var ratingPaint = new SKPaint
            {
                Color = accentBright,
                IsAntialias = true,
                TextSize = 27,
                Typeface = boldTypeface ?? SKTypeface.Default
            };
            canvas.DrawText(
                $"RATING {rating:0.0} / 5  ·  {ratingCount}",
                572,
                520,
                ratingPaint);
        }

        using var domainPaint = new SKPaint
        {
            Color = muted,
            IsAntialias = true,
            TextSize = 24,
            Typeface = regularTypeface ?? SKTypeface.Default
        };
        canvas.DrawText("badwolf.buzz", 572, 566, domainPaint);
    }

    private static float DrawWrappedText(
        SKCanvas canvas,
        string value,
        SKPaint paint,
        float x,
        float firstBaseline,
        float maxWidth,
        float lineHeight,
        int maxLines)
    {
        var words = value
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0)
        {
            return firstBaseline;
        }

        var lines = new List<string>();
        var current = string.Empty;
        var consumedWords = 0;

        foreach (var word in words)
        {
            var candidate = string.IsNullOrEmpty(current) ? word : $"{current} {word}";
            if (paint.MeasureText(candidate) <= maxWidth || string.IsNullOrEmpty(current))
            {
                current = candidate;
                consumedWords++;
                continue;
            }

            lines.Add(FitText(current, paint, maxWidth));
            if (lines.Count == maxLines)
            {
                break;
            }

            current = word;
            consumedWords++;
        }

        if (lines.Count < maxLines && !string.IsNullOrEmpty(current))
        {
            lines.Add(FitText(current, paint, maxWidth));
        }

        if (consumedWords < words.Length && lines.Count > 0)
        {
            lines[^1] = FitText(lines[^1] + "…", paint, maxWidth);
        }

        var baseline = firstBaseline;
        foreach (var line in lines.Take(maxLines))
        {
            canvas.DrawText(line, x, baseline, paint);
            baseline += lineHeight;
        }

        return baseline - lineHeight;
    }

    private static string FitText(string value, SKPaint paint, float maxWidth)
    {
        if (paint.MeasureText(value) <= maxWidth)
        {
            return value;
        }

        const string ellipsis = "…";
        var length = value.Length;
        while (length > 1 && paint.MeasureText(value[..length] + ellipsis) > maxWidth)
        {
            length--;
        }

        return value[..Math.Max(1, length)] + ellipsis;
    }

    private static SKColor ParseColor(string value)
    {
        var hex = value.Trim().TrimStart('#');
        if (hex.Length != 6)
        {
            throw new ArgumentException("Expected a six-digit hexadecimal color.", nameof(value));
        }

        return new SKColor(
            Convert.ToByte(hex[..2], 16),
            Convert.ToByte(hex.Substring(2, 2), 16),
            Convert.ToByte(hex.Substring(4, 2), 16));
    }

    private static SKColor WithAlpha(SKColor color, byte alpha) =>
        new(color.Red, color.Green, color.Blue, alpha);
}
