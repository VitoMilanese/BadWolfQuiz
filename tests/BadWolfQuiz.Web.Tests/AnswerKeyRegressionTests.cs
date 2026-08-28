namespace BadWolfQuiz.Web.Tests;

public sealed class AnswerKeyRegressionTests
{
    [Fact]
    public void Multiple_choice_answer_key_uses_correct_option_and_additional_content()
    {
        var model = File.ReadAllText(FindWebFile(
            "Pages",
            "Admin",
            "Games",
            "AnswerKey.cshtml.cs"));

        Assert.Contains(
            "MultipleChoiceAnswerContract.IsMultipleChoice",
            model,
            StringComparison.Ordinal);
        Assert.Contains(
            "definition?.RevealAnswerBlocks",
            model,
            StringComparison.Ordinal);
        Assert.Contains(
            "question.AnswerBlocks.Take(1).ToArray()",
            model,
            StringComparison.Ordinal);
        Assert.Contains(
            "IsPeerRatedQuestion = question.PresentationType ==",
            model,
            StringComparison.Ordinal);
        Assert.Contains(
            "? question.QuestionBlocks",
            model,
            StringComparison.Ordinal);
        Assert.Contains(
            ": GetVisibleAnswerBlocks(game, question);",
            model,
            StringComparison.Ordinal);
        Assert.Contains(
            "return question.AnswerBlocks;",
            model,
            StringComparison.Ordinal);
        Assert.Contains(
            "DeferredGameMediaStore mediaStore",
            model,
            StringComparison.Ordinal);
        Assert.Contains(
            "DeferredGameMediaRole.Question",
            model,
            StringComparison.Ordinal);
        Assert.Contains(
            "DeferredGameMediaRole.Answer",
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
            "var answerKeyLabel = Model.IsPeerRatedQuestion",
            page,
            StringComparison.Ordinal);
        Assert.Contains(
            "Localizer[\"GameBoard_Question\"]",
            page,
            StringComparison.Ordinal);
        Assert.Contains(
            "Localizer[\"Label_CorrectAnswer\"]",
            page,
            StringComparison.Ordinal);
        Assert.Contains("<h2>@answerKeyLabel</h2>", page, StringComparison.Ordinal);
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
    public void Answer_key_visibility_mode_is_always_available_and_persists_between_reloads()
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
        Assert.Contains("data-answer-visible=\"false\"", page, StringComparison.Ordinal);
        Assert.Contains("aria-pressed=\"false\"", page, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "@if (Model.AnswerBlocks.Count > 0)",
            page,
            StringComparison.Ordinal);

        Assert.Contains("answer-key-show-icon", page, StringComparison.Ordinal);
        Assert.Contains("answer-key-hide-icon", page, StringComparison.Ordinal);
        Assert.Contains(
            ".answer-key-visibility-toggle[data-answer-visible=\"true\"] .answer-key-show-icon",
            css,
            StringComparison.Ordinal);
        Assert.Contains(
            ".answer-key-visibility-toggle[data-answer-visible=\"true\"] .answer-key-hide-icon",
            css,
            StringComparison.Ordinal);

        Assert.Contains(
            "const storageKey = `badwolf-answer-key-visible:${page.dataset.gameCode ?? \"\"}`;",
            page,
            StringComparison.Ordinal);
        Assert.Contains(
            "window.sessionStorage.getItem(storageKey) === \"true\"",
            page,
            StringComparison.Ordinal);
        Assert.Contains(
            "window.sessionStorage.setItem(storageKey, isVisible.toString());",
            page,
            StringComparison.Ordinal);
        Assert.Contains(
            "setAnswerVisibility(readStoredVisibility());",
            page,
            StringComparison.Ordinal);
        Assert.Contains(
            "toggle.dataset.answerVisible = isVisible ? \"true\" : \"false\";",
            page,
            StringComparison.Ordinal);
        Assert.Contains(
            "toggle.setAttribute(\"aria-pressed\", isVisible.toString());",
            page,
            StringComparison.Ordinal);
        Assert.Contains(
            "const isVisible = toggle.dataset.answerVisible !== \"true\";",
            page,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Hidden_answer_uses_placeholder_and_visibility_mode_applies_when_answer_exists()
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
        var normalizedPage = NormalizeWhitespace(page);

        Assert.Contains("data-answer-key-hidden-placeholder", page, StringComparison.Ordinal);
        Assert.Contains(
            "var showAnswerKeyLabel = Model.IsPeerRatedQuestion",
            page,
            StringComparison.Ordinal);
        Assert.Contains(
            "Localizer[\"GameBoard_ShowQuestion\"]",
            page,
            StringComparison.Ordinal);
        Assert.Contains(
            "Localizer[\"Button_ShowAnswer\"]",
            page,
            StringComparison.Ordinal);
        Assert.Contains("<span>@showAnswerKeyLabel</span>", page, StringComparison.Ordinal);
        Assert.Contains("id=\"answer-key-content\"", page, StringComparison.Ordinal);
        Assert.Contains("data-answer-key-content", page, StringComparison.Ordinal);
        Assert.Contains(
            "data-answer-key-content hidden>",
            normalizedPage,
            StringComparison.Ordinal);
        Assert.Contains("content.hidden = !isVisible;", page, StringComparison.Ordinal);
        Assert.Contains("placeholder.hidden = isVisible;", page, StringComparison.Ordinal);

        Assert.Contains(".answer-key-hidden-placeholder", css, StringComparison.Ordinal);
        Assert.Contains(".answer-key-content[hidden]", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Answer_key_reloads_only_when_the_answer_identity_changes()
    {
        var page = File.ReadAllText(FindWebFile(
            "Pages",
            "Admin",
            "Games",
            "AnswerKey.cshtml"));
        var model = File.ReadAllText(FindWebFile(
            "Pages",
            "Admin",
            "Games",
            "AnswerKey.cshtml.cs"));
        var normalizedPage = NormalizeWhitespace(page);

        Assert.Contains(
            "public int? CurrentSourceQuestionId { get; private set; }",
            model,
            StringComparison.Ordinal);
        Assert.Contains(
            "CurrentSourceQuestionId = question.SourceQuestionId;",
            model,
            StringComparison.Ordinal);
        Assert.Contains(
            "data-game-status=\"@Model.Game.Session.Status.ToString().ToLowerInvariant()\"",
            page,
            StringComparison.Ordinal);
        Assert.Contains(
            "data-source-question-id=\"@Model.CurrentSourceQuestionId\"",
            page,
            StringComparison.Ordinal);
        Assert.Contains(
            "const renderedQuestionId = Number.parseInt(",
            page,
            StringComparison.Ordinal);
        Assert.Contains(
            "let currentStatus = (page.dataset.gameStatus ?? \"\").toLowerCase();",
            page,
            StringComparison.Ordinal);
        Assert.Contains(
            "let currentQuestionId = Number.isInteger(renderedQuestionId)",
            page,
            StringComparison.Ordinal);
        Assert.Contains("let reloadRequested = false;", page, StringComparison.Ordinal);
        Assert.Contains("const requestReload = () =>", page, StringComparison.Ordinal);
        Assert.Contains(
            "currentStatus === \"running\" && finalQuestionStatuses.has(nextStatus)",
            normalizedPage,
            StringComparison.Ordinal);
        Assert.Contains(
            "const nextQuestionId = Number.isInteger(update?.sourceQuestionId)",
            page,
            StringComparison.Ordinal);
        Assert.Contains(
            "if (nextQuestionId === null || nextQuestionId === currentQuestionId)",
            normalizedPage,
            StringComparison.Ordinal);
        Assert.Contains("currentQuestionId = nextQuestionId;", page, StringComparison.Ordinal);
        Assert.DoesNotContain("hasInitialStatus", page, StringComparison.Ordinal);
        Assert.DoesNotContain("hasInitialBuzzer", page, StringComparison.Ordinal);
        Assert.Equal(
            1,
            page.Split(
                "window.location.reload();",
                StringSplitOptions.None).Length - 1);
    }

    private static string NormalizeWhitespace(string value)
    {
        return System.Text.RegularExpressions.Regex.Replace(
            value,
            @"\s+",
            " ").Trim();
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
