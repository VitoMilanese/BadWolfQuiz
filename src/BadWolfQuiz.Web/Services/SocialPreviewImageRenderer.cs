using SkiaSharp;

namespace BadWolfQuiz.Web.Services;

public static class SocialPreviewImageRenderer
{
    public const int Width = 1200;
    public const int Height = 630;

    public static byte[] Render(string? variant)
    {
        var isJoin = string.Equals(variant, "join", StringComparison.OrdinalIgnoreCase);

        using var bitmap = new SKBitmap(new SKImageInfo(
            Width,
            Height,
            SKColorType.Bgra8888,
            SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);

        using (var backgroundPaint = new SKPaint { IsAntialias = true })
        using (var backgroundShader = SKShader.CreateLinearGradient(
                   new SKPoint(0, 0),
                   new SKPoint(Width, Height),
                   new[]
                   {
                       new SKColor(10, 10, 14),
                       new SKColor(28, 7, 10),
                       new SKColor(8, 8, 11)
                   },
                   new[] { 0f, 0.55f, 1f },
                   SKShaderTileMode.Clamp))
        {
            backgroundPaint.Shader = backgroundShader;
            canvas.DrawRect(0, 0, Width, Height, backgroundPaint);
        }

        using (var glowPaint = new SKPaint
               {
                   Color = new SKColor(185, 22, 35, 75),
                   IsAntialias = true
               })
        {
            canvas.DrawCircle(275, 320, 260, glowPaint);
        }

        using (var accentPaint = new SKPaint
               {
                   Color = new SKColor(210, 31, 45),
                   IsAntialias = true
               })
        using (var accentPath = new SKPath())
        {
            accentPath.MoveTo(0, 0);
            accentPath.LineTo(155, 0);
            accentPath.LineTo(62, Height);
            accentPath.LineTo(0, Height);
            accentPath.Close();
            canvas.DrawPath(accentPath, accentPaint);
        }

        using (var wolfPaint = new SKPaint
               {
                   Color = new SKColor(236, 236, 242),
                   IsAntialias = true,
                   Style = SKPaintStyle.Fill
               })
        using (var wolfPath = new SKPath())
        {
            wolfPath.MoveTo(145, 410);
            wolfPath.LineTo(105, 205);
            wolfPath.LineTo(220, 258);
            wolfPath.LineTo(292, 175);
            wolfPath.LineTo(365, 258);
            wolfPath.LineTo(480, 205);
            wolfPath.LineTo(438, 410);
            wolfPath.QuadTo(292, 500, 145, 410);
            wolfPath.Close();
            canvas.DrawPath(wolfPath, wolfPaint);
        }

        using (var facePaint = new SKPaint
               {
                   Color = new SKColor(20, 20, 26),
                   IsAntialias = true,
                   Style = SKPaintStyle.Fill
               })
        {
            canvas.DrawCircle(240, 337, 16, facePaint);
            canvas.DrawCircle(345, 337, 16, facePaint);

            using var muzzlePath = new SKPath();
            muzzlePath.MoveTo(270, 388);
            muzzlePath.LineTo(315, 388);
            muzzlePath.LineTo(292, 420);
            muzzlePath.Close();
            canvas.DrawPath(muzzlePath, facePaint);
        }

        using (var eyeAccentPaint = new SKPaint
               {
                   Color = new SKColor(210, 31, 45),
                   IsAntialias = true
               })
        {
            canvas.DrawCircle(240, 337, 7, eyeAccentPaint);
            canvas.DrawCircle(345, 337, 7, eyeAccentPaint);
        }

        using var boldTypeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold);
        using var regularTypeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Normal);

        using (var brandPaint = new SKPaint
               {
                   Color = SKColors.White,
                   IsAntialias = true,
                   TextSize = 78,
                   Typeface = boldTypeface ?? SKTypeface.Default
               })
        {
            canvas.DrawText("BAD WOLF", 520, 270, brandPaint);
        }

        using (var quizPaint = new SKPaint
               {
                   Color = new SKColor(224, 35, 50),
                   IsAntialias = true,
                   TextSize = 118,
                   Typeface = boldTypeface ?? SKTypeface.Default
               })
        {
            canvas.DrawText("QUIZ", 520, 390, quizPaint);
        }

        using (var dividerPaint = new SKPaint
               {
                   Color = new SKColor(224, 35, 50),
                   StrokeWidth = 4,
                   IsAntialias = true
               })
        {
            canvas.DrawLine(522, 430, 1010, 430, dividerPaint);
        }

        using (var subtitlePaint = new SKPaint
               {
                   Color = new SKColor(220, 220, 225),
                   IsAntialias = true,
                   TextSize = isJoin ? 36 : 28,
                   Typeface = isJoin
                       ? boldTypeface ?? SKTypeface.Default
                       : regularTypeface ?? SKTypeface.Default
               })
        {
            canvas.DrawText(
                isJoin ? "JOIN THE GAME" : "PLAY  |  HOST  |  CREATE",
                522,
                492,
                subtitlePaint);
        }

        using (var domainPaint = new SKPaint
               {
                   Color = new SKColor(150, 150, 160),
                   IsAntialias = true,
                   TextSize = 24,
                   Typeface = regularTypeface ?? SKTypeface.Default
               })
        {
            canvas.DrawText("badwolf.buzz", 522, 548, domainPaint);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 92);
        return encoded.ToArray();
    }
}
