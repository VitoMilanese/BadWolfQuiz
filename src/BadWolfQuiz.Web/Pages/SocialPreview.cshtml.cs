using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BadWolfQuiz.Web.Pages;

public sealed class SocialPreviewModel : PageModel
{
    public IActionResult OnGet(
        string? variant = null,
        string? theme = null,
        string? bg = null,
        string? panel = null,
        string? panel2 = null,
        string? text = null,
        string? muted = null,
        string? accent = null,
        string? accentBright = null,
        string? highlight = null)
    {
        Response.Headers.CacheControl = "public, max-age=86400";

        var customColors = TryBuildCustomColors(
            theme,
            bg,
            panel,
            panel2,
            text,
            muted,
            accent,
            accentBright,
            highlight);

        return File(
            SocialPreviewImageRenderer.Render(variant, theme, customColors),
            "image/png");
    }

    private static SiteThemeColors? TryBuildCustomColors(
        string? theme,
        string? bg,
        string? panel,
        string? panel2,
        string? text,
        string? muted,
        string? accent,
        string? accentBright,
        string? highlight)
    {
        if (!string.Equals(
                SiteThemeCatalog.Normalize(theme),
                "custom",
                StringComparison.Ordinal))
        {
            return null;
        }

        var colors = new SiteThemeColors(
            bg ?? string.Empty,
            panel ?? string.Empty,
            panel2 ?? string.Empty,
            text ?? string.Empty,
            muted ?? string.Empty,
            accent ?? string.Empty,
            accentBright ?? string.Empty,
            highlight ?? string.Empty);

        return SiteThemeCatalog.AreValid(colors) ? colors : null;
    }
}
