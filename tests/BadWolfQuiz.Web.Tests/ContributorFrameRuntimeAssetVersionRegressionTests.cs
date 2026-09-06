namespace BadWolfQuiz.Web.Tests;

public sealed class ContributorFrameRuntimeAssetVersionRegressionTests
{
    [Fact]
    public void Player_and_host_game_pages_load_fingerprinted_frame_runtime_assets_last()
    {
        var helper = File.ReadAllText(FindWebFile(
            "TagHelpers",
            "ContributorFrameRuntimeAssetsTagHelper.cs"));
        var imports = File.ReadAllText(FindWebFile(
            "Pages",
            "_ViewImports.cshtml"));
        var insetScript = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "contributor-frame-insets.js"));
        var styles = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "contributor-frames.css"));

        Assert.Contains("public override int Order => 2000;", helper);
        Assert.Contains("/Admin/Games/Lobby", helper);
        Assert.Contains("/Player/Lobby", helper);
        Assert.Contains("AddFileVersionToPath", helper);
        Assert.Contains("/css/contributor-frames.css", helper);
        Assert.Contains("/js/contributor-frame-insets.js", helper);
        Assert.Contains(
            "ContributorFrameRuntimeAssetsTagHelper",
            imports,
            StringComparison.Ordinal);

        Assert.Contains("\"src\"", insetScript, StringComparison.Ordinal);
        Assert.Contains(
            "contributor-frame-clipped-media",
            insetScript,
            StringComparison.Ordinal);
        Assert.Contains(
            "clip-path: circle(var(--contributor-frame-clip-radius, 0px) at 50% 50%) !important;",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            ".player-avatar-control.contributor-frame-owner[data-avatar-frame] > .player-avatar-current",
            styles,
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
