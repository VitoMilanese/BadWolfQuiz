using BadWolfQuiz.Game.Runtime;

namespace BadWolfQuiz.Web.Services;

public readonly record struct SocialPreviewThemePalette(
    string Background,
    string Panel,
    string PanelSecondary,
    string Text,
    string MutedText,
    string Accent,
    string AccentBright,
    string Highlight)
{
    public static SocialPreviewThemePalette Resolve(
        string? themeId,
        SiteThemeColors? customThemeColors = null)
    {
        var normalized = SiteThemeCatalog.Normalize(themeId);

        if (string.Equals(normalized, "custom", StringComparison.Ordinal))
        {
            var custom = SiteThemeCatalog.Normalize(customThemeColors);
            return FromSiteThemeColors(custom);
        }

        return normalized switch
        {
            "crimson-night" => new(
                "#09070b", "#151118", "#211820", "#fff7f7",
                "#bca8ad", "#b91c35", "#ff4964", "#ffbf69"),
            "arctic-neon" => new(
                "#050b14", "#0c1725", "#12263a", "#eefaff",
                "#91a9bd", "#087f9c", "#35d9ff", "#a78bfa"),
            "emerald-stage" => new(
                "#07100d", "#0d1b16", "#142820", "#f3fbf5",
                "#9bb5a6", "#0d7b58", "#35d399", "#eabf55"),
            "royal-violet" => new(
                "#0d0815", "#191126", "#28183b", "#fbf7ff",
                "#b7a4c9", "#7c3aed", "#c084fc", "#f472b6"),
            "sunset-orange" => new(
                "#120b06", "#21140b", "#352012", "#fff8f1",
                "#cbb19a", "#c6530a", "#ff8a2a", "#ffd166"),
            "daylight" => new(
                "#eef2f7", "#ffffff", "#dfe7f0", "#182230",
                "#5f6f82", "#315f9d", "#447dc6", "#b56b0a"),
            "warm-parchment" => new(
                "#e8ddc8", "#f5ecd9", "#ddd0b8", "#392f27",
                "#75675a", "#a64b2a", "#cf6841", "#8d681e"),
            "mint-fog" => new(
                "#dce8e3", "#edf5f1", "#c9dbd3", "#20342d",
                "#5b746a", "#18705c", "#2b9479", "#8a6420"),
            "ukrainian-sky" => new(
                "#061529", "#0b2545", "#123a66", "#f7fbff",
                "#a9c4dd", "#0057b8", "#2f80ed", "#ffd700"),
            "italian-spirit" => new(
                "#0b1511", "#12221a", "#1b3428", "#fffaf7",
                "#b7c5bd", "#008c45", "#20b86a", "#f05a5a"),
            "pride-spectrum" => new(
                "#100b18", "#1a1325", "#291b38", "#fff8ff",
                "#c4accb", "#c22d8f", "#ff69c7", "#ffd166"),
            "retro-arcade" => new(
                "#07081a", "#10132b", "#1a2040", "#f8f7ff",
                "#a9add1", "#be1f91", "#ff43d1", "#b8f34a"),
            _ => new(
                "#070709", "#111116", "#18181f", "#f4f4f5",
                "#a3a3ad", "#c1121f", "#ef233c", "#f2b134")
        };
    }

    private static SocialPreviewThemePalette FromSiteThemeColors(
        SiteThemeColors colors) =>
        new(
            colors.Background,
            colors.Panel,
            colors.PanelSecondary,
            colors.Text,
            colors.MutedText,
            colors.Accent,
            colors.AccentBright,
            colors.Highlight);
}
