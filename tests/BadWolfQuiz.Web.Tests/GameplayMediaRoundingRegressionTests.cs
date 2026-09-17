namespace BadWolfQuiz.Web.Tests;

public sealed class GameplayMediaRoundingRegressionTests
{
    [Fact]
    public void Viewport_fit_controller_rounds_the_actual_contained_image_area()
    {
        var introStyles = ReadWebFile("wwwroot", "css", "game-intro-stage.css")
            .ReplaceLineEndings("\n");
        var viewportScript = ReadWebFile("wwwroot", "js", "game-content-viewport-fit.js")
            .ReplaceLineEndings("\n");

        Assert.Contains(
            "border-radius: clamp(12px, 1.3vw, 20px);",
            introStyles,
            StringComparison.Ordinal);
        Assert.Contains(
            "const imageSelector = \":scope > .game-content-block > img.game-content-image\";",
            viewportScript,
            StringComparison.Ordinal);
        Assert.Contains(
            "const gameplayImageRadius = \"clamp(12px, 1.3vw, 20px)\";",
            viewportScript,
            StringComparison.Ordinal);
        Assert.Contains(
            "const naturalRatio = image.naturalWidth / image.naturalHeight;",
            viewportScript,
            StringComparison.Ordinal);
        Assert.Contains(
            "const boxRatio = bounds.width / bounds.height;",
            viewportScript,
            StringComparison.Ordinal);
        Assert.Contains(
            "const paintedWidth = bounds.height * naturalRatio;",
            viewportScript,
            StringComparison.Ordinal);
        Assert.Contains(
            "const paintedHeight = bounds.width / naturalRatio;",
            viewportScript,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"clip-path\",",
            viewportScript,
            StringComparison.Ordinal);
        Assert.Contains(
            "round ${gameplayImageRadius}",
            viewportScript,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Runtime_rounding_covers_current_new_and_choice_images()
    {
        var viewportScript = ReadWebFile("wwwroot", "js", "game-content-viewport-fit.js")
            .ReplaceLineEndings("\n");

        Assert.Contains("img.game-content-image", viewportScript, StringComparison.Ordinal);
        Assert.Contains("img.final-question-transition-image", viewportScript, StringComparison.Ordinal);
        Assert.Contains(".all-player-choice-option img", viewportScript, StringComparison.Ordinal);
        Assert.Contains(".all-player-host-choice-option img", viewportScript, StringComparison.Ordinal);
        Assert.Contains(
            "document.querySelectorAll(gameplayImageSelector)",
            viewportScript,
            StringComparison.Ordinal);
        Assert.Contains(
            "if (event.target.matches(gameplayImageSelector))",
            viewportScript,
            StringComparison.Ordinal);
        Assert.Contains(
            "document.addEventListener(\"badwolf:host-gameplay-updated\", fitAll);",
            viewportScript,
            StringComparison.Ordinal);
        Assert.Contains(
            "window.addEventListener(\"resize\", scheduleFit);",
            viewportScript,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Existing_gameplay_asset_hook_loads_the_cache_busted_corner_controller()
    {
        var helper = ReadWebFile("TagHelpers", "GameplayPolishAssetsTagHelper.cs")
            .ReplaceLineEndings("\n");

        Assert.Contains(
            "[HtmlTargetElement(\"div\", Attributes = \"data-host-gameplay-view\")]",
            helper,
            StringComparison.Ordinal);
        Assert.Contains(
            "/js/game-content-viewport-fit.js?v=8",
            helper,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Host_lobby_renders_question_and_final_images_with_the_gameplay_image_class()
    {
        var lobby = ReadWebFile("Pages", "Admin", "Games", "Lobby.cshtml")
            .ReplaceLineEndings("\n");

        Assert.True(
            CountOccurrences(lobby, "<img class=\"game-content-image\"") >= 3,
            "Expected normal question, Final Question and Final Answer image renderers to use game-content-image.");
    }

    private static int CountOccurrences(string value, string search)
    {
        var count = 0;
        var offset = 0;
        while ((offset = value.IndexOf(search, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += search.Length;
        }

        return count;
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
