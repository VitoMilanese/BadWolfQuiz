namespace BadWolfQuiz.Web.Tests;

public sealed class ContributorFrameRuntimeMediaRegressionTests
{
    [Fact]
    public void Runtime_media_uses_the_same_filename_inset_contract_as_frame_preview()
    {
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "contributor-frame-runtime-media.js"));
        var styles = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "contributor-frame-runtime-media.css"));
        var helper = File.ReadAllText(FindWebFile(
            "TagHelpers",
            "ContributorFrameRuntimeMediaAssetsTagHelper.cs"));

        Assert.Contains(
            "body.dataset.contributorFrameNativeInsets",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "nativeInsetPixels * renderedFrameSize / naturalFrameSize",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "media.classList.add(\"contributor-frame-runtime-media\")",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "badwolf:host-gameplay-updated",
            script,
            StringComparison.Ordinal);

        Assert.Contains(
            "padding: var(--contributor-frame-runtime-inset, 0px) !important;",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            "border-radius: 50% !important;",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            ".player-avatar-control.contributor-frame-owner[data-avatar-frame] > .player-avatar-current",
            styles,
            StringComparison.Ordinal);
        Assert.Contains("background: transparent !important;", styles, StringComparison.Ordinal);

        Assert.Contains(
            "/css/contributor-frame-runtime-media.css?v=1",
            helper,
            StringComparison.Ordinal);
        Assert.Contains(
            "/js/contributor-frame-runtime-media.js?v=1",
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
