namespace BadWolfQuiz.Web.Tests;

public sealed class AnswerKeyRegressionTests
{
    [Fact]
    public void All_player_multiple_choice_answer_key_uses_only_the_correct_option()
    {
        var model = File.ReadAllText(FindWebFile(
            "Pages",
            "Admin",
            "Games",
            "AnswerKey.cshtml.cs"));

        Assert.Contains(
            "QuestionPresentationType.AllPlayerMultipleChoice",
            model,
            StringComparison.Ordinal);
        Assert.Contains(
            "return [question.AnswerBlocks[0]];",
            model,
            StringComparison.Ordinal);
        Assert.Contains(
            "AnswerBlocks = GetVisibleAnswerBlocks(question);",
            model,
            StringComparison.Ordinal);
        Assert.Contains(
            ": GetVisibleAnswerBlocks(question);",
            model,
            StringComparison.Ordinal);
        Assert.Contains(
            "return question.AnswerBlocks;",
            model,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Answer_key_uses_the_real_topbar_and_does_not_create_page_scroll_chrome()
    {
        var page = File.ReadAllText(FindWebFile(
            "Pages",
            "Admin",
            "Games",
            "AnswerKey.cshtml"));
        var css = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "answer-key.css"));

        Assert.Contains("~/css/answer-key.css", page, StringComparison.Ordinal);
        Assert.Contains(
            "ViewData[\"HidePortalFooter\"] = true;",
            page,
            StringComparison.Ordinal);
        Assert.Contains("@section HeaderContext", page, StringComparison.Ordinal);
        Assert.Contains(
            "<h2>@Localizer[\"Label_CorrectAnswer\"]</h2>",
            page,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "<header class=\"answer-key-header\">",
            page,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Model.QuestionLabel", page, StringComparison.Ordinal);
        Assert.DoesNotContain("GameBoard_Answer", page, StringComparison.Ordinal);

        Assert.Contains(
            "body:has(.answer-key-page) .page-shell",
            css,
            StringComparison.Ordinal);
        Assert.Contains(
            "height: calc(100dvh - var(--topbar-height));",
            css,
            StringComparison.Ordinal);
        Assert.Contains("padding: 0;", css, StringComparison.Ordinal);
        Assert.Contains("overflow: hidden;", css, StringComparison.Ordinal);
        Assert.Contains("height: 100%;", css, StringComparison.Ordinal);
        Assert.Contains("min-height: 0;", css, StringComparison.Ordinal);
        Assert.Contains(
            ".answer-key-content.game-content-presentation",
            css,
            StringComparison.Ordinal);
        Assert.Contains("width: 100%;", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Answer_key_starts_hidden_and_header_eye_toggles_the_answer()
    {
        var page = File.ReadAllText(FindWebFile(
            "Pages",
            "Admin",
            "Games",
            "AnswerKey.cshtml"));
        var css = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "answer-key.css"));

        Assert.Contains("data-answer-key-visibility-toggle", page, StringComparison.Ordinal);
        Assert.Contains("aria-controls=\"answer-key-content\"", page, StringComparison.Ordinal);
        Assert.Contains("aria-pressed=\"false\"", page, StringComparison.Ordinal);
        Assert.Contains("data-answer-key-show-icon", page, StringComparison.Ordinal);
        Assert.Contains("data-answer-key-hide-icon", page, StringComparison.Ordinal);
        Assert.Contains("data-answer-key-hidden-placeholder", page, StringComparison.Ordinal);
        Assert.Contains("@Localizer[\"Button_ShowAnswer\"]", page, StringComparison.Ordinal);
        Assert.Contains("id=\"answer-key-content\"", page, StringComparison.Ordinal);
        Assert.Contains("data-answer-key-content", page, StringComparison.Ordinal);
        Assert.Contains("data-answer-key-content\n                 hidden", page, StringComparison.Ordinal);
        Assert.Contains("content.hidden = !isVisible;", page, StringComparison.Ordinal);
        Assert.Contains("placeholder.hidden = isVisible;", page, StringComparison.Ordinal);
        Assert.Contains("showIcon.hidden = isVisible;", page, StringComparison.Ordinal);
        Assert.Contains("hideIcon.hidden = !isVisible;", page, StringComparison.Ordinal);
        Assert.Contains(
            "toggle.setAttribute(\"aria-pressed\", isVisible.toString());",
            page,
            StringComparison.Ordinal);

        Assert.Contains(".answer-key-hidden-placeholder", css, StringComparison.Ordinal);
        Assert.Contains(".answer-key-content[hidden]", css, StringComparison.Ordinal);
        Assert.Contains(".answer-key-visibility-toggle [hidden]", css, StringComparison.Ordinal);
    }

    private static string FindWebFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidateParts = new[]
            {
                directory.FullName,
                "src",
                "BadWolfQuiz.Web"
            }.Concat(parts).ToArray();
            var candidate = Path.Combine(candidateParts);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate BadWolfQuiz.Web file: {Path.Combine(parts)}");
    }
}
