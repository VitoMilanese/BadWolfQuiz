namespace BadWolfQuiz.Web.Tests;

public sealed class ContributorFrameRuntimeMediaRegressionTests
{
    [Fact]
    public void Runtime_media_uses_the_same_filename_inset_contract_as_frame_preview()
    {
        var insetScript = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "contributor-frame-insets.js"));
        var styles = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "contributor-frames.css"));
        var supportHelper = File.ReadAllText(FindWebFile(
            "TagHelpers",
            "ContributorSupportTagHelper.cs"));

        Assert.Contains(
            "body.dataset.contributorFrameNativeInsets",
            insetScript,
            StringComparison.Ordinal);
        Assert.Contains(
            "nativeInsetPixels * layoutFrameSize / naturalFrameSize",
            insetScript,
            StringComparison.Ordinal);
        Assert.Contains(
            "layoutFrameSize / 2 - scaledInsetPixels",
            insetScript,
            StringComparison.Ordinal);
        Assert.Contains(
            "media.classList.add(\"contributor-frame-clipped-media\")",
            insetScript,
            StringComparison.Ordinal);
        Assert.Contains(
            "badwolf:host-gameplay-updated",
            insetScript,
            StringComparison.Ordinal);

        Assert.Contains(
            ".contributor-frame-owner[data-avatar-frame] .contributor-frame-clipped-media",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            "clip-path: circle(var(--contributor-frame-clip-radius, 0px) at 50% 50%) !important;",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            ".player-avatar-control.contributor-frame-owner[data-avatar-frame] > .player-avatar-current",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            "background: transparent !important;",
            styles,
            StringComparison.Ordinal);

        Assert.Contains(
            "/js/contributor-frames.js",
            supportHelper,
            StringComparison.Ordinal);
        Assert.Contains(
            "/js/contributor-frame-insets.js",
            supportHelper,
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
