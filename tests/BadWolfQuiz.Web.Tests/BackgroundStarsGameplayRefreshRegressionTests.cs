namespace BadWolfQuiz.Web.Tests;

public sealed class BackgroundStarsGameplayRefreshRegressionTests
{
    [Fact]
    public void Host_gameplay_updates_rehost_the_existing_starfield()
    {
        var tagHelper = ReadWebFile("TagHelpers", "BackgroundStarsTagHelper.cs");
        var syncScript = ReadWebFile(
            "wwwroot",
            "js",
            "background-stars-gameplay-sync.js");
        var lobby = ReadWebFile("Pages", "Admin", "Games", "Lobby.cshtml");
        var siteScript = ReadWebFile("wwwroot", "js", "site.js");

        Assert.Contains(
            "/js/background-stars-gameplay-sync.js?v=1",
            tagHelper,
            StringComparison.Ordinal);
        Assert.Contains(
            "badwolf:host-gameplay-updated",
            syncScript,
            StringComparison.Ordinal);
        Assert.Contains(
            "window.BadWolfStarfield?.refresh?.();",
            syncScript,
            StringComparison.Ordinal);
        Assert.Contains(
            "window.requestAnimationFrame",
            syncScript,
            StringComparison.Ordinal);
        Assert.Contains(
            "badwolf:host-gameplay-updated",
            lobby,
            StringComparison.Ordinal);
        Assert.Contains(
            "badwolf:host-gameplay-updated",
            siteScript,
            StringComparison.Ordinal);
    }

    private static string ReadWebFile(params string[] relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                new[] { directory.FullName, "src", "BadWolfQuiz.Web" }
                    .Concat(relativePath)
                    .ToArray());
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate BadWolfQuiz.Web/{string.Join('/', relativePath)}.");
    }
}
