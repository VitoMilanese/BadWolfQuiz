namespace BadWolfQuiz.Web.Tests;

public sealed class HeaderGameControlsTests
{
    [Fact]
    public void Gameplay_controls_are_moved_into_the_header_without_duplication()
    {
        var lobby = File.ReadAllText(FindWebFile(
            "Pages",
            "Admin",
            "Games",
            "Lobby.cshtml"));
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "discord-media-mute.js"));

        Assert.Equal(1, CountOccurrences(lobby, "class=\"game-side-controls\""));
        Assert.Contains("const moveGameControlsToHeader = () =>", script);
        Assert.Contains("header.insertBefore(controls, discordSettings ?? null);", script);
        Assert.Contains("controls.dataset.headerGameControls = \"\";", script);
    }

    [Fact]
    public void Header_controls_preserve_presentation_visibility_rules()
    {
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "discord-media-mute.js"));

        Assert.Contains("host-gameplay-presentation-mode", script);
        Assert.Contains("attributeFilter: [\"class\"]", script);
        Assert.Contains("? \"none\"", script);
        Assert.Contains(": \"flex\";", script);
    }

    private static int CountOccurrences(string value, string fragment)
    {
        var count = 0;
        var offset = 0;
        while ((offset = value.IndexOf(fragment, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += fragment.Length;
        }

        return count;
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
