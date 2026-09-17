namespace BadWolfQuiz.Web.Tests;

public sealed class QuestionHintGameplayRegressionTests
{
    [Fact]
    public void Host_controls_and_persistent_panel_are_wired_for_standard_questions()
    {
        var tagHelper = ReadRepositoryFile(
            "src",
            "BadWolfQuiz.Web",
            "TagHelpers",
            "QuestionHintGameplayTagHelpers.cs");

        Assert.Contains("ResolveQuestion", tagHelper, StringComparison.Ordinal);
        Assert.Contains("JudgeQuestionAnswer", tagHelper, StringComparison.Ordinal);
        Assert.Contains("QuestionPresentationType.Standard", tagHelper, StringComparison.Ordinal);
        Assert.Contains("question-hint-reveal-button", tagHelper, StringComparison.Ordinal);
        Assert.Contains("question-hint-reveal-all-button", tagHelper, StringComparison.Ordinal);
        Assert.Contains("icon-button", tagHelper, StringComparison.Ordinal);
        Assert.Contains("Показати підказку", tagHelper, StringComparison.Ordinal);
        Assert.Contains("Показати ще одну підказку", tagHelper, StringComparison.Ordinal);
        Assert.Contains("Показати всі підказки", tagHelper, StringComparison.Ordinal);
        Assert.Contains("handler=All", tagHelper, StringComparison.Ordinal);
        Assert.Contains("question-hints-panel", tagHelper, StringComparison.Ordinal);
        Assert.Contains("question-presentation:has(> .question-hints-panel)", tagHelper, StringComparison.Ordinal);
        Assert.Contains("animation: question-hints-panel-rise", tagHelper, StringComparison.Ordinal);
        Assert.DoesNotContain("question-hints-panel-handle", tagHelper, StringComparison.Ordinal);
        Assert.DoesNotContain("question-hints-panel-title", tagHelper, StringComparison.Ordinal);
        Assert.Contains("width: calc(100% - clamp(1rem, 2vw, 2rem));", tagHelper, StringComparison.Ordinal);
        Assert.Contains("max-width: none;", tagHelper, StringComparison.Ordinal);
        Assert.Contains("box-sizing: border-box;", tagHelper, StringComparison.Ordinal);
    }

    [Fact]
    public void Gameplay_tag_helpers_do_not_consume_global_layout_or_routing_attributes()
    {
        var tagHelper = ReadRepositoryFile(
            "src",
            "BadWolfQuiz.Web",
            "TagHelpers",
            "QuestionHintGameplayTagHelpers.cs");

        Assert.DoesNotContain(
            "[HtmlAttributeName(\"class\")]",
            tagHelper,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "[HtmlAttributeName(\"asp-page-handler\")]",
            tagHelper,
            StringComparison.Ordinal);
        Assert.Contains(
            "context.AllAttributes.TryGetAttribute(\"class\"",
            tagHelper,
            StringComparison.Ordinal);
        Assert.Contains(
            "context.AllAttributes.TryGetAttribute(\"asp-page-handler\"",
            tagHelper,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Hint_reveal_endpoint_persists_runtime_state_and_returns_revealed_hint_state()
    {
        var page = ReadRepositoryFile(
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "Games",
            "QuestionHints.cshtml.cs");

        Assert.Contains("OnPostAllAsync", page, StringComparison.Ordinal);
        Assert.Contains("question.RevealNextHint(hints.Count)", page, StringComparison.Ordinal);
        Assert.Contains("question.RevealAllHints(hints.Count)", page, StringComparison.Ordinal);
        Assert.Contains("game.MarkPersistenceChanged()", page, StringComparison.Ordinal);
        Assert.Contains("return new JsonResult(new", page, StringComparison.Ordinal);
        Assert.Contains("revealedHints", page, StringComparison.Ordinal);
        Assert.Contains("revealedHintCount", page, StringComparison.Ordinal);
        Assert.Contains("totalHintCount = hints.Count", page, StringComparison.Ordinal);
        Assert.Contains("sourceQuestionId", page, StringComparison.Ordinal);
        Assert.Contains("rewardValue = question.CorrectAnswerValue", page, StringComparison.Ordinal);
        Assert.Contains("question.RevealedHintCount <= 0", page, StringComparison.Ordinal);
        Assert.Contains("Take(Math.Min(question.RevealedHintCount, hints.Count))", page, StringComparison.Ordinal);
        Assert.Contains("Response.Headers.CacheControl = \"no-store\"", page, StringComparison.Ordinal);
    }

    [Fact]
    public void Hint_client_renders_revealed_hints_without_full_gameplay_refresh()
    {
        var assets = ReadRepositoryFile(
            "src",
            "BadWolfQuiz.Web",
            "TagHelpers",
            "QuestionHintGameplayClientAssetsTagHelper.cs");

        Assert.Contains("renderHintPanel(payload)", assets, StringComparison.Ordinal);
        Assert.Contains("syncButtons(payload)", assets, StringComparison.Ordinal);
        Assert.Contains("syncReward(payload)", assets, StringComparison.Ordinal);
        Assert.Contains("payload.revealedHints", assets, StringComparison.Ordinal);
        Assert.Contains("data-question-hints-panel", assets, StringComparison.Ordinal);
        Assert.Contains("latestRevealState", assets, StringComparison.Ordinal);
        Assert.Contains("badwolf:host-gameplay-updated", assets, StringComparison.Ordinal);
        Assert.Contains("applyRevealState(latestRevealState)", assets, StringComparison.Ordinal);
        Assert.DoesNotContain("question-hints-panel-handle", assets, StringComparison.Ordinal);
        Assert.DoesNotContain("question-hints-panel-title", assets, StringComparison.Ordinal);
        Assert.Contains("width: calc(100% - clamp(1rem, 2vw, 2rem));", assets, StringComparison.Ordinal);
        Assert.Contains("max-width: none;", assets, StringComparison.Ordinal);
        Assert.Contains("box-sizing: border-box;", assets, StringComparison.Ordinal);
        Assert.Contains("width: 46px !important;", assets, StringComparison.Ordinal);
        Assert.Contains("height: 46px !important;", assets, StringComparison.Ordinal);
        Assert.Contains("flex: 0 0 46px;", assets, StringComparison.Ordinal);
        Assert.Contains("event.stopImmediatePropagation()", assets, StringComparison.Ordinal);
        Assert.Contains("setBusy(true)", assets, StringComparison.Ordinal);
        Assert.Contains("Accept\": \"application/json", assets, StringComparison.Ordinal);
        Assert.DoesNotContain("BadWolfHostGameplay?.refresh", assets, StringComparison.Ordinal);
        Assert.DoesNotContain("window.location.reload()", assets, StringComparison.Ordinal);
    }

    [Fact]
    public void Gameplay_uses_only_non_empty_text_or_image_hints()
    {
        var data = ReadRepositoryFile(
            "src",
            "BadWolfQuiz.Web",
            "Services",
            "QuestionHintGameplayData.cs");

        Assert.Contains("ContentBlockType.Text", data, StringComparison.Ordinal);
        Assert.Contains("!string.IsNullOrWhiteSpace(block.TextContent)", data, StringComparison.Ordinal);
        Assert.Contains("ContentBlockType.Image", data, StringComparison.Ordinal);
        Assert.Contains("block.HasFileData", data, StringComparison.Ordinal);
        Assert.Contains("Take(4)", data, StringComparison.Ordinal);
    }

    private static string ReadRepositoryFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                new[] { directory.FullName }
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
