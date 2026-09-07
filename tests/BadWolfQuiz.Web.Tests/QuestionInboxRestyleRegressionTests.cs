namespace BadWolfQuiz.Web.Tests;

public sealed class QuestionInboxRestyleRegressionTests
{
    [Fact]
    public void Question_inbox_uses_dedicated_admin_presentation()
    {
        var markup = ReadWebFile("Pages", "Admin", "QuestionInbox.cshtml");

        Assert.Contains("@model BadWolfQuiz.Web.Pages.Admin.QuestionInboxModel", markup, StringComparison.Ordinal);
        Assert.Contains("~/css/question-inbox.css", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"question-inbox-page\"", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"question-inbox-hero\"", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"question-inbox-count\"", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"question-inbox-empty\"", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"question-inbox-list\"", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"question-inbox-card", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("<style>", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Existing_localized_conversation_and_action_copy_is_preserved()
    {
        var markup = ReadWebFile("Pages", "Admin", "QuestionInbox.cshtml");

        Assert.Contains("Localizer[\"QuestionInbox_Title\"]", markup, StringComparison.Ordinal);
        Assert.Contains("Localizer[\"QuestionInbox_Empty\"]", markup, StringComparison.Ordinal);
        Assert.Contains("Localizer[\"QuestionInbox_Question\"]", markup, StringComparison.Ordinal);
        Assert.Contains("Localizer[\"QuestionInbox_From\"]", markup, StringComparison.Ordinal);
        Assert.Contains("Localizer[\"QuestionInbox_User\"]", markup, StringComparison.Ordinal);
        Assert.Contains("Localizer[\"QuestionInbox_Developer\"]", markup, StringComparison.Ordinal);
        Assert.Contains("Localizer[\"QuestionInbox_Answer\"]", markup, StringComparison.Ordinal);
        Assert.Contains("Localizer[\"QuestionInbox_Reply\"]", markup, StringComparison.Ordinal);
        Assert.Contains("Localizer[\"QuestionBot_Title\"]", markup, StringComparison.Ordinal);
        Assert.Contains("Localizer[\"Button_Delete\"]", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Reply_delete_and_question_bot_contracts_are_preserved()
    {
        var markup = ReadWebFile("Pages", "Admin", "QuestionInbox.cshtml");

        Assert.Contains("asp-page-handler=\"Answer\"", markup, StringComparison.Ordinal);
        Assert.Contains("name=\"id\" value=\"@question.Id\"", markup, StringComparison.Ordinal);
        Assert.Contains("name=\"answer\"", markup, StringComparison.Ordinal);
        Assert.Contains("maxlength=\"5000\"", markup, StringComparison.Ordinal);
        Assert.Contains("asp-page-handler=\"Delete\"", markup, StringComparison.Ordinal);
        Assert.Contains("data-delete-question", markup, StringComparison.Ordinal);
        Assert.Contains("asp-page-handler=\"SaveQuestionBot\"", markup, StringComparison.Ordinal);
        Assert.Contains("data-question-bot-guild", markup, StringComparison.Ordinal);
        Assert.Contains("data-question-bot-channel", markup, StringComparison.Ordinal);
        Assert.Contains("?handler=QuestionBotChannels&guildId=", markup, StringComparison.Ordinal);
        Assert.Contains("data-open-on-load=\"@openQuestionBotSettings.ToString().ToLowerInvariant()\"", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Dedicated_styles_cover_cards_dialogs_and_responsiveness()
    {
        var styles = ReadWebFile("wwwroot", "css", "question-inbox.css")
            .ReplaceLineEndings("\n");

        Assert.Contains("body.portal-layout:has(.question-inbox-page) > .page-shell", styles, StringComparison.Ordinal);
        Assert.Contains(".question-inbox-hero {", styles, StringComparison.Ordinal);
        Assert.Contains(".question-inbox-card {", styles, StringComparison.Ordinal);
        Assert.Contains(".question-messages {", styles, StringComparison.Ordinal);
        Assert.Contains(".question-message-user {", styles, StringComparison.Ordinal);
        Assert.Contains(".question-message-developer {", styles, StringComparison.Ordinal);
        Assert.Contains(".question-inbox-dialog {", styles, StringComparison.Ordinal);
        Assert.Contains("overflow-wrap: anywhere;", styles, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 1180px)", styles, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 820px)", styles, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 620px)", styles, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 420px)", styles, StringComparison.Ordinal);
        Assert.Contains(":focus-visible", styles, StringComparison.Ordinal);
        Assert.Contains("@media (prefers-reduced-motion: reduce)", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void Page_model_keeps_filtering_ordering_and_existing_handlers()
    {
        var model = ReadWebFile("Pages", "Admin", "QuestionInbox.cshtml.cs");

        Assert.Contains("[BindProperty(SupportsGet = true)]", model, StringComparison.Ordinal);
        Assert.Contains("public int? QuestionId { get; set; }", model, StringComparison.Ordinal);
        Assert.Contains("public JsonResult OnGetQuestionBotChannels(string guildId)", model, StringComparison.Ordinal);
        Assert.Contains("public async Task<IActionResult> OnPostSaveQuestionBotAsync", model, StringComparison.Ordinal);
        Assert.Contains("public async Task<IActionResult> OnPostAnswerAsync", model, StringComparison.Ordinal);
        Assert.Contains("public async Task<IActionResult> OnPostDeleteAsync", model, StringComparison.Ordinal);
        Assert.Contains("query = query.Where(x => x.Id == QuestionId.Value);", model, StringComparison.Ordinal);
        Assert.Contains(".ThenByDescending(x => x.UpdatedAtUtc)", model, StringComparison.Ordinal);
        Assert.Contains("return RedirectToPage(new { questionId = id });", model, StringComparison.Ordinal);
        Assert.Contains("openQuestionBotSettings = true", model, StringComparison.Ordinal);
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
