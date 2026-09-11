namespace BadWolfQuiz.Web.Tests;

public sealed class HostCustomAchievementEditorRegressionTests
{
    [Fact]
    public void Editor_uses_the_existing_question_tag_editor_interaction_model()
    {
        var root = FindRepositoryRoot();
        var editor = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "CustomAchievements.cshtml"));
        var source = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "CustomAchievements.cshtml.cs"));
        var questionEditor = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "Quizzes",
            "QuestionEditor.cshtml"));

        foreach (var hook in new[]
        {
            "data-question-tags-editor",
            "data-question-tag-list",
            "data-question-tag-fields",
            "data-question-tag-combobox",
            "data-question-tag-suggestions",
            "data-add-question-tag",
            "data-copy-question-tags",
            "data-clear-question-tags",
            "question-tag-chip",
            "question-tag-suggestion"
        })
        {
            Assert.Contains(hook, questionEditor, StringComparison.Ordinal);
            Assert.Contains(hook, editor, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("<textarea asp-for=\"Input.Tags\"", editor, StringComparison.Ordinal);
        Assert.Contains("name=\"Input.Tags\"", editor, StringComparison.Ordinal);
        Assert.Contains("OnGetTagSuggestionsAsync", source, StringComparison.Ordinal);
        Assert.Contains("QuestionTagSuggestionQuery.GetAsync", source, StringComparison.Ordinal);
        Assert.Contains("List<string> Tags", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Editor_uses_the_current_full_width_portal_visual_language()
    {
        var root = FindRepositoryRoot();
        var editor = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "CustomAchievements.cshtml"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "css",
            "custom-achievements.css"));

        Assert.Contains("custom-achievements-page", editor, StringComparison.Ordinal);
        Assert.Contains("custom-achievements-hero", editor, StringComparison.Ordinal);
        Assert.Contains("custom-achievements-workspace", editor, StringComparison.Ordinal);
        Assert.DoesNotContain("page-heading custom-achievement-heading", editor, StringComparison.Ordinal);
        Assert.DoesNotContain("content-panel custom-achievement-editor", editor, StringComparison.Ordinal);
        Assert.Contains(
            "body.portal-layout:has(.custom-achievements-page) > .page-shell",
            css,
            StringComparison.Ordinal);
        Assert.Contains(".custom-achievements-hero::after", css, StringComparison.Ordinal);
        Assert.Contains("clip-path:", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Header_menu_exposes_localized_achievement_editor_next_to_achievements()
    {
        var root = FindRepositoryRoot();
        var layout = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Shared",
            "_Layout.cshtml"));
        var imports = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "_ViewImports.cshtml"));
        var tagHelpers = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "TagHelpers",
            "HostCustomAchievementsTagHelper.cs"));
        var ukrainian = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Resources",
            "Localization",
            "HostCustomAchievementEditorResource.uk.resx"));

        var achievementsIndex = layout.IndexOf(
            "asp-page=\"/Achievements\"",
            StringComparison.Ordinal);
        var editorIndex = layout.IndexOf(
            "asp-page=\"/Admin/CustomAchievements\"",
            StringComparison.Ordinal);

        Assert.True(achievementsIndex >= 0);
        Assert.True(editorIndex > achievementsIndex);
        Assert.Contains("CustomAchievementLocalizer[\"Title\"]", layout, StringComparison.Ordinal);
        Assert.Contains("Редактор досягнень", ukrainian, StringComparison.Ordinal);
        Assert.Contains("HostCustomAchievementAssetsTagHelper", imports, StringComparison.Ordinal);
        Assert.Contains("HostCustomAchievementSelfGridTagHelper", imports, StringComparison.Ordinal);
        Assert.DoesNotContain("HostCustomAchievementMenuTagHelper", tagHelpers, StringComparison.Ordinal);
    }

    [Fact]
    public void Editor_save_is_ajax_compact_and_uses_shared_save_shortcut_and_popup()
    {
        var root = FindRepositoryRoot();
        var editor = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "Pages", "Admin", "CustomAchievements.cshtml"));
        var source = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "Pages", "Admin", "CustomAchievements.cshtml.cs"));
        var css = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "wwwroot", "css", "custom-achievements.css"));
        var script = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "wwwroot", "js", "custom-achievement-editor.js"));
        var shortcut = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "wwwroot", "js", "editor-save-shortcut.js"));
        var overlay = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "wwwroot", "js", "editor-save-overlay.js"));
        var shortcutHelper = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "TagHelpers", "EditorSaveShortcutAssetsTagHelper.cs"));

        Assert.Contains("data-ajax-custom-achievement-editor", editor, StringComparison.Ordinal);
        Assert.Contains("data-custom-achievement-save-status", editor, StringComparison.Ordinal);
        Assert.Contains("ajax-save-status", editor, StringComparison.Ordinal);
        Assert.Contains("~/js/editor-save-overlay.js", editor, StringComparison.Ordinal);
        Assert.DoesNotContain("CustomAchievementLocalizer[\"Back\"]", editor, StringComparison.Ordinal);
        Assert.DoesNotContain("CustomAchievementLocalizer[\"TargetHint\"]", editor, StringComparison.Ordinal);
        Assert.DoesNotContain(">HOST</span>", editor, StringComparison.Ordinal);
        Assert.Contains("CustomAchievementLocalizer[editing ? \"Save\" : \"Add\"]", editor, StringComparison.Ordinal);
        Assert.Contains("data-custom-achievement-new-button", editor, StringComparison.Ordinal);
        Assert.Contains("IsAjaxRequest()", source, StringComparison.Ordinal);
        Assert.Contains("X-Requested-With", script, StringComparison.Ordinal);
        Assert.Contains("window.BadWolfBusy?.show?.()", script, StringComparison.Ordinal);
        Assert.Contains("window.BadWolfBusy?.hide?.()", script, StringComparison.Ordinal);
        Assert.Contains("window.history.replaceState", script, StringComparison.Ordinal);
        Assert.Contains("form[data-ajax-custom-achievement-editor]", shortcut, StringComparison.Ordinal);
        Assert.Contains("CustomAchievementsModel", shortcutHelper, StringComparison.Ordinal);
        Assert.Contains("form[data-ajax-custom-achievement-editor]", overlay, StringComparison.Ordinal);
        Assert.Contains("[data-custom-achievement-save-status]", overlay, StringComparison.Ordinal);
        Assert.Contains("editor-save-overlay", overlay, StringComparison.Ordinal);
        Assert.Contains("align-content: start;", css, StringComparison.Ordinal);
        Assert.Contains("max-height: 48px;", css, StringComparison.Ordinal);
        Assert.Contains("min-height: 2.35em;", css, StringComparison.Ordinal);
        Assert.Contains(".question-tag-list:not(:has(", css, StringComparison.Ordinal);
        Assert.Contains(".question-tag-fields", css, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src")) &&
                Directory.Exists(Path.Combine(directory.FullName, "tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
