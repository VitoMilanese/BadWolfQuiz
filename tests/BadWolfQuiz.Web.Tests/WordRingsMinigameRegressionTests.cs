using BadWolfQuiz.Web.Services;

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
        Assert.True(
            navigation.LastIndexOf("href=\\\"/Admin/MinigameEditor\\\"", StringComparison.Ordinal) <
            navigation.LastIndexOf("href=\\\"/Admin/WordRingsEditor\\\"", StringComparison.Ordinal));
        Assert.Contains("@model BadWolfQuiz.Web.Pages.WordRingsModel", game);
        Assert.Contains("data-word-rings-puzzle", game);
        Assert.Contains("word-rings-pointer-drag.js", game);
        Assert.Contains("@model BadWolfQuiz.Web.Pages.Admin.WordRingsEditorModel", editor);
        Assert.Contains("Authorize(Policy = \"MasterHost\")", editor);
        Assert.Contains("word-rings-editor-tabs", editor);
        Assert.Contains("data-open-word-rings-create", editor);
        Assert.Contains("data-word-rings-delete-dialog", editor);
        Assert.DoesNotContain("word-rings-editor-coming-soon", editor);
        Assert.DoesNotContain("word-rings-editor-coming-soon", editorStyles);
        Assert.Contains("width: min(27vw, 390px)", editorStyles);
        Assert.Contains("@media (max-width: 600px)", editorStyles);
    }

    [Fact]
    public void Runtime_uses_server_generated_puzzle_and_real_ring_geometry()
    {
        var script = ReadWebFile("wwwroot", "js", "word-rings.js");
        var pageModel = ReadWebFile("Pages", "WordRings.cshtml.cs");
        var store = ReadWebFile("Services", "WordRingsRuleStore.cs");

        Assert.Contains("Object.entries(JSON.parse", script);
        Assert.Contains("membershipAt", script);
        Assert.Contains("ring.getBoundingClientRect()", script);
        Assert.Contains("Math.hypot", script);
        Assert.DoesNotContain("['панда', 'ABC']", script);
        Assert.Contains("CreatePuzzle()", pageModel);
        Assert.Contains("Random.Shared", store);
        Assert.Contains("WordRingColor.Blue", store);
        Assert.Contains("WordRingColor.Yellow", store);
        Assert.Contains("WordRingColor.Red", store);
        Assert.Contains("activeSnapshot", store);
        Assert.Contains("snapshot.Where(rule => rule.IsEnabled)", store);
        Assert.Contains("DefaultOutsideWords", store);
        Assert.Contains("assignOutside", script);
        Assert.Contains("errors === 0", script);
    }

    [Fact]
    public async Task Rule_store_excludes_disabled_rules_and_keeps_one_enabled_rule_per_ring()
    {
        var root = Path.Combine(Path.GetTempPath(), $"badwolf-word-rings-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var store = WordRingsRuleStore.Get(new TestWebHostEnvironment(root));
            var originalBlue = Assert.Single(store.GetRules(WordRingColor.Blue));

            var addResult = await store.AddAsync(
                WordRingColor.Blue,
                "Тестове активне правило",
                "альфа, бета",
                CancellationToken.None);
            Assert.Equal(WordRingRuleMutationResult.Success, addResult);

            var addedBlue = store.GetRules(WordRingColor.Blue)
                .Single(rule => rule.Id != originalBlue.Id);
            Assert.True(addedBlue.IsEnabled);

            var disableOriginal = await store.SetEnabledAsync(
                originalBlue.Id,
                enabled: false,
                CancellationToken.None);
            Assert.Equal(WordRingRuleMutationResult.Success, disableOriginal);

            for (var attempt = 0; attempt < 8; attempt++)
            {
                var puzzle = store.CreatePuzzle();
                Assert.Equal("Тестове активне правило", puzzle.BlueRuleText);
                Assert.DoesNotContain("кіт", puzzle.Words);
                Assert.DoesNotContain("вовк", puzzle.Words);
            }

            var disableLastEnabled = await store.SetEnabledAsync(
                addedBlue.Id,
                enabled: false,
                CancellationToken.None);
            Assert.Equal(WordRingRuleMutationResult.LastEnabledRule, disableLastEnabled);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
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
        Assert.Contains("left: 16%", refinements);
        Assert.Contains("right: 16%", refinements);
        Assert.Contains("bottom: 9%", refinements);
        Assert.Contains("data-membership=\"AB\"", styles);
        Assert.Contains("data-membership=\"AC\"", styles);
        Assert.Contains("data-membership=\"BC\"", styles);
        Assert.Contains("data-membership=\"ABC\"", styles);
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
        Assert.Contains("-webkit-user-drag: none", refinements);
        Assert.Contains("touch-action: none", refinements);
    }

    [Fact]
    public void Editor_persists_validated_rules_and_uses_styled_confirmation_dialog()
    {
        var editor = ReadWebFile("Pages", "Admin", "WordRingsEditor.cshtml");
        var model = ReadWebFile("Pages", "Admin", "WordRingsEditor.cshtml.cs");
        var store = ReadWebFile("Services", "WordRingsRuleStore.cs");
        var script = ReadWebFile("wwwroot", "js", "word-rings-editor.js");
        var overlay = ReadWebFile("wwwroot", "js", "editor-save-overlay.js");
        var styles = ReadWebFile("wwwroot", "css", "word-rings-editor.css");

        Assert.Contains("App_Data", store);
        Assert.Contains("word-rings-rules.json", store);
        Assert.Contains("[ ,;]+", store);
        Assert.DoesNotContain("[\\s,;]+", store);
        Assert.Contains("bool? Enabled = null", store);
        Assert.Contains("public bool IsEnabled => Enabled is not false", store);
        Assert.Contains("SetEnabledAsync", store);
        Assert.Contains("WordRingRuleMutationResult.LastRule", store);
        Assert.Contains("WordRingRuleMutationResult.LastEnabledRule", store);
        Assert.Contains("OnPostCreateRuleAsync", model);
        Assert.Contains("OnPostSetRuleEnabledAsync", model);
        Assert.Contains("OnPostDeleteRuleAsync", model);
        Assert.Contains("asp-page-handler=\"CreateRule\"", editor);
        Assert.Contains("asp-page-handler=\"SetRuleEnabled\"", editor);
        Assert.Contains("data-word-rings-rule-enabled", editor);
        Assert.Contains("type=\"checkbox\"", editor);
        Assert.Contains("asp-page-handler=\"DeleteRule\"", editor);
        Assert.Contains("dialog-card-danger", editor);
        Assert.Contains("data-editor-save-status", editor);
        Assert.Contains("data-editor-save-overlay-root", editor);
        Assert.Contains("~/js/editor-save-overlay.js", editor);
        Assert.Contains("[data-editor-save-overlay-root]", overlay);
        Assert.Contains("showModal()", script);
        Assert.Contains("requestSubmit()", script);
        Assert.Contains("[ ,;]+", script);
        Assert.DoesNotContain("[\\s,;]+", script);
        Assert.DoesNotContain("window.confirm", script);
        Assert.DoesNotContain("alert(", script);
        Assert.Contains("background: #9f101b !important", styles);
        Assert.Contains("border-color: #ff5b66 !important", styles);
        Assert.Contains(".word-rings-editor-rule-controls", styles);
        Assert.Contains("top: 16px", styles);
        Assert.Contains("right: 16px", styles);
        Assert.Contains("overflow-x: hidden", styles);
        Assert.Contains("box-sizing: border-box", styles);
        Assert.Contains("stroke: currentColor", styles);
    }

    [Fact]
    public void Revealed_rules_are_positioned_by_color_without_visible_ring_letters()
    {
        var game = ReadWebFile("Pages", "WordRings.cshtml");
        var refinements = ReadWebFile("wwwroot", "css", "word-rings-refinements.css");

        Assert.Contains("word-rings-rule-a", game);
        Assert.Contains("word-rings-rule-b", game);
        Assert.Contains("word-rings-rule-c", game);
        Assert.Contains("Model.Puzzle.BlueRuleText", game);
        Assert.Contains("Model.Puzzle.YellowRuleText", game);
        Assert.Contains("Model.Puzzle.RedRuleText", game);
        Assert.Contains("border-color: var(--word-ring-a)", refinements);
        Assert.Contains("border-color: var(--word-ring-b)", refinements);
        Assert.Contains("border-color: var(--word-ring-c)", refinements);
        Assert.DoesNotContain("<span>A</span>", game);
        Assert.DoesNotContain("<span>B</span>", game);
        Assert.DoesNotContain("<span>C</span>", game);
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

    private sealed class TestWebHostEnvironment(string root) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "BadWolfQuiz.Web.Tests";
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
        public string WebRootPath { get; set; } = root;
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = root;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
