namespace BadWolfQuiz.Web.Services;

using BadWolfQuiz.Game.Runtime;
using System.Text.RegularExpressions;

public static class SiteThemeCatalog
{
    public const string DefaultId = "classic-wolf";

    private static readonly HashSet<string> ValidIds = new(
        StringComparer.OrdinalIgnoreCase)
    {
        DefaultId,
        "crimson-night",
        "arctic-neon",
        "emerald-stage",
        "royal-violet",
        "sunset-orange",
        "daylight",
        "warm-parchment",
        "mint-fog",
        "ukrainian-sky",
        "italian-spirit",
        "custom"
    };

    public static bool IsValid(string? themeId) =>
        !string.IsNullOrWhiteSpace(themeId) && ValidIds.Contains(themeId);

    public static string Normalize(string? themeId) =>
        IsValid(themeId) ? themeId!.ToLowerInvariant() : DefaultId;

    public static bool AreValid(SiteThemeColors? colors) =>
        colors is not null &&
        IsColor(colors.Background) &&
        IsColor(colors.Panel) &&
        IsColor(colors.PanelSecondary) &&
        IsColor(colors.Text) &&
        IsColor(colors.MutedText) &&
        IsColor(colors.Accent) &&
        IsColor(colors.AccentBright) &&
        IsColor(colors.Highlight);

    public static SiteThemeColors Normalize(SiteThemeColors? colors) =>
        AreValid(colors) ? colors! : SiteThemeColors.Default;

    public static string BuildCssVariables(SiteThemeColors? colors)
    {
        var value = Normalize(colors);
        return string.Join(';',
            $"--bg:{value.Background}",
            $"--panel:{value.Panel}",
            $"--panel-2:{value.PanelSecondary}",
            $"--text:{value.Text}",
            $"--muted:{value.MutedText}",
            $"--red:{value.Accent}",
            $"--red-bright:{value.AccentBright}",
            $"--gold:{value.Highlight}");
    }

    private static bool IsColor(string? color) =>
        color is not null && Regex.IsMatch(color, "^#[0-9a-fA-F]{6}$");
}
