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
        Assert.Contains(
            "Відтворити іншим способом",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            ".youtube-antibot-fallback {",
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
    public void Alternative_playback_switches_from_privacy_enhanced_to_standard_youtube_embed()
    {
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "youtube-antibot-fallback.js"));

        Assert.Contains(
            "const buildAlternativeEmbedUrl = value =>",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "url.hostname = \"www.youtube.com\";",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "url.searchParams.set(\"origin\", window.location.origin);",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "const isAlternativePlayback = iframe =>",
            script,
            StringComparison.Ordinal);
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
        Assert.Contains(
            ".youtube-antibot-fallback-expanded {",
            css,
            StringComparison.Ordinal);
        Assert.Contains("position: fixed;", css, StringComparison.Ordinal);
        Assert.Contains("width: 100vw;", css, StringComparison.Ordinal);
        Assert.Contains("height: 100vh;", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Managed_retry_launches_the_alternative_embed_instead_of_repeating_primary_playback()
    {
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "youtube-antibot-fallback.js"));

        Assert.Contains(
            "const alternativeSource = buildAlternativeEmbedUrl(primarySource);",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "window.BadWolfYouTubeAutoExpand.stopAll();",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "restoredPlaceholder.dataset.youtubeEmbedUrl = alternativeSource;",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "restoredPlaceholder.click();",
            script,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Inline_players_use_the_same_alternative_embed_on_retry()
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
            "const alternativeSource = buildAlternativeEmbedUrl(source);",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "iframe.src = alternativeSource;",
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
    public void Debug_simulation_blocks_only_the_primary_embed_so_the_alternative_can_be_tested()
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
            "simulateAntiBot && !isAlternativePlayback(iframe)",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "simulateThisAttempt ? \"simulated\" : \"startup-timeout\"",
            script,
            StringComparison.Ordinal);
        Assert.Matches(
            "simulateThisAttempt\\r?\\n\\s*\\? simulatedBlockDelayMs\\r?\\n\\s*: playbackHealthTimeoutMs",
            script);
        Assert.Contains(
            "(simulateAntiBot && !isAlternativePlayback(iframe))",
            script,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Working_alternative_player_state_cancels_the_real_startup_watchdog()
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
