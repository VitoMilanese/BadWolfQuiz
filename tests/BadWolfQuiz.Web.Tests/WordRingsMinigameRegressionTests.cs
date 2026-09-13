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
        Assert.Contains("word-rings-pointer-drag.js", game);
        Assert.DoesNotContain("word-rings-drag-visuals.js", game);
        Assert.Contains("word-rings-circle word-rings-circle-a\"></div>", game);
        Assert.Contains("word-rings-circle word-rings-circle-b\"></div>", game);
        Assert.Contains("word-rings-circle word-rings-circle-c\"></div>", game);
        Assert.Contains("@page \"/Admin/WordRingsEditor\"", editor);
        Assert.Contains("Authorize", editor);
        Assert.Contains("~/css/minigame-editor.css", editor);
        Assert.Contains("~/css/word-rings-editor.css", editor);
        Assert.Contains("word-rings-editor-heading", editor);
        Assert.DoesNotContain("<strong>A</strong>", editor);
        Assert.DoesNotContain("<strong>B</strong>", editor);
        Assert.DoesNotContain("<strong>C</strong>", editor);
        Assert.DoesNotContain("<span>A</span>", editor);
        Assert.DoesNotContain("<span>B</span>", editor);
        Assert.DoesNotContain("<span>C</span>", editor);
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
    public void Dragging_uses_pointer_events_instead_of_native_browser_drag_images()
    {
        var game = ReadWebFile("Pages", "WordRings.cshtml");
        var script = ReadWebFile("wwwroot", "js", "word-rings.js");
        var pointerDrag = ReadWebFile("wwwroot", "js", "word-rings-pointer-drag.js");
        var refinements = ReadWebFile("wwwroot", "css", "word-rings-refinements.css");

        Assert.DoesNotContain("draggable=\"true\"", game);
        Assert.DoesNotContain("dragstart", script);
        Assert.DoesNotContain("dataTransfer", script);
        Assert.Contains("pointerdown", pointerDrag);
        Assert.Contains("pointermove", pointerDrag);
        Assert.Contains("pointerup", pointerDrag);
        Assert.Contains("pointercancel", pointerDrag);
        Assert.Contains("setPointerCapture", pointerDrag);
        Assert.Contains("document.elementFromPoint", pointerDrag);
        Assert.Contains("word-rings-pointer-preview", pointerDrag);
        Assert.Contains("word.draggable = false", pointerDrag);
        Assert.DoesNotContain("dataTransfer", pointerDrag);
        Assert.DoesNotContain("setDragImage", pointerDrag);
        Assert.DoesNotContain("dragstart", pointerDrag);
        Assert.Contains("-webkit-user-drag: none", refinements);
        Assert.Contains("touch-action: none", refinements);
        Assert.Contains(".word-rings-pointer-preview", refinements);
    }

    [Fact]
    public void Revealed_rules_are_positioned_by_color_without_visible_ring_letters()
    {
        var game = ReadWebFile("Pages", "WordRings.cshtml");
        var refinements = ReadWebFile("wwwroot", "css", "word-rings-refinements.css");
        var en = ReadWebFile("Resources", "Localization", "WordRingsResource.resx");
        var uk = ReadWebFile("Resources", "Localization", "WordRingsResource.uk.resx");
        var it = ReadWebFile("Resources", "Localization", "WordRingsResource.it.resx");

        Assert.True(
            game.IndexOf("word-rings-stage", StringComparison.Ordinal) <
            game.IndexOf("word-rings-rules", StringComparison.Ordinal));
        Assert.Contains("word-rings-rule-a", game);
        Assert.Contains("word-rings-rule-b", game);
        Assert.Contains("word-rings-rule-c", game);
        Assert.Contains(".word-rings-stage > .word-rings-rules", refinements);
        Assert.Contains("border-color: var(--word-ring-a)", refinements);
        Assert.Contains("border-color: var(--word-ring-b)", refinements);
        Assert.Contains("border-color: var(--word-ring-c)", refinements);
        Assert.DoesNotContain("<value>A:", en);
        Assert.DoesNotContain("<value>B:", en);
        Assert.DoesNotContain("<value>C:", en);
        Assert.DoesNotContain("<value>A:", uk);
        Assert.DoesNotContain("<value>B:", uk);
        Assert.DoesNotContain("<value>C:", uk);
        Assert.DoesNotContain("<value>A:", it);
        Assert.DoesNotContain("<value>B:", it);
        Assert.DoesNotContain("<value>C:", it);
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
