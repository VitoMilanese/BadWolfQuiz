using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Tests;

public sealed class QuestionHintsRegressionTests
{
    [Fact]
    public void Editor_renders_standard_hint_toggle_and_text_image_collection()
    {
        var view = ReadWebFile("Pages", "Admin", "Quizzes", "QuestionEditor.cshtml");

        Assert.Contains("Input.EnableHints", view, StringComparison.Ordinal);
        Assert.Contains("data-standard-question-hints", view, StringComparison.Ordinal);
        Assert.Contains("CollectionId = \"hint-blocks\"", view, StringComparison.Ordinal);
        Assert.Contains("FieldPrefix = \"Input.HintBlocks\"", view, StringComparison.Ordinal);
        Assert.Contains("ContentBlockType.Text", view, StringComparison.Ordinal);
        Assert.Contains("ContentBlockType.Image", view, StringComparison.Ordinal);
        Assert.Contains("Label_Hints", view, StringComparison.Ordinal);
    }

    [Fact]
    public void Hint_tab_is_visible_only_for_enabled_standard_questions()
    {
        var tabs = ReadWebFile("wwwroot", "js", "image-clipboard-upload.js");
        var editor = ReadWebFile("Pages", "Admin", "Quizzes", "QuestionEditor.cshtml");

        Assert.Contains("createTab(\"hints\"", tabs, StringComparison.Ordinal);
        Assert.Contains("presentationTypeSelect.value === \"0\"", tabs, StringComparison.Ordinal);
        Assert.Contains("hintCheckbox.checked", tabs, StringComparison.Ordinal);
        Assert.Contains("badwolf:question-editor-hints-changed", tabs, StringComparison.Ordinal);
        Assert.Contains("standardHintsSetting.hidden = !isStandard", editor, StringComparison.Ordinal);
    }

    [Fact]
    public void Editor_enforces_one_to_four_text_or_image_hints()
    {
        var view = ReadWebFile("Pages", "Admin", "Quizzes", "QuestionEditor.cshtml");
        var model = ReadWebFile("Pages", "Admin", "Quizzes", "QuestionEditor.cshtml.cs");

        Assert.Contains("section.id === 'hint-blocks'", view, StringComparison.Ordinal);
        Assert.Contains("index >= 4", view, StringComparison.Ordinal);
        Assert.Contains("count <= 1", view, StringComparison.Ordinal);
        Assert.Contains("Input.HintBlocks.Count is < 1 or > 4", model, StringComparison.Ordinal);
        Assert.Contains("not ContentBlockType.Text", model, StringComparison.Ordinal);
        Assert.Contains("not ContentBlockType.Image", model, StringComparison.Ordinal);
    }

    [Fact]
    public void Hint_blocks_are_persisted_independently_and_return_ids_after_ajax_save()
    {
        var models = ReadWebFile("Models", "QuizModels.cs");
        var context = ReadWebFile("Data", "QuizDbContext.cs");
        var editor = ReadWebFile("Pages", "Admin", "Quizzes", "QuestionEditor.cshtml.cs");

        Assert.Contains("ICollection<QuestionHintContentBlock> HintBlocks", models, StringComparison.Ordinal);
        Assert.Contains("class QuestionHintContentBlock", models, StringComparison.Ordinal);
        Assert.Contains("DbSet<QuestionHintContentBlock> QuestionHintContentBlocks", context, StringComparison.Ordinal);
        Assert.Contains("db.QuestionHintContentBlocks", editor, StringComparison.Ordinal);
        Assert.Contains("OnGetHintBlockFileAsync", editor, StringComparison.Ordinal);
        Assert.Contains("hintBlocks = persistedHintBlocks", editor, StringComparison.Ordinal);
    }

    [Fact]
    public void Copy_clone_package_and_archive_paths_preserve_hint_blocks()
    {
        var copy = ReadWebFile("Services", "QuestionCopyOperations.cs");
        var clone = ReadWebFile("Services", "QuizCloneOperations.cs");
        var package = ReadWebFile("Services", "QuizPackageService.cs");
        var archive = ReadWebFile("Services", "QuizMediaArchiveService.cs");
        var background = ReadWebFile("Services", "MediaArchiveBackgroundService.cs");

        Assert.Contains("source.HintBlocks", copy, StringComparison.Ordinal);
        Assert.Contains("CloneHintBlock", copy, StringComparison.Ordinal);
        Assert.Contains("sourceQuestion.HintBlocks", clone, StringComparison.Ordinal);
        Assert.Contains("QuestionHintContentBlock", package, StringComparison.Ordinal);
        Assert.Contains("HintBlocks = null", package, StringComparison.Ordinal);
        Assert.Contains("ArchivedMediaRole.HintBlock", archive, StringComparison.Ordinal);
        Assert.Contains("QuestionHintContentBlocks", background, StringComparison.Ordinal);
    }

    [Fact]
    public void Gameplay_hint_controls_use_registered_dedicated_client_actions()
    {
        var imports = ReadWebFile("Pages", "_ViewImports.cshtml");
        var controls = ReadWebFile("TagHelpers", "QuestionHintGameplayTagHelpers.cs");
        var assets = ReadWebFile(
            "TagHelpers",
            "QuestionHintGameplayClientAssetsTagHelper.cs");

        Assert.Contains(
            "QuestionHintGameplayClientAssetsTagHelper",
            imports,
            StringComparison.Ordinal);
        Assert.Contains(
            "button.Attributes[\"type\"] = \"button\"",
            controls,
            StringComparison.Ordinal);
        Assert.Contains(
            "data-question-hint-action-url",
            controls,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "button.Attributes[\"formaction\"]",
            controls,
            StringComparison.Ordinal);
        Assert.Contains(
            "target.dataset.questionHintActionUrl",
            assets,
            StringComparison.Ordinal);
        Assert.Contains("setBusy(true)", assets, StringComparison.Ordinal);
        Assert.Contains("}, true);", assets, StringComparison.Ordinal);
        Assert.Contains("margin: 0 !important;", assets, StringComparison.Ordinal);
        Assert.Contains(
            ".host-game-board > .message.message-error",
            assets,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Ef_model_has_cascading_hint_block_relationship()
    {
        var options = new DbContextOptionsBuilder<QuizDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        using var db = new QuizDbContext(options);
        var entity = db.Model.FindEntityType(typeof(QuestionHintContentBlock));

        Assert.NotNull(entity);
        var foreignKey = Assert.Single(entity!.GetForeignKeys());
        Assert.Equal(typeof(QuizQuestion), foreignKey.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior);
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
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }
            directory = directory.Parent;
        }
        throw new FileNotFoundException(Path.Combine(parts));
    }
}
