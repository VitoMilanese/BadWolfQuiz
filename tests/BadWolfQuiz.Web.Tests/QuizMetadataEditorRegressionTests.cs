namespace BadWolfQuiz.Web.Tests;

public sealed class QuizMetadataEditorRegressionTests
{
    [Fact]
    public void Quiz_editor_links_title_icon_to_metadata_page_and_preserves_selected_round()
    {
        var markup = ReadWebFile("Pages", "Admin", "Quizzes", "Editor.cshtml");
        Assert.Contains("class=\"quiz-metadata-edit-button\"", markup, StringComparison.Ordinal);
        Assert.Contains("asp-page=\"QuizMetadataEditor\"", markup, StringComparison.Ordinal);
        Assert.Contains("asp-route-id=\"@Model.Quiz.Id\"", markup, StringComparison.Ordinal);
        Assert.Contains("asp-route-selectedRoundId=\"@Model.SelectedRoundId\"", markup, StringComparison.Ordinal);
        Assert.Contains("QuizMetadata_Edit", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"quiz-editor-title-with-action\"", markup, StringComparison.Ordinal);
        Assert.Contains("<span>@Model.Quiz.Title</span>", markup, StringComparison.Ordinal);
        Assert.Contains("vertical-align: middle;", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("quiz-editor-title-row", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Metadata_editor_has_title_description_twenty_tags_and_stable_standard_actions()
    {
        var markup = ReadWebFile("Pages", "Admin", "Quizzes", "QuizMetadataEditor.cshtml");
        var source = ReadWebFile("Pages", "Admin", "Quizzes", "QuizMetadataEditor.cshtml.cs");
        Assert.Contains("asp-for=\"Input.Title\"", markup, StringComparison.Ordinal);
        Assert.Contains("asp-for=\"Input.Description\"", markup, StringComparison.Ordinal);
        Assert.Contains("data-quiz-tags-editor", markup, StringComparison.Ordinal);
        Assert.Contains("name=\"Input.Tags\"", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"editor-actions\"", markup, StringComparison.Ordinal);
        Assert.Contains("asp-page=\"Editor\"", markup, StringComparison.Ordinal);
        Assert.Contains("@Localizer[\"Button_Back\"]", markup, StringComparison.Ordinal);
        Assert.Contains("@Localizer[\"Button_Save\"]", markup, StringComparison.Ordinal);
        Assert.Contains("public const int MaximumTagCount = 20;", source, StringComparison.Ordinal);
        Assert.Contains("Validation_QuizTagLimit", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_ValidationScriptsPartial", markup, StringComparison.Ordinal);
        Assert.Contains("id=\"success-message\"", markup, StringComparison.Ordinal);
        Assert.Contains("data-editor-save-status", markup, StringComparison.Ordinal);
        Assert.Contains("quiz-metadata-save-status:not(.editor-save-overlay)", markup, StringComparison.Ordinal);
        Assert.Contains("data-editor-reset=\"true\"", markup, StringComparison.Ordinal);
        Assert.Contains("editor-reset-button", markup, StringComparison.Ordinal);
        Assert.Contains("maxSuggestionResults = 8", markup, StringComparison.Ordinal);
        Assert.Contains(".slice(0, maxSuggestionResults)", markup, StringComparison.Ordinal);
        Assert.Contains("for (const tagValue of splitTagValues(value))", markup, StringComparison.Ordinal);
        Assert.Contains("if (tags.length >= maxTags)", markup, StringComparison.Ordinal);
        Assert.Contains("tags.push(tagValue);", markup, StringComparison.Ordinal);
        Assert.Contains("event.key !== \"Escape\"", markup, StringComparison.Ordinal);
        Assert.Contains("backLink.click();", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("data-max-message", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("data-quiz-tag-limit-message", markup, StringComparison.Ordinal);
        Assert.Contains("if (Input.Tags.Count > MaximumTagCount)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Metadata_editor_queries_only_scalar_metadata_and_quiz_tags()
    {
        var source = ReadWebFile("Pages", "Admin", "Quizzes", "QuizMetadataEditor.cshtml.cs");
        Assert.Contains(".AsNoTracking()", source, StringComparison.Ordinal);
        Assert.Contains(".Select(x => new", source, StringComparison.Ordinal);
        Assert.Contains("Tags = x.Tags", source, StringComparison.Ordinal);
        Assert.Contains(".Include(x => x.Tags)", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".Include(x => x.Rounds)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("QuestionContentBlocks", source, StringComparison.Ordinal);
        Assert.DoesNotContain("FinalQuestionBlocks", source, StringComparison.Ordinal);
        Assert.DoesNotContain("FileData", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Quiz_tag_model_is_host_scoped_unique_per_quiz_and_cascades_with_quiz()
    {
        var context = ReadWebFile("Data", "QuizDbContext.cs");
        Assert.Contains("public DbSet<QuizTag> QuizTags", context, StringComparison.Ordinal);
        Assert.Contains(".HasQueryFilter(x => x.Quiz.HostId == CurrentHostId)", context, StringComparison.Ordinal);
        Assert.Contains(".WithMany(x => x.Tags)", context, StringComparison.Ordinal);
        Assert.Contains("new { x.QuizId, x.NormalizedName }", context, StringComparison.Ordinal);
        Assert.Contains(".OnDelete(DeleteBehavior.Cascade)", context, StringComparison.Ordinal);
    }

    [Fact]
    public void Metadata_editor_localization_keys_exist_in_every_shared_resource()
    {
        var keys = new[]
        {
            "Title_QuizMetadataEditor", "QuizMetadata_Edit", "Label_QuizTags",
            "Placeholder_QuizTag", "Hint_QuizTags", "Validation_QuizTagLimit",
            "Validation_QuizTagTooLong", "QuizMetadata_Saved"
        };
        var resources = new[]
        {
            "SharedResource.resx", "SharedResource.uk.resx",
            "SharedResource.it.resx", "SharedResource.ru.resx"
        };
        foreach (var resource in resources)
        {
            var content = ReadWebFile("Resources", "Localization", resource);
            foreach (var key in keys)
                Assert.Contains($"name=\"{key}\"", content, StringComparison.Ordinal);
        }
    }

    private static string ReadWebFile(params string[] parts) =>
        NormalizeLineEndings(File.ReadAllText(FindWebFile(parts)));

    private static string NormalizeLineEndings(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    private static string FindWebFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "BadWolfQuiz.Web", Path.Combine(parts));
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }
        throw new FileNotFoundException($"Could not locate BadWolfQuiz.Web file: {Path.Combine(parts)}");
    }
}
