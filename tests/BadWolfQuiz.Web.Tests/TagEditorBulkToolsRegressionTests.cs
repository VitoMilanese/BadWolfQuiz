namespace BadWolfQuiz.Web.Tests;

public sealed class TagEditorBulkToolsRegressionTests
{
    [Theory]
    [InlineData("src/BadWolfQuiz.Web/Pages/Admin/Quizzes/QuestionEditor.cshtml", "data-copy-question-tags", "question-tag-input")]
    [InlineData("src/BadWolfQuiz.Web/Pages/Admin/Quizzes/FinalQuestionEditor.cshtml", "data-copy-question-tags", "question-tag-input")]
    [InlineData("src/BadWolfQuiz.Web/Pages/Admin/Quizzes/QuizMetadataEditor.cshtml", "data-copy-quiz-tags", "quiz-tag-input")]
    public void Tag_editors_copy_all_tags_and_accept_comma_or_semicolon_lists(
        string relativePath,
        string copyButtonMarker,
        string inputId)
    {
        var source = ReadSource(relativePath);

        Assert.Contains(copyButtonMarker, source, StringComparison.Ordinal);
        Assert.Contains("tags.join(\"; \")", source, StringComparison.Ordinal);
        Assert.Contains("navigator.clipboard?.writeText", source, StringComparison.Ordinal);
        Assert.Contains("document.execCommand(\"copy\")", source, StringComparison.Ordinal);
        Assert.Contains(".split(/[;,]/)", source, StringComparison.Ordinal);
        Assert.Contains(".map(item => item.trim())", source, StringComparison.Ordinal);
        Assert.Contains("tagValue.length > 100", source, StringComparison.Ordinal);

        var inputMarker = $"id=\"{inputId}\"";
        var inputStart = source.IndexOf(inputMarker, StringComparison.Ordinal);
        Assert.True(inputStart >= 0);
        var inputEnd = source.IndexOf("/>", inputStart, StringComparison.Ordinal);
        Assert.True(inputEnd > inputStart);
        var inputMarkup = source[inputStart..inputEnd];
        Assert.DoesNotContain("maxlength=\"100\"", inputMarkup, StringComparison.Ordinal);
    }

    [Fact]
    public void Copy_button_is_first_tag_list_item_and_is_hidden_when_list_is_empty()
    {
        foreach (var relativePath in new[]
        {
            "src/BadWolfQuiz.Web/Pages/Admin/Quizzes/QuestionEditor.cshtml",
            "src/BadWolfQuiz.Web/Pages/Admin/Quizzes/FinalQuestionEditor.cshtml",
            "src/BadWolfQuiz.Web/Pages/Admin/Quizzes/QuizMetadataEditor.cshtml"
        })
        {
            var source = ReadSource(relativePath);
            Assert.Contains("copyButton.hidden = tags.length === 0", source, StringComparison.Ordinal);
            Assert.Contains("list.appendChild(copyButton)", source, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("src/BadWolfQuiz.Web/Pages/Admin/Quizzes/QuestionEditor.cshtml", "question-tag-action", "data-clear-question-tags")]
    [InlineData("src/BadWolfQuiz.Web/Pages/Admin/Quizzes/FinalQuestionEditor.cshtml", "question-tag-action", "data-clear-question-tags")]
    [InlineData("src/BadWolfQuiz.Web/Pages/Admin/Quizzes/QuizMetadataEditor.cshtml", "quiz-tag-action", "data-clear-quiz-tags")]
    public void Tag_action_buttons_are_square_and_clear_all_tags(
        string relativePath,
        string actionClass,
        string clearButtonMarker)
    {
        var source = ReadSource(relativePath);

        Assert.Contains($".{actionClass} {{ flex: 0 0 2.75rem;", source, StringComparison.Ordinal);
        Assert.Contains("width: 2.75rem;", source, StringComparison.Ordinal);
        Assert.Contains("height: 2.75rem;", source, StringComparison.Ordinal);
        Assert.Contains("aspect-ratio: 1 / 1;", source, StringComparison.Ordinal);
        Assert.Contains(clearButtonMarker, source, StringComparison.Ordinal);
        Assert.Contains("clearButton.hidden = tags.length === 0", source, StringComparison.Ordinal);
        Assert.Contains("list.appendChild(clearButton)", source, StringComparison.Ordinal);
        Assert.Contains("tags = [];", source, StringComparison.Ordinal);
        Assert.Contains("clearButton?.addEventListener", source, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("src/BadWolfQuiz.Web/Pages/Admin/Quizzes/QuestionEditor.cshtml")]
    [InlineData("src/BadWolfQuiz.Web/Pages/Admin/Quizzes/FinalQuestionEditor.cshtml")]
    [InlineData("src/BadWolfQuiz.Web/Pages/Admin/Quizzes/QuizMetadataEditor.cshtml")]
    public void Escape_closes_open_tag_dropdown_without_leaving_editor(string relativePath)
    {
        var source = ReadSource(relativePath);
        var escapeStart = source.IndexOf("if (event.key === \"Escape\")", StringComparison.Ordinal);
        Assert.True(escapeStart >= 0);

        var branchLength = Math.Min(320, source.Length - escapeStart);
        var escapeBranch = source.Substring(escapeStart, branchLength);
        Assert.Contains("if (!suggestionBox.hidden)", escapeBranch, StringComparison.Ordinal);
        Assert.Contains("event.preventDefault();", escapeBranch, StringComparison.Ordinal);
        Assert.Contains("event.stopPropagation();", escapeBranch, StringComparison.Ordinal);
        Assert.Contains("hideSuggestions();", escapeBranch, StringComparison.Ordinal);
    }

    [Fact]
    public void Global_escape_shortcut_treats_open_listbox_as_blocking_ui()
    {
        var source = ReadSource(
            "src/BadWolfQuiz.Web/wwwroot/js/gameplay-escape-shortcuts.js");

        Assert.Contains(
            "[role='listbox']:not([hidden])",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Quiz_metadata_bulk_add_stops_at_the_existing_maximum_tag_count()
    {
        var source = ReadSource(
            "src/BadWolfQuiz.Web/Pages/Admin/Quizzes/QuizMetadataEditor.cshtml");

        Assert.Contains("data-max-tags=", source, StringComparison.Ordinal);
        Assert.Contains("const maxTags = Number.parseInt", source, StringComparison.Ordinal);
        Assert.Contains("for (const tagValue of splitTagValues(value))", source, StringComparison.Ordinal);
        Assert.Contains("if (tags.length >= maxTags)", source, StringComparison.Ordinal);
        Assert.Contains("break;", source, StringComparison.Ordinal);
    }

    private static string ReadSource(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !Directory.Exists(Path.Combine(directory.FullName, "src", "BadWolfQuiz.Web")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return File.ReadAllText(Path.Combine(directory.FullName, relativePath));
    }
}
