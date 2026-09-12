namespace BadWolfQuiz.Web.Tests;

public sealed class HostCardResizePersistenceRegressionTests
{
    [Fact]
    public void Host_card_resize_keeps_live_dimensions_authoritative_during_reentrant_restores()
    {
        var script = ReadWebFile(
            "wwwroot",
            "js",
            "host-card-resize-persistence.js");
        var assets = ReadWebFile(
            "TagHelpers",
            "HostCardResizeRuntimeAssetsTagHelper.cs");
        var viewImports = ReadWebFile(
            "Pages",
            "_ViewImports.cshtml");

        Assert.Contains(
            "document.addEventListener(\"pointermove\"",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "localStorage.setItem(storageKey, JSON.stringify(liveSize));",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "window.addEventListener(\"resize\"",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "requestAnimationFrame(reapplyLiveSize);",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "badwolf:host-gameplay-updated",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "requestAnimationFrame(() => requestAnimationFrame(reapplyLiveSize));",
            script,
            StringComparison.Ordinal);

        Assert.Contains(
            "\"/Admin/Games/Lobby\"",
            assets,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"/js/host-card-resize-persistence.js\"",
            assets,
            StringComparison.Ordinal);
        Assert.Contains(
            "AddFileVersionToPath",
            assets,
            StringComparison.Ordinal);
        Assert.Contains(
            "@addTagHelper BadWolfQuiz.Web.TagHelpers.HostCardResizeRuntimeAssetsTagHelper, BadWolfQuiz.Web",
            viewImports,
            StringComparison.Ordinal);
    }

    private static string ReadWebFile(params string[] parts)
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
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate BadWolfQuiz.Web file: {string.Join('/', parts)}");
    }
}
