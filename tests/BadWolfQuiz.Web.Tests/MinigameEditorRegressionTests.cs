namespace BadWolfQuiz.Web.Tests;

public sealed class MinigameEditorRegressionTests
{
    [Fact]
    public void Masterhost_editor_is_authorized_linked_and_uses_busy_indicator()
    {
        var model = ReadWebFile("Pages", "Admin", "MinigameEditor.cshtml.cs");
        var page = ReadWebFile("Pages", "Admin", "MinigameEditor.cshtml");
        var menuTagHelper = ReadWebFile("TagHelpers", "HeaderSeoNavigationTagHelper.cs");
        var script = ReadWebFile("wwwroot", "js", "minigame-editor.js");

        Assert.Contains("[Authorize(Policy = \"MasterHost\")]", model);
        Assert.Contains("/Admin/MinigameEditor", menuTagHelper);
        Assert.Contains("/Admin/MasterGames", menuTagHelper);
        Assert.Contains("MasterHostId", menuTagHelper);
        Assert.Contains("BadWolfBusy.navigate", menuTagHelper);
        Assert.Contains("data-minigame-editor-busy", page);
        Assert.Contains("data-minigame-editor-nav", page);
        Assert.Contains("BadWolfBusy.show()", script);
        Assert.Contains("BadWolfBusy.navigate", script);
    }

    [Fact]
    public void Editor_preserves_workflows_and_uses_branded_responsive_presentation()
    {
        var page = ReadWebFile("Pages", "Admin", "MinigameEditor.cshtml");
        var model = ReadWebFile("Pages", "Admin", "MinigameEditor.cshtml.cs");
        var css = ReadWebFile("wwwroot", "css", "minigame-editor.css");
        var resourceSyncCss = ReadWebFile("wwwroot", "css", "minigame-resource-sync.css");
        var script = ReadWebFile("wwwroot", "js", "minigame-editor.js");
        var store = ReadWebFile("Services", "MinigameCatalogStore.cs");
        var answerFileParser = ReadWebFile("Services", "MinigameAnswerFileParser.cs");

        Assert.Contains("asp-page-handler=\"CreateGame\"", page);
        Assert.Contains("asp-page-handler=\"UpdateGame\"", page);
        Assert.Contains("asp-page-handler=\"DeleteGame\"", page);
        Assert.Contains("asp-page-handler=\"CreateQuestion\"", page);
        Assert.Contains("asp-page-handler=\"UpdateQuestion\"", page);
        Assert.Contains("asp-page-handler=\"DeleteQuestion\"", page);
        Assert.Contains("name=\"enabled\"", page);
        Assert.Contains("QuestionEnabled", page);
        Assert.Contains("DisabledQuestionIds", model);
        Assert.Contains("SetEnabledAsync", model);
        Assert.Contains("asp-page-handler=\"SaveAnswers\"", page);
        Assert.Contains("asp-page-handler=\"ImportAnswers\"", page);
        Assert.Contains("asp-page-handler=\"ExportAnswers\"", page);
        Assert.Contains("data-minigame-answer-form", page);
        Assert.Contains("data-minigame-answer-select", page);
        Assert.DoesNotContain("answers[@index].QuestionId", page);
        Assert.DoesNotContain("minigame-editor-answer-actions", page);
        Assert.Contains("answersJson", model);
        Assert.Contains("JsonSerializer.Deserialize", model);
        Assert.Contains("OnGetExportAnswersAsync", model);
        Assert.DoesNotContain("answers.Any(answer => !answer.AnswerYes.HasValue)", model);
        Assert.Contains("null => string.Empty", model);
        Assert.Contains("MinigameAnswerFileParser.Parse", model);
        Assert.Contains("SaveAnswersAsync(gameId, values", model);
        Assert.Contains("value.Length == 0", answerFileParser);
        Assert.Contains("IReadOnlyList<bool?> Answers", answerFileParser);
        Assert.Contains("JSON.stringify(buildPayload())", script);
        Assert.Contains("fetch(answerForm.action", script);
        Assert.Contains("keepalive: true", script);
        Assert.Contains("flushMinigameEditorAnswers", script);
        Assert.Contains("await flushMinigameEditorAnswers()", script);
        Assert.Contains("activeAnswerFilter", script);
        Assert.Contains("dataset.minigameAnswerFilter", script);
        Assert.Contains("select.value === activeAnswerFilter", script);
        Assert.Contains("row.hidden = !visible", script);
        Assert.Contains("applyAnswerFilter()", script);

        Assert.Contains("private const int PageSize = 25;", model);
        Assert.Contains("GetGamesPageAsync", model);
        Assert.Contains("GetQuestionItemsPageAsync", model);
        Assert.Contains("GetAnswerItemsPageAsync", model);
        Assert.Contains("UpdateAnswersAsync(gameId, values", model);
        Assert.Contains("MinigameEditorPagination", model);
        Assert.Contains("name=\"pageNumber\"", page);
        Assert.Contains("asp-route-pageNumber", page);
        Assert.Contains("minigame-editor-pager", page);
        Assert.Contains("LIMIT $take OFFSET $skip", store);
        Assert.Contains("ON CONFLICT(GameId, QuestionId)", store);

        Assert.Contains("const pageSize = 25;", script);
        Assert.Contains("buildPageNumbers", script);
        Assert.Contains("dataset.minigameEditorPageNumbers", script);
        Assert.Contains("data-minigame-editor-pager-position=\"top\"", script);
        Assert.Contains("content.before(topPager)", script);
        Assert.Contains("new DOMParser().parseFromString", script);
        Assert.Contains("replacePagedContent", script);
        Assert.Contains("window.history.pushState", script);
        Assert.Contains("window.history.replaceState", script);
        Assert.Contains("window.addEventListener('popstate'", script);
        Assert.Contains("window.scrollTo(0, previousScrollY)", script);
        Assert.Contains("fetch(href", script);
        Assert.Contains("'X-Requested-With': 'XMLHttpRequest'", script);

        Assert.Contains("body:has(.minigame-editor-heading) > .page-shell", css);
        Assert.Contains("overflow-y: auto;", css);
        Assert.Contains("overscroll-behavior-y: contain;", css);
        Assert.Contains("@media (max-width: 700px)", css);
        Assert.Contains("overflow-y: visible;", css);
        Assert.Contains(".minigame-editor-heading::after", css);
        Assert.Contains("content: \"EDITOR\"", css);
        Assert.Contains(".minigame-editor-counts::before", css);
        Assert.Contains(".minigame-editor-tabs > .button.button-primary", css);
        Assert.Contains(".minigame-editor-game-card", css);
        Assert.Contains(
            "grid-template-columns: clamp(150px, 12vw, 190px) minmax(0, 1fr) 52px",
            css);
        Assert.Contains(".minigame-editor-question-list > li:nth-child(even)", css);
        Assert.Contains(".minigame-editor-question-list > li.is-disabled", css);
        Assert.Contains(".minigame-editor-question-enabled input[type=\"checkbox\"]:checked", css);
        Assert.Contains(".minigame-editor-answer-table tbody tr:nth-child(even) > td", css);
        Assert.Contains(".minigame-editor-answer-table td + td", css);
        Assert.Contains(".minigame-editor-answer-filter", css);
        Assert.Contains(".minigame-editor-answer-form.is-answer-filtered", css);
        Assert.Contains(".minigame-editor-pager", css);
        Assert.DoesNotContain(".minigame-editor-answer-actions", css);
        Assert.Contains("@media (max-width: 760px)", css);
        Assert.Contains("@media (prefers-reduced-motion: reduce)", css);

        Assert.Contains(".minigame-resource-sync-panel::before", resourceSyncCss);
        Assert.Contains("content: \"SYNC\"", resourceSyncCss);
        Assert.Contains(".minigame-resource-cleanup-dialog::backdrop", resourceSyncCss);
        Assert.Contains(".minigame-resource-delete-confirm-content::before", resourceSyncCss);
        Assert.Contains("@media (max-width: 760px)", resourceSyncCss);
        Assert.Contains("@media (prefers-reduced-motion: reduce)", resourceSyncCss);

        Assert.Contains("expectedCount", store);
        Assert.Contains("value == \"1\"", store);
        Assert.Contains("value == \"0\"", store);
        Assert.Contains("BeginTransactionAsync", store);
    }

