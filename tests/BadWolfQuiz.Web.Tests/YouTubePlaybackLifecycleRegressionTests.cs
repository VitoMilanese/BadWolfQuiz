namespace BadWolfQuiz.Web.Tests;

public sealed class YouTubePlaybackLifecycleRegressionTests
{
    [Fact]
    public void Closing_managed_playback_restores_placeholder_instead_of_leaving_iframe()
    {
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "youtube-auto-expand.js"));

        Assert.Contains(
            "closeButton.addEventListener(\"click\", () => restorePlaceholder(iframe));",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "restorePlaceholder(expandedIframe);",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "iframe.dataset.youtubeEmbedUrl = embedUrl;",
            script,
            StringComparison.Ordinal);

        var restoreStart = script.IndexOf(
            "function restorePlaceholder(iframe)",
            StringComparison.Ordinal);
        var restoreEnd = script.IndexOf(
            "const buildLaunchUrl =",
            restoreStart,
            StringComparison.Ordinal);

        Assert.True(restoreStart >= 0);
        Assert.True(restoreEnd > restoreStart);

        var restore = script[restoreStart..restoreEnd];
        Assert.Contains("clearExpandedPresentation();", restore, StringComparison.Ordinal);
        Assert.Contains("endTimedPlayback(iframe);", restore, StringComparison.Ordinal);
        Assert.Contains("iframe.replaceWith(placeholder);", restore, StringComparison.Ordinal);
        Assert.Contains("bindPlaceholder(placeholder);", restore, StringComparison.Ordinal);
    }

    [Fact]
    public void Removing_playing_iframe_during_gameplay_refresh_does_not_resume_timer()
    {
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "youtube-auto-expand.js"));

        var abandonStart = script.IndexOf(
            "const abandonTimedPlayback =",
            StringComparison.Ordinal);
        var abandonEnd = script.IndexOf(
            "const clearExpandedPresentation =",
            abandonStart,
            StringComparison.Ordinal);

        Assert.True(abandonStart >= 0);
        Assert.True(abandonEnd > abandonStart);

        var abandon = script[abandonStart..abandonEnd];
        Assert.Contains("timerPlaybackOwner = null;", abandon, StringComparison.Ordinal);
        Assert.Contains("shouldResumeTimer = false;", abandon, StringComparison.Ordinal);
        Assert.DoesNotContain("resumePausedTimer();", abandon, StringComparison.Ordinal);

        var unbindStart = script.IndexOf(
            "const unbindMediaTree =",
            StringComparison.Ordinal);
        var unbindEnd = script.IndexOf(
            "window.BadWolfYouTubeAutoExpand =",
            unbindStart,
            StringComparison.Ordinal);

        Assert.True(unbindStart >= 0);
        Assert.True(unbindEnd > unbindStart);

        var unbind = script[unbindStart..unbindEnd];
        Assert.Contains("abandonTimedPlayback(iframe);", unbind, StringComparison.Ordinal);
        Assert.DoesNotContain("endTimedPlayback(iframe);", unbind, StringComparison.Ordinal);
    }

    private static string FindWebFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidateParts = new[]
            {
                directory.FullName,
                "src",
                "BadWolfQuiz.Web"
            }.Concat(parts).ToArray();
            var candidate = Path.Combine(candidateParts);
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate BadWolfQuiz.Web file: {Path.Combine(parts)}");
    }
}
