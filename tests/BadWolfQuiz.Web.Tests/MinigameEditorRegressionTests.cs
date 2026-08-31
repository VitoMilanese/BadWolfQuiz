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
    public void Editor_uses_horizontal_game_cards_and_non_blocking_answer_autosave()
    {
        var page = ReadWebFile("Pages", "Admin", "MinigameEditor.cshtml");
        var model = ReadWebFile("Pages", "Admin", "MinigameEditor.cshtml.cs");
        var css = ReadWebFile("wwwroot", "css", "minigame-editor.css");
        var script = ReadWebFile("wwwroot", "js", "minigame-editor.js");
        var store = ReadWebFile("Services", "MinigameCatalogStore.cs");

        Assert.Contains("asp-page-handler=\"CreateGame\"", page);
        Assert.Contains("asp-page-handler=\"UpdateGame\"", page);
        Assert.Contains("asp-page-handler=\"DeleteGame\"", page);
        Assert.Contains("asp-page-handler=\"CreateQuestion\"", page);
        Assert.Contains("asp-page-handler=\"UpdateQuestion\"", page);
        Assert.Contains("asp-page-handler=\"DeleteQuestion\"", page);
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
        Assert.Contains("JSON.stringify(buildPayload())", script);
        Assert.Contains("fetch(answerForm.action", script);
        Assert.Contains("activeAnswerFilter", script);
        Assert.Contains("data.minigameAnswerFilter", script);
        Assert.Contains("select.value === activeAnswerFilter", script);
        Assert.Contains("row.hidden = !visible", script);
        Assert.Contains("applyAnswerFilter()", script);
        Assert.Contains("grid-template-columns: 112px minmax(0, 1fr) auto", css);
        Assert.Contains(".minigame-editor-question-list > li:nth-child(even)", css);
        Assert.Contains(".minigame-editor-answer-table tbody tr:nth-child(even) > td", css);
        Assert.Contains(".minigame-editor-answer-table td + td", css);
        Assert.Contains(".minigame-editor-answer-filter", css);
        Assert.Contains(".minigame-editor-answer-form.is-answer-filtered", css);
        Assert.DoesNotContain(".minigame-editor-answer-actions", css);
        Assert.Contains("MinigameAnswerImportParser.Parse", model);
        Assert.Contains("expectedCount", store);
        Assert.Contains("value == \"1\"", store);
        Assert.Contains("value == \"0\"", store);
        Assert.Contains("BeginTransactionAsync", store);
    }

    [Fact]
    public void Runtime_reads_minigame_catalog_and_images_from_database_store()
    {
        var hub = ReadWebFile("Hubs", "MinigameHub.cs");
        var gamePage = ReadWebFile("Pages", "GuessWhatIPlay.cshtml.cs");
        var catalogPage = ReadWebFile("Pages", "Minigames.cshtml.cs");
        var store = ReadWebFile("Services", "MinigameCatalogStore.cs");
        var migration = ReadWebFile("Migrations", "20260831090000_AddMinigameCatalogTables.cs");

        Assert.Contains("MinigameCatalogStore", hub);
        Assert.Contains("GetCountsAsync", hub);
        Assert.Contains("GenerateCardsAsync", hub);
        Assert.Contains("GetQuestionsAsync", hub);
        Assert.Contains("MinigameCatalogStore", gamePage);
        Assert.Contains("GetGameImageAsync", gamePage);
        Assert.DoesNotContain("PhysicalFile", gamePage);
        Assert.Contains("MinigameCatalogStore", catalogPage);
        Assert.Contains("20260831090000_AddMinigameCatalogTables", migration);
        Assert.Contains("CREATE TABLE IF NOT EXISTS MinigameCatalogGames", migration);
        Assert.Contains("CREATE TABLE IF NOT EXISTS MinigameCatalogQuestions", migration);
        Assert.Contains("CREATE TABLE IF NOT EXISTS MinigameCatalogAnswers", migration);
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
