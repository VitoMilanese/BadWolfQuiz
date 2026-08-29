using System.Text.Json;

namespace BadWolfQuiz.Web.Tests;

public sealed class MinigamesRegressionTests
{
    [Fact]
    public void Main_menu_and_runtime_expose_minigames()
    {
        var layout = ReadWebFile("Pages", "Shared", "_Layout.cshtml");
        var program = ReadWebFile("Program.cs");

        Assert.Contains("asp-page=\"/Minigames\"", layout);
        Assert.Contains("Menu_Minigames", layout);
        Assert.Contains("RenderSectionAsync(\"Styles\"", layout);
        Assert.Contains("AddOptions<MinigameOptions>()", program);
        Assert.Contains("MinigameCardSetStore", program);
        Assert.Contains("MapHub<MinigameHub>(\"/hubs/minigames\")", program);
    }

    [Fact]
    public void Card_count_is_configurable()
    {
        using var document = JsonDocument.Parse(ReadWebFile("appsettings.json"));
        var cardCount = document.RootElement
            .GetProperty("Minigames")
            .GetProperty("CardCount")
            .GetInt32();

        Assert.Equal(4, cardCount);
    }

    [Fact]
    public void Page_keeps_cards_in_the_viewport_with_equal_grid_cells()
    {
        var page = ReadWebFile("Pages", "Minigames.cshtml");
        var styles = ReadWebFile("wwwroot", "css", "minigames.css");
        var script = ReadWebFile("wwwroot", "js", "minigames.js");

        Assert.Contains("ViewData[\"HidePortalFooter\"] = true", page);
        Assert.Contains("data-highlighted-file", page);
        Assert.Contains("minigame-card-name", page);
        Assert.Contains("overflow: hidden", styles);
        Assert.Contains("height: calc(100dvh", styles);
        Assert.Contains("width: 100%;", styles);
        Assert.Contains("height: 100%;", styles);
        Assert.Contains("gridTemplateColumns", script);
        Assert.Contains("gridTemplateRows", script);
        Assert.Contains("ResizeObserver", script);
    }

    [Fact]
    public void Realtime_regeneration_resets_only_local_card_state()
    {
        var script = ReadWebFile("wwwroot", "js", "minigames.js");
        var hub = ReadWebFile("Hubs", "MinigameHub.cs");

        Assert.Contains("const inactiveCards = new Set()", script);
        Assert.Contains("inactiveCards.clear()", script);
        Assert.Contains("cardsRegenerated", script);
        Assert.Contains("withAutomaticReconnect()", script);
        Assert.DoesNotContain("localStorage", script);
        Assert.Contains("Clients.All.SendAsync(\"cardsRegenerated\"", hub);
    }

    private static string ReadWebFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = System.IO.Path.Combine(
                new[] { directory.FullName, "src", "BadWolfQuiz.Web" }
                    .Concat(parts)
                    .ToArray());
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate BadWolfQuiz.Web file: {System.IO.Path.Combine(parts)}");
    }
}
