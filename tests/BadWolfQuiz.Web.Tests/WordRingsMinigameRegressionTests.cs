namespace BadWolfQuiz.Web.Tests;

public sealed class WordRingsMinigameRegressionTests
{
    [Fact]
    public void Catalog_and_header_expose_word_rings_game_and_editor()
    {
        var catalog = ReadWebFile("Pages", "Minigames.cshtml");
        var layout = ReadWebFile("Pages", "Shared", "_Layout.cshtml");
        var navigation = ReadWebFile("TagHelpers", "HeaderSeoNavigationTagHelper.cs");
        var game = ReadWebFile("Pages", "WordRings.cshtml");
        var editor = ReadWebFile("Pages", "Admin", "WordRingsEditor.cshtml");
        var editorStyles = ReadWebFile("wwwroot", "css", "word-rings-editor.css");

        Assert.Contains("asp-page=\"/WordRings\"", catalog);
        Assert.DoesNotContain("<strong>02</strong>", catalog);
        Assert.Contains("asp-page=\"/Admin/WordRingsEditor\"", layout);
        Assert.Contains("wordRingsLocalizer", navigation);
        Assert.Contains("output.SuppressOutput()", navigation);
        Assert.Contains("href=\\\"/Admin/MinigameEditor\\\"", navigation);
        Assert.Contains("href=\\\"/Admin/WordRingsEditor\\\"", navigation);
        Assert.True(
            navigation.LastIndexOf("href=\\\"/Admin/MinigameEditor\\\"", StringComparison.Ordinal) <
            navigation.LastIndexOf("href=\\\"/Admin/WordRingsEditor\\\"", StringComparison.Ordinal),
            "Word-rings editor must render after the existing minigame editor for MasterHost navigation.");
        Assert.Contains("@page \"/minigames/word-rings\"", game);
        Assert.Contains("data-ring-stage", game);
        Assert.Contains("data-word-list", game);
        Assert.Contains("data-outside-zone", game);
        Assert.Contains("word-rings-refinements.css", game);
        Assert.Contains("word-rings-drag-visuals.js", game);
        Assert.Contains("@page \"/Admin/WordRingsEditor\"", editor);
        Assert.Contains("Authorize", editor);
        Assert.Contains("~/css/minigame-editor.css", editor);
        Assert.Contains("~/css/word-rings-editor.css", editor);
        Assert.Contains("word-rings-editor-heading", editor);
        Assert.DoesNotContain("style=\"", editor);
        Assert.Contains("word-rings-editor-coming-soon", editorStyles);
        Assert.Contains("@media (max-width: 600px)", editorStyles);
    }

    [Fact]
    public void Runtime_supports_all_venn_memberships_and_validation()
    {
        var script = ReadWebFile("wwwroot", "js", "word-rings.js");

        Assert.Contains("membershipAt", script);
        Assert.Contains("pointIsInsideRing", script);
        Assert.Contains("ring.getBoundingClientRect()", script);
        Assert.Contains("Math.hypot", script);
        Assert.DoesNotContain("['A', 0.38, 0.39]", script);
        Assert.Contains("['панда', 'ABC']", script);
        Assert.Contains("['лампа', 'BC']", script);
        Assert.Contains("['песик', 'AC']", script);
        Assert.Contains("['жаба', 'AB']", script);
        Assert.Contains("['дім', '']", script);
        Assert.Contains("assignOutside", script);
        Assert.Contains("errors === 0", script);
    }

    [Fact]
    public void Board_uses_centered_symmetric_ring_geometry_and_membership_colored_word_borders()
    {
        var styles = ReadWebFile("wwwroot", "css", "word-rings.css");
        var refinements = ReadWebFile("wwwroot", "css", "word-rings-refinements.css");
        var editorStyles = ReadWebFile("wwwroot", "css", "word-rings-editor.css");

        Assert.Contains("--word-ring-a: #5f86ee", styles);
        Assert.Contains("--word-ring-b: #e0b43c", styles);
        Assert.Contains("--word-ring-c: #e85d5d", styles);
        Assert.Contains("width: 48%", styles);
        Assert.Contains("left: 16%", refinements);
        Assert.Contains("right: 16%", refinements);
        Assert.Contains("top: 4.5%", refinements);
        Assert.Contains("left: 26%", refinements);
        Assert.Contains("bottom: 9%", refinements);
        Assert.Contains("data-membership=\"AB\"", styles);
        Assert.Contains("data-membership=\"AC\"", styles);
        Assert.Contains("data-membership=\"BC\"", styles);
        Assert.Contains("data-membership=\"ABC\"", styles);
        Assert.Contains("33.333%", styles);
        Assert.Contains("66.666%", styles);
        Assert.Contains("color: #5f86ee", editorStyles);
        Assert.Contains("color: #e0b43c", editorStyles);
        Assert.Contains("color: #e85d5d", editorStyles);
    }

    [Fact]
    public void Drag_visuals_use_a_clean_custom_ghost_and_clear_transient_drag_state_after_drop()
    {
        var refinements = ReadWebFile("wwwroot", "css", "word-rings-refinements.css");
        var dragVisuals = ReadWebFile("wwwroot", "js", "word-rings-drag-visuals.js");

        Assert.Contains("word-rings-drag-ghost", refinements);
        Assert.Contains("opacity: 1 !important", refinements);
        Assert.Contains("document.createElement('span')", dragVisuals);
        Assert.Contains("setDragImage", dragVisuals);
        Assert.Contains("requestAnimationFrame(clearDragVisuals)", dragVisuals);
        Assert.Contains("classList.remove('is-dragging')", dragVisuals);
        Assert.Contains("source.dataset.membership", dragVisuals);
    }

    private static string ReadWebFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName, "src", "BadWolfQuiz.Web" }.Concat(parts).ToArray());
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate BadWolfQuiz.Web file: {Path.Combine(parts)}");
    }
}
