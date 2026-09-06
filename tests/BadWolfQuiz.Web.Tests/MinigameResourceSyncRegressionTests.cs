namespace BadWolfQuiz.Web.Tests;

public sealed class MinigameResourceSyncRegressionTests
{
    [Fact]
    public void Masterhost_editor_exposes_resource_sync_and_paged_cleanup_dialog()
    {
        var page = ReadWebFile("Pages", "Admin", "MinigameEditor.cshtml");
        var endpoint = ReadWebFile("Pages", "Admin", "MinigameResourceSync.cshtml.cs");
        var script = ReadWebFile("wwwroot", "js", "minigame-resource-sync.js");
        var service = ReadWebFile("Services", "MinigameResourceSyncService.cs");

        Assert.Contains("data-minigame-resource-sync-start", page);
        Assert.Contains("data-minigame-resource-cleanup-dialog", page);
        Assert.Contains("data-minigame-resource-cleanup-keep", page);
        Assert.Contains("data-minigame-resource-cleanup-delete", page);
        Assert.Contains("MinigameResourceSync", page);
        Assert.Contains("minigame-resource-sync.js", page);
        Assert.Contains("[Authorize(Policy = \"MasterHost\")]", endpoint);
        Assert.Contains("OnPostSynchronizeAsync", endpoint);
        Assert.Contains("OnPostDeleteMissingAsync", endpoint);
        Assert.Contains("const pageSize = 10", script);
        Assert.Contains("selectedIds", script);
        Assert.Contains("window.confirm", script);
        Assert.Contains("BadWolfBusy", script);
        Assert.Contains("DeleteMissingGamesAsync", service);
        Assert.Contains("!resourceNamesResult.Names.Contains(game.Name)", service);
        Assert.Contains("MinigameAnswerFileParser.Parse", service);
    }

    private static string ReadWebFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                new[] { directory.FullName, "src", "BadWolfQuiz.Web" }
                    .Concat(parts)
                    .ToArray());
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate BadWolfQuiz.Web file: {Path.Combine(parts)}");
    }
}
