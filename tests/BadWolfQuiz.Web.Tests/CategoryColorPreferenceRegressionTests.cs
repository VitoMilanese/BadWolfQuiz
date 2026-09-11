using System.Xml.Linq;
using BadWolfQuiz.Web.Models;

namespace BadWolfQuiz.Web.Tests;

public sealed class CategoryColorPreferenceRegressionTests
{
    [Fact]
    public void New_categories_default_to_automatic_color_mode()
    {
        var category = new QuizCategory();
        Assert.Equal(QuizCategoryColorMode.Automatic, category.ColorMode);
        Assert.Null(category.CustomColor);
    }

    [Fact]
    public void Category_description_editor_offers_all_three_color_modes()
    {
        var markup = ReadWebFile("Pages", "Admin", "Quizzes", "DescriptionEditor.cshtml");
        Assert.Contains("data-category-color-settings", markup, StringComparison.Ordinal);
        Assert.Contains("value=\"Automatic\"", markup, StringComparison.Ordinal);
        Assert.Contains("value=\"Custom\"", markup, StringComparison.Ordinal);
        Assert.Contains("value=\"Theme\"", markup, StringComparison.Ordinal);
        Assert.Contains("data-category-color-picker", markup, StringComparison.Ordinal);
        Assert.Contains("data-category-color-picker-trigger", markup, StringComparison.Ordinal);
        Assert.Contains("data-category-color-picker-popover", markup, StringComparison.Ordinal);
        Assert.Contains("data-category-color-spectrum", markup, StringComparison.Ordinal);
        Assert.Contains("data-category-color-hue", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("type=\"color\"", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Category_description_editor_has_compact_radios_and_save_shortcut()
    {
        var markup = ReadWebFile("Pages", "Admin", "Quizzes", "DescriptionEditor.cshtml");
        var css = ReadWebFile("wwwroot", "css", "description-editor.css");

        Assert.Contains(@"id=""description-editor-form""", markup, StringComparison.Ordinal);
        Assert.Contains(@"event.code === ""KeyS""", markup, StringComparison.Ordinal);
        Assert.Contains("event.ctrlKey || event.metaKey", markup, StringComparison.Ordinal);
        Assert.Contains("event.preventDefault();", markup, StringComparison.Ordinal);
        Assert.Contains("event.stopImmediatePropagation();", markup, StringComparison.Ordinal);
        Assert.Contains("editorForm.requestSubmit();", markup, StringComparison.Ordinal);
        Assert.Contains(@".category-color-mode-card input[type=""radio""]", css, StringComparison.Ordinal);
        Assert.Contains("width: 1.1rem;", css, StringComparison.Ordinal);
        Assert.Contains("grid-template-columns: 1.1rem minmax(0, 1fr);", css, StringComparison.Ordinal);
        Assert.Contains(".category-color-picker-popover", css, StringComparison.Ordinal);
        Assert.Contains(".category-color-picker-spectrum", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Category_description_editor_persists_mode_and_custom_color()
    {
        var source = ReadWebFile("Pages", "Admin", "Quizzes", "DescriptionEditor.cshtml.cs");
        Assert.Contains("ColorMode = category.ColorMode", source, StringComparison.Ordinal);
        Assert.Contains("CustomColor = category.CustomColor ?? DefaultCategoryColor", source, StringComparison.Ordinal);
        Assert.Contains("category.ColorMode = Input.ColorMode;", source, StringComparison.Ordinal);
        Assert.Contains("category.CustomColor = normalizedCustomColor;", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Game_board_receives_persisted_category_color_preferences()
    {
        var markup = ReadWebFile("Pages", "Admin", "Games", "Lobby.cshtml");
        var model = ReadWebFile("Pages", "Admin", "Games", "Lobby.cshtml.cs");
        Assert.Contains("data-category-color-mode", markup, StringComparison.Ordinal);
        Assert.Contains("data-category-custom-color", markup, StringComparison.Ordinal);
        Assert.Contains("LoadBoardCategoryColorsAsync", model, StringComparison.Ordinal);
        Assert.Contains("category.ColorMode", model, StringComparison.Ordinal);
        Assert.Contains("category.CustomColor", model, StringComparison.Ordinal);
    }

    [Fact]
    public void Persistent_host_board_syncs_category_color_metadata()
    {
        var markup = ReadWebFile("Pages", "Admin", "Games", "Lobby.cshtml");
        Assert.Contains("const syncCategoryColorMetadata =", markup, StringComparison.Ordinal);
        Assert.Contains("data-category-color-mode", markup, StringComparison.Ordinal);
        Assert.Contains("data-category-custom-color", markup, StringComparison.Ordinal);
        Assert.Contains("syncCategoryColorMetadata(currentGrid, nextGrid);", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Clone_and_package_preserve_category_color_preferences()
    {
        var clone = ReadWebFile("Services", "QuizCloneOperations.cs");
        var package = ReadWebFile("Services", "QuizPackageService.cs");
        Assert.Contains("ColorMode = sourceCategory.ColorMode", clone, StringComparison.Ordinal);
        Assert.Contains("CustomColor = sourceCategory.CustomColor", clone, StringComparison.Ordinal);
        Assert.Contains("category.ColorMode", package, StringComparison.Ordinal);
        Assert.Contains("category.CustomColor", package, StringComparison.Ordinal);
        Assert.Contains("QuizCategoryColorMode.Automatic", package, StringComparison.Ordinal);
    }

    [Fact]
    public void Category_color_labels_exist_in_all_supported_shared_resources()
    {
        foreach (var resource in new[]
        {
            "SharedResource.resx",
            "SharedResource.uk.resx",
            "SharedResource.it.resx",
            "SharedResource.ru.resx"
        })
        {
            var document = XDocument.Load(FindWebFile("Resources", "Localization", resource));
            var names = document.Root!
                .Elements("data")
                .Select(element => (string?)element.Attribute("name"))
                .ToHashSet(StringComparer.Ordinal);
            Assert.Contains("CategoryColor_Title", names);
            Assert.Contains("CategoryColor_Automatic", names);
            Assert.Contains("CategoryColor_Custom", names);
            Assert.Contains("CategoryColor_Theme", names);
        }
    }

    [Fact]
    public void Category_color_migration_defaults_existing_categories_to_automatic()
    {
        var migrationDirectory = Path.GetDirectoryName(
            FindWebFile("Migrations", "QuizDbContextModelSnapshot.cs"))!;
        var migration = Directory.GetFiles(migrationDirectory, "*_AddQuizCategoryColors.cs")
            .Single(path => !path.EndsWith(".Designer.cs", StringComparison.Ordinal));
        var source = File.ReadAllText(migration);
        Assert.Contains("name: \"ColorMode\"", source, StringComparison.Ordinal);
        Assert.Contains("defaultValue: 0", source, StringComparison.Ordinal);
        Assert.Contains("name: \"CustomColor\"", source, StringComparison.Ordinal);
        Assert.Contains("nullable: true", source, StringComparison.Ordinal);
    }

    private static string ReadWebFile(params string[] parts) =>
        File.ReadAllText(FindWebFile(parts));

    private static string FindWebFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                new[] { directory.FullName, "src", "BadWolfQuiz.Web" }
                    .Concat(parts)
                    .ToArray());
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate BadWolfQuiz.Web file: {Path.Combine(parts)}");
    }
}
