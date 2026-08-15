namespace BadWolfQuiz.Web.Tests;

public sealed class YouTubePlaceholderRegressionTests
{
    [Fact]
    public void Server_rendered_youtube_blocks_use_local_placeholder_before_launch()
    {
        var gameplay = File.ReadAllText(FindWebFile(
            "Pages",
            "Admin",
            "Games",
            "_GameContentPreview.cshtml"));
        var answerKey = File.ReadAllText(FindWebFile(
            "Pages",
            "Admin",
            "Games",
            "AnswerKey.cshtml"));

        Assert.Contains("data-youtube-placeholder", gameplay, StringComparison.Ordinal);
        Assert.Contains("youtube-placeholder.svg", gameplay, StringComparison.Ordinal);
        Assert.Contains(
            "data-youtube-frame-class=\"game-content-video youtube-auto-expand\"",
            gameplay,
            StringComparison.Ordinal);
        Assert.Contains(
            "data-youtube-allow-fullscreen=\"false\"",
            gameplay,
            StringComparison.Ordinal);
        Assert.Contains("&fs=0", gameplay, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "<iframe class=\"game-content-video youtube-auto-expand\"",
            gameplay,
            StringComparison.Ordinal);

        Assert.Contains("data-youtube-placeholder", answerKey, StringComparison.Ordinal);
        Assert.Contains("youtube-placeholder.svg", answerKey, StringComparison.Ordinal);
        Assert.Contains(
            "data-youtube-frame-class=\"game-content-video\"",
            answerKey,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "<iframe class=\"game-content-video\"",
            answerKey,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Editor_preview_iframes_are_hidden_and_replaced_by_shared_placeholder()
    {
        var questionEditor = File.ReadAllText(FindWebFile(
            "Pages",
            "Admin",
            "Quizzes",
            "QuestionEditor.cshtml"));
        var finalEditor = File.ReadAllText(FindWebFile(
            "Pages",
            "Admin",
            "Quizzes",
            "FinalQuestionEditor.cshtml"));
        var previewModal = File.ReadAllText(FindWebFile(
            "Pages",
            "Admin",
            "Quizzes",
            "Shared",
            "_QuestionPreviewModal.cshtml"));
        var css = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "youtube-placeholder.css"));
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "youtube-auto-expand.js"));

        Assert.Contains(
            "iframe.className = \"youtube-auto-expand\";",
            questionEditor,
            StringComparison.Ordinal);
        Assert.Contains(
            "iframe.className = \"youtube-auto-expand\";",
            finalEditor,
            StringComparison.Ordinal);
        Assert.Contains(
            "youtube-placeholder.css",
            previewModal,
            StringComparison.Ordinal);
        Assert.Contains(
            "iframe.youtube-auto-expand:not([data-youtube-launched])",
            css,
            StringComparison.Ordinal);
        Assert.Contains(
            "replaceSourceFrameWithPlaceholder",
            script,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Placeholder_click_creates_autoplay_iframe_and_preserves_auto_expand_behavior()
    {
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "youtube-auto-expand.js"));

        Assert.Contains(
            "url.searchParams.set(\"autoplay\", \"1\");",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "iframe.dataset.youtubeLaunched = \"true\";",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "placeholder.replaceWith(iframe);",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "event.target.playVideo();",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "iframe.youtube-auto-expand[data-youtube-launched]",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "onStateChange: event => handleStateChange(iframe, event)",
            script,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Placeholder_layout_and_styles_remain_available_after_ajax_gameplay_refresh()
    {
        var css = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "youtube-placeholder.css"));
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "youtube-auto-expand.js"));

        Assert.Contains(
            ".game-content-block:has(> .youtube-placeholder)",
            css,
            StringComparison.Ordinal);
        Assert.Contains("max-width: 960px;", css, StringComparison.Ordinal);
        Assert.Contains(
            "placeholderStylesheetUrl",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "ensurePlaceholderStylesheet();",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "document.head.appendChild(stylesheet);",
            script,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Managed_launch_expands_immediately_and_pauses_other_media_before_api_state_events()
    {
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "youtube-auto-expand.js"));
        var launchStart = script.IndexOf(
            "const launchPlaceholder =",
            StringComparison.Ordinal);
        var launchEnd = script.IndexOf(
            "const bindPlaceholder =",
            launchStart,
            StringComparison.Ordinal);

        Assert.True(launchStart >= 0);
        Assert.True(launchEnd > launchStart);

        var launch = script[launchStart..launchEnd];
        Assert.Contains(
            "const managedFullscreen = iframe.classList.contains(\"youtube-auto-expand\");",
            launch,
            StringComparison.Ordinal);
        Assert.Contains("pauseNativeMedia(null);", launch, StringComparison.Ordinal);
        Assert.Contains("pauseYouTubeFrames(iframe);", launch, StringComparison.Ordinal);
        Assert.Contains("expandVideo(iframe);", launch, StringComparison.Ordinal);

        var expandIndex = launch.IndexOf("expandVideo(iframe);", StringComparison.Ordinal);
        var bindIndex = launch.IndexOf("bindFrame(iframe);", StringComparison.Ordinal);
        Assert.True(expandIndex >= 0 && bindIndex > expandIndex);
    }

    [Fact]
    public void Managed_fullscreen_masks_youtube_metadata_and_disables_native_fullscreen()
    {
        var gameplay = File.ReadAllText(FindWebFile(
            "Pages",
            "Admin",
            "Games",
            "_GameContentPreview.cshtml"));
        var css = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "youtube-placeholder.css"));
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "youtube-auto-expand.js"));

        Assert.Contains("&fs=0", gameplay, StringComparison.Ordinal);
        Assert.Contains(
            "data-youtube-allow-fullscreen=\"false\"",
            gameplay,
            StringComparison.Ordinal);
        Assert.Contains(
            "url.searchParams.set(\"fs\", \"0\");",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "iframe.allowFullscreen = managedFullscreen",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "body.youtube-auto-expanded-open::before",
            css,
            StringComparison.Ordinal);
        Assert.Contains("height: max(132px, 15vh);", css, StringComparison.Ordinal);
        Assert.Contains("z-index: 10001;", css, StringComparison.Ordinal);
        Assert.Contains(
            ".youtube-auto-expand-close",
            css,
            StringComparison.Ordinal);
        Assert.Contains("z-index: 10002 !important;", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Placeholder_uses_replaceable_local_asset_and_visible_play_affordance()
    {
        var assetPath = FindWebFile(
            "wwwroot",
            "images",
            "youtube-placeholder.svg");
        var asset = File.ReadAllText(assetPath);
        var css = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "youtube-placeholder.css"));
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "youtube-auto-expand.js"));

        Assert.Contains("<svg", asset, StringComparison.Ordinal);
        Assert.DoesNotContain("youtube.com", asset, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(".youtube-placeholder-play", css, StringComparison.Ordinal);
        Assert.Contains(
            "new URL(\"../images/youtube-placeholder.svg\", scriptUrl)",
            script,
            StringComparison.Ordinal);
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
