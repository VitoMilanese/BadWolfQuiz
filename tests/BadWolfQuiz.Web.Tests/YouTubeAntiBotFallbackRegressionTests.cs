namespace BadWolfQuiz.Web.Tests;

public sealed class YouTubeAntiBotFallbackRegressionTests
{
    [Fact]
    public void Shared_layout_exposes_the_debug_simulation_flag_and_loads_fallback_manager()
    {
        var appSettings = File.ReadAllText(FindWebFile("appsettings.json"));
        var layout = File.ReadAllText(FindWebFile("Pages", "Shared", "_Layout.cshtml"));

        Assert.Contains(
            "\"SimulateYouTubeAntiBot\": false",
            appSettings,
            StringComparison.Ordinal);
        Assert.Contains(
            "Configuration.GetValue<bool>(\"SimulateYouTubeAntiBot\")",
            layout,
            StringComparison.Ordinal);
        Assert.Contains(
            "~/js/youtube-antibot-fallback.js",
            layout,
            StringComparison.Ordinal);
        Assert.Contains(
            "data-simulate-youtube-anti-bot",
            layout,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Blocked_playback_uses_a_dedicated_state_instead_of_the_preplay_placeholder()
    {
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "youtube-antibot-fallback.js"));
        var css = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "youtube-placeholder.css"));

        Assert.Contains(
            "const createBlockedSurface = reason =>",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "surface.className = \"youtube-antibot-fallback\";",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "youtube-antibot-fallback-title",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "youtube-antibot-fallback-message",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "youtube-antibot-fallback-retry",
            script,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "createFallbackPlaceholder",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            ".youtube-antibot-fallback {",
            css,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            ".youtube-placeholder.youtube-antibot-fallback-expanded",
            css,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Blocked_playback_is_paused_before_the_iframe_is_hidden_or_replaced()
    {
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "youtube-antibot-fallback.js"));

        Assert.Contains(
            "const pauseBlockedPlayback = iframe =>",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "player.pauseVideo();",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "func: \"pauseVideo\"",
            script,
            StringComparison.Ordinal);

        var pauseCall = script.IndexOf(
            "pauseBlockedPlayback(iframe);",
            StringComparison.Ordinal);
        var managedFallback = script.IndexOf(
            "presentManagedFallback(iframe, surface, retryButton)",
            StringComparison.Ordinal);
        var inlineFallback = script.IndexOf(
            "presentInlineFallback(iframe, surface, retryButton);",
            StringComparison.Ordinal);

        Assert.True(pauseCall >= 0);
        Assert.True(managedFallback > pauseCall);
        Assert.True(inlineFallback > pauseCall);
    }

    [Fact]
    public void Managed_players_keep_fullscreen_open_while_the_blocked_state_is_visible()
    {
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "youtube-antibot-fallback.js"));
        var css = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "youtube-placeholder.css"));

        Assert.Contains(
            "const presentManagedFallback = (iframe, surface, retryButton) =>",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "document.body.classList.contains(\"youtube-auto-expanded-open\")",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "iframe.style.visibility = \"hidden\";",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "surface.classList.add(\"youtube-antibot-fallback-expanded\")",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "document.body.appendChild(surface);",
            script,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "window.BadWolfYouTubeAutoExpand.stopAll();\n            return;",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            ".youtube-antibot-fallback-expanded {",
            css,
            StringComparison.Ordinal);
        Assert.Contains("position: fixed;", css, StringComparison.Ordinal);
        Assert.Contains("width: 100vw;", css, StringComparison.Ordinal);
        Assert.Contains("height: 100vh;", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Managed_retry_returns_to_the_normal_youtube_launch_path()
    {
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "youtube-antibot-fallback.js"));

        Assert.Contains(
            "const retryManagedPlayback = iframe =>",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "window.BadWolfYouTubeAutoExpand.stopAll();",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "restoredPlaceholder.click();",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "retryButton.addEventListener",
            script,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Inline_players_show_the_same_blocked_state_and_can_retry()
    {
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "youtube-antibot-fallback.js"));

        Assert.Contains(
            "const presentInlineFallback = (iframe, surface, retryButton) =>",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "iframe.replaceWith(surface);",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "const retryInlinePlayback = (iframe, surface, source) =>",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "surface.replaceWith(iframe);",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "boundFrames.delete(iframe);",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "startPlaybackWatchdog(iframe);",
            script,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Inline_answer_key_style_players_get_their_own_health_events()
    {
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "youtube-antibot-fallback.js"));

        Assert.Contains(
            "const initializeInlineFrame = iframe =>",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "new window.YT.Player(iframe",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "onError: () => markPlaybackBlocked(iframe, \"player-error\")",
            script,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Debug_simulation_and_real_failures_share_the_same_blocked_state_path()
    {
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "youtube-antibot-fallback.js"));

        Assert.Contains(
            "simulatedBlockDelayMs = 900",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "simulateAntiBot ? \"simulated\" : \"startup-timeout\"",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "markPlaybackBlocked(iframe, \"player-error\")",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "const { surface, retryButton } = createBlockedSurface(reason);",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "badwolf:youtube-blocked",
            script,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Working_player_state_cancels_the_real_startup_watchdog()
    {
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "youtube-antibot-fallback.js"));

        Assert.Contains("event === \"infoDelivery\"", script, StringComparison.Ordinal);
        Assert.Contains("event === \"onStateChange\"", script, StringComparison.Ordinal);
        Assert.Contains("playerState === -1", script, StringComparison.Ordinal);
        Assert.Contains("markPlaybackHealthy(iframe);", script, StringComparison.Ordinal);
        Assert.Contains("clearPlaybackWatchdog(iframe);", script, StringComparison.Ordinal);
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
