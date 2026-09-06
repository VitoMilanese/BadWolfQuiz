namespace BadWolfQuiz.Web.Tests;

public sealed class ContributorFrameMediaParityRegressionTests
{
    [Fact]
    public void Runtime_frame_media_matches_the_player_frame_preview_geometry()
    {
        var styles = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "contributor-frame-media-parity.css"));
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "contributor-frame-media-parity.js"));
        var helper = File.ReadAllText(FindWebFile(
            "TagHelpers",
            "ContributorFrameRuntimeAssetsTagHelper.cs"));
        var previewStyles = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "contributor-player-frame-settings.css"));

        Assert.Contains(
            "nativeInset * renderedSize / naturalSize",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "contributor-frame-preview-parity-media",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "--contributor-frame-preview-parity-size",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            ".game-scoreboard .scoreboard-player:not(.host-card) > .player-card-avatar",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "badwolf:host-gameplay-updated",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "attributeFilter",
            script,
            StringComparison.Ordinal);
        Assert.Contains("\"src\"", script, StringComparison.Ordinal);

        Assert.Contains(
            "padding: var(--contributor-frame-preview-parity-inset, 0px) !important;",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            "border-radius: 50% !important;",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            "clip-path: none !important;",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            "background: transparent !important;",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            "width: var(--contributor-frame-preview-parity-size) !important;",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            "height: var(--contributor-frame-preview-parity-size) !important;",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            "justify-self: center !important;",
            styles,
            StringComparison.Ordinal);

        Assert.Contains(
            "padding: var(--player-avatar-frame-preview-inset);",
            previewStyles,
            StringComparison.Ordinal);
        Assert.Contains(
            "/css/contributor-frame-media-parity.css",
            helper,
            StringComparison.Ordinal);
        Assert.Contains(
            "/js/contributor-frame-media-parity.js",
            helper,
            StringComparison.Ordinal);
    }

    private static string FindWebFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                new[]
                {
                    directory.FullName,
                    "src",
                    "BadWolfQuiz.Web"
                }.Concat(parts).ToArray());

            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate BadWolfQuiz.Web file: {string.Join('/', parts)}");
    }
}
