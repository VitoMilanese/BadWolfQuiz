namespace BadWolfQuiz.Web.Tests;

public sealed class BoardHeaderLayoutRegressionTests
{
    [Fact]
    public void Host_board_header_layout_synchronizes_immediately_before_follow_up_frame()
    {
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "board-header-layout.js"));

        Assert.Contains("grid.getClientRects().length === 0", script);
        Assert.Contains(
            "document.addEventListener(\n        \"badwolf:host-gameplay-updated\",\n        syncBeforePaint);",
            script);
        Assert.Contains(
            "const syncBeforePaint = () => {\n        syncHeaderHeights();",
            script);

        var immediateSync = script.IndexOf(
            "const syncBeforePaint = () => {\n        syncHeaderHeights();",
            StringComparison.Ordinal);
        var followUpFrame = script.IndexOf(
            "window.requestAnimationFrame(() => {",
            immediateSync,
            StringComparison.Ordinal);

        Assert.True(immediateSync >= 0);
        Assert.True(followUpFrame > immediateSync);
    }

    [Fact]
    public void Host_gameplay_bootstrap_loads_board_header_layout()
    {
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "gameplay-escape-shortcuts.js"));

        Assert.Contains(
            "loadSharedScript(\"/js/board-header-layout.js\");",
            script);
    }

    private static string FindWebFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var pathParts = new string[parts.Length + 3];
            pathParts[0] = directory.FullName;
            pathParts[1] = "src";
            pathParts[2] = "BadWolfQuiz.Web";
            Array.Copy(parts, 0, pathParts, 3, parts.Length);

            var candidate = Path.Combine(pathParts);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not find web file: {string.Join('/', parts)}.");
    }
}
