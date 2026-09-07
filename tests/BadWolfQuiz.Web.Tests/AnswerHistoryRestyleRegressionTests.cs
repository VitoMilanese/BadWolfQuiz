namespace BadWolfQuiz.Web.Tests;

public sealed class AnswerHistoryRestyleRegressionTests
{
    [Fact]
    public void Answer_history_keeps_session_route_and_uses_dedicated_presentation()
    {
        var markup = ReadWebFile("Pages", "Admin", "Games", "AnswerHistory.cshtml");

        Assert.Contains("@page \"{id:guid}\"", markup, StringComparison.Ordinal);
        Assert.Contains("@model BadWolfQuiz.Web.Pages.Admin.Games.AnswerHistoryModel", markup, StringComparison.Ordinal);
        Assert.Contains("~/css/answer-history.css", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"answer-history-page\"", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"answer-history-hero\"", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"answer-history-session\"", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"answer-history-content\"", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Model.Game.PublicCode", markup, StringComparison.Ordinal);
        Assert.Contains("Model.Game.Session.Quiz.Title", markup, StringComparison.Ordinal);
        Assert.Contains("@((questionIndex + 1).ToString(\"00\"))", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("@(questionIndex + 1).ToString(\"00\")", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Hero_and_primary_labels_continue_to_use_existing_localization()
    {
        var markup = ReadWebFile("Pages", "Admin", "Games", "AnswerHistory.cshtml");

        Assert.Contains("Localizer[\"AnswerHistory_Eyebrow\"]", markup, StringComparison.Ordinal);
        Assert.Contains("Localizer[\"AnswerHistory_Title\"]", markup, StringComparison.Ordinal);
        Assert.Contains("Localizer[\"AnswerHistory_Description\"]", markup, StringComparison.Ordinal);
        Assert.Contains("Localizer[\"AnswerHistory_BackToGame\"]", markup, StringComparison.Ordinal);
        Assert.Contains("Localizer[\"AnswerHistory_AddEntry\"]", markup, StringComparison.Ordinal);
        Assert.Contains("Localizer[\"AnswerHistory_Question\"]", markup, StringComparison.Ordinal);
        Assert.Contains("Localizer[\"AnswerHistory_Player\"]", markup, StringComparison.Ordinal);
        Assert.Contains("Localizer[\"AnswerHistory_Value\"]", markup, StringComparison.Ordinal);
        Assert.Contains("Localizer[\"AnswerHistory_Correct\"]", markup, StringComparison.Ordinal);
        Assert.DoesNotContain(">MATCH<", markup, StringComparison.Ordinal);
        Assert.DoesNotContain(">CONTROL ROOM<", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Existing_add_update_delete_form_contracts_are_preserved()
    {
        var markup = ReadWebFile("Pages", "Admin", "Games", "AnswerHistory.cshtml");

        Assert.Contains("asp-page-handler=\"Add\"", markup, StringComparison.Ordinal);
        Assert.Contains("asp-page-handler=\"Update\"", markup, StringComparison.Ordinal);
        Assert.Contains("asp-page-handler=\"Delete\"", markup, StringComparison.Ordinal);
        Assert.Contains("name=\"sourceQuestionId\"", markup, StringComparison.Ordinal);
        Assert.Contains("name=\"attemptId\"", markup, StringComparison.Ordinal);
        Assert.Contains("name=\"playerId\"", markup, StringComparison.Ordinal);
        Assert.Contains("name=\"value\"", markup, StringComparison.Ordinal);
        Assert.Contains("name=\"isCorrect\"", markup, StringComparison.Ordinal);
        Assert.Contains("data-add-answer-history", markup, StringComparison.Ordinal);
        Assert.Contains("data-delete-answer-history", markup, StringComparison.Ordinal);
        Assert.Contains("resolveQuestionIfAvailable", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Back_navigation_and_both_existing_dialog_flows_are_preserved()
    {
        var markup = ReadWebFile("Pages", "Admin", "Games", "AnswerHistory.cshtml");

        Assert.Contains("data-answer-history-back-to-game", markup, StringComparison.Ordinal);
        Assert.Contains("asp-page=\"/Admin/Games/Lobby\"", markup, StringComparison.Ordinal);
        Assert.Contains("asp-route-id=\"@Model.Game.Session.Id.Value\"", markup, StringComparison.Ordinal);
        Assert.Contains("event.key !== \"Escape\"", markup, StringComparison.Ordinal);
        Assert.Contains("document.querySelector(\"dialog[open]\")", markup, StringComparison.Ordinal);
        Assert.Contains("backToGame.click();", markup, StringComparison.Ordinal);

        Assert.Contains("id=\"delete-answer-history-dialog\"", markup, StringComparison.Ordinal);
        Assert.Contains("id=\"answer-history-resolve-question-dialog\"", markup, StringComparison.Ordinal);
        Assert.Contains("data-answer-history-resolve-yes", markup, StringComparison.Ordinal);
        Assert.Contains("data-answer-history-resolve-no", markup, StringComparison.Ordinal);
        Assert.Contains("resolveDialog.showModal();", markup, StringComparison.Ordinal);
        Assert.Contains("deleteDialog.showModal();", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Dedicated_styles_cover_full_width_cards_forms_responsiveness_and_accessibility()
    {
        var styles = ReadWebFile("wwwroot", "css", "answer-history.css")
            .ReplaceLineEndings("\n");

        Assert.Contains(
            "body.portal-layout:has(.answer-history-page) > .page-shell",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(".answer-history-hero {", styles, StringComparison.Ordinal);
        Assert.Contains(".answer-history-session {", styles, StringComparison.Ordinal);
        Assert.Contains(".answer-history-add-card {", styles, StringComparison.Ordinal);
        Assert.Contains(".answer-history-question-card {", styles, StringComparison.Ordinal);
        Assert.Contains(".answer-history-page .answer-history-entry-form {", styles, StringComparison.Ordinal);
        Assert.Contains(".answer-history-dialog-card {", styles, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 1180px)", styles, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 820px)", styles, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 620px)", styles, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 420px)", styles, StringComparison.Ordinal);
        Assert.Contains(":focus-visible", styles, StringComparison.Ordinal);
        Assert.Contains("@media (prefers-reduced-motion: reduce)", styles, StringComparison.Ordinal);
        Assert.DoesNotContain("content: \"MATCH", styles, StringComparison.Ordinal);
        Assert.DoesNotContain("content: \"HISTORY", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void Page_model_keeps_host_owned_score_history_and_realtime_update_behavior()
    {
        var model = ReadWebFile("Pages", "Admin", "Games", "AnswerHistory.cshtml.cs");

        Assert.Contains("sessionRegistry.FindOwned(new GameSessionId(id), currentHost.RequiredId)", model, StringComparison.Ordinal);
        Assert.Contains("sessionRegistry.AddQuestionAnswerHistoryEntry(", model, StringComparison.Ordinal);
        Assert.Contains("sessionRegistry.UpdateQuestionAnswerHistoryEntry(", model, StringComparison.Ordinal);
        Assert.Contains("sessionRegistry.RemoveQuestionAnswerHistoryEntry(", model, StringComparison.Ordinal);
        Assert.Contains("private static bool IsValidHistoryValue(int value) =>\n        value > 0;", model.ReplaceLineEndings("\n"), StringComparison.Ordinal);
        Assert.Contains("\"PlayersChanged\"", model, StringComparison.Ordinal);
        Assert.Contains("\"BuzzerStateChanged\"", model, StringComparison.Ordinal);
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

        throw new FileNotFoundException($"Could not locate web file: {string.Join('/', parts)}");
    }
}