    [Fact]
    public void Runtime_reads_minigame_catalog_and_only_enabled_question_cards()
    {
        var hub = ReadWebFile("Hubs", "MinigameHub.cs");
        var gamePage = ReadWebFile("Pages", "GuessWhatIPlay.cshtml.cs");
        var catalogPage = ReadWebFile("Pages", "Minigames.cshtml.cs");
        var store = ReadWebFile("Services", "MinigameCatalogStore.cs");
        var availability = ReadWebFile("Services", "MinigameQuestionAvailabilityStore.cs");
        var migration = ReadWebFile("Migrations", "20260831090000_AddMinigameCatalogTables.cs");
        var availabilityMigration = ReadWebFile(
            "Migrations",
            "20260831130000_AddMinigameDisabledQuestions.cs");

        Assert.Contains("MinigameCatalogStore", hub);
        Assert.Contains("GetCountsAsync", hub);
        Assert.Contains("GenerateCardsAsync", hub);
        Assert.Contains("MinigameQuestionAvailabilityStore", hub);
        Assert.Contains("GetEnabledQuestionCountAsync", hub);
        Assert.Contains("GetEnabledQuestionsAsync", hub);
        Assert.Contains("MinigameCatalogStore", gamePage);
        Assert.Contains("GetGameImageAsync", gamePage);
        Assert.DoesNotContain("PhysicalFile", gamePage);
        Assert.Contains("MinigameCatalogStore", catalogPage);
        Assert.Contains("20260831090000_AddMinigameCatalogTables", migration);
        Assert.Contains("CREATE TABLE IF NOT EXISTS MinigameCatalogGames", migration);
        Assert.Contains("CREATE TABLE IF NOT EXISTS MinigameCatalogQuestions", migration);
        Assert.Contains("CREATE TABLE IF NOT EXISTS MinigameCatalogAnswers", migration);
        Assert.Contains("MinigameDisabledQuestions", availability);
        Assert.Contains("WHERE d.QuestionId IS NULL", availability);
        Assert.Contains("20260831130000_AddMinigameDisabledQuestions", availabilityMigration);
        Assert.Contains("CREATE TABLE IF NOT EXISTS MinigameDisabledQuestions", availabilityMigration);
        Assert.Contains("questions.txt", store);
        Assert.Contains("TryParseFile", store);
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
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate BadWolfQuiz.Web file: {Path.Combine(parts)}");
    }
}
