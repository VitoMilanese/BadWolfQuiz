namespace BadWolfQuiz.Web.Tests;

public sealed class HostAvatarFramePresentationRegressionTests
{
    [Fact]
    public void Host_frame_preview_live_settings_and_non_avatar_media_stay_consistent()
    {
        var styles = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "contributor-frames.css"));
        var helper = File.ReadAllText(FindWebFile(
            "TagHelpers",
            "ContributorGameSettingsTagHelper.cs"));
        var syncScript = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "host-frame-live-settings-sync.js"));

        Assert.Contains(
            ".host-settings-page .host-avatar-frame-preview > .host-avatar-preview",
            styles,
            StringComparison.Ordinal);
        Assert.Contains("border: 0;", styles, StringComparison.Ordinal);
        Assert.Contains("background: transparent;", styles, StringComparison.Ordinal);

        Assert.Contains(
            ".contributor-frame-owner[data-avatar-frame] > :is(",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(".player-card-avatar", styles, StringComparison.Ordinal);
        Assert.Contains(".host-card-media", styles, StringComparison.Ordinal);
        Assert.Contains("border-radius: 50% !important;", styles, StringComparison.Ordinal);
        Assert.Contains(
            "--contributor-frame-media-inset",
            styles,
            StringComparison.Ordinal);

        Assert.Contains(
            "/js/host-frame-live-settings-sync.js",
            helper,
            StringComparison.Ordinal);
        Assert.Contains("data-avatar-frame", syncScript, StringComparison.Ordinal);
        Assert.Contains(
            "HostAvatarFrameEnabled",
            syncScript,
            StringComparison.Ordinal);
        Assert.Contains(
            "[data-contributor-frame-id]",
            syncScript,
            StringComparison.Ordinal);
        Assert.Contains(
            "panel.dispatchEvent(new Event(\"change\", { bubbles: true }))",
            syncScript,
            StringComparison.Ordinal);
        Assert.Contains(
            "data.contributorFrameNativeInsets",
            syncScript.Replace("dataset", "data"),
            StringComparison.Ordinal);
        Assert.Contains(
            "--contributor-frame-media-inset",
            syncScript,
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
