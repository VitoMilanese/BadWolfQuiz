namespace BadWolfQuiz.Web.Tests;

public sealed class MinigameHintsRegressionTests
{
    [Fact]
    public void Guess_what_i_play_wires_optional_hint_ui_without_replacing_manual_answers()
    {
        var page = Read("src/BadWolfQuiz.Web/Pages/GuessWhatIPlay.cshtml");
        var script = Read("src/BadWolfQuiz.Web/wwwroot/js/minigames-hints.js");
        var existingGameplay = Read("src/BadWolfQuiz.Web/wwwroot/js/minigames.js");

        Assert.Contains("data-new-game-allow-hints", page);
        Assert.Contains("data-card-hint-dialog", page);
        Assert.Contains("data-question-response-hint", page);
        Assert.Contains("minigames-hints.css", page);
        Assert.Contains("minigames-hints.js", page);

        Assert.Contains("event.stopImmediatePropagation()", script);
        Assert.Contains("'StartNewGameWithHints'", script);
        Assert.Contains("newGameAllowHints.checked", script);
        Assert.DoesNotContain(
            "newGameQuestionCards.checked && newGameAllowHints.checked",
            script);
        Assert.Contains("newGameAllowHints.disabled = false", script);
        Assert.Contains("questionCardsEnabledOf", script);
        Assert.Contains("if (!questionCardsEnabledOf(currentState))", script);
        Assert.Contains("searchInput.minLength = minimumSearchLength", script);
        Assert.Contains("'SearchCardHints'", script);
        Assert.Contains("runBusy", script);
        Assert.Contains("'GetCardHints'", script);
        Assert.Contains("'GetQuestionResponseHint'", script);
        Assert.Contains("event.stopPropagation()", script);
        Assert.Contains("data-minigame-hint-trigger", script);

        Assert.Contains("'SubmitQuestionResponse'", existingGameplay);
        Assert.Contains("questionResponseYes.addEventListener", existingGameplay);
        Assert.Contains("questionResponseNo.addEventListener", existingGameplay);
    }

    [Fact]
    public void Hint_server_contract_preserves_legacy_start_and_searches_only_answered_questions()
    {
        var hub = Read("src/BadWolfQuiz.Web/Hubs/MinigameHub.cs");
        var service = Read("src/BadWolfQuiz.Web/Services/MinigameHintService.cs");

        Assert.Contains("public Task<MinigameRoomSnapshot> StartNewGame(", hub);
        Assert.Contains("public Task<MinigameRoomSnapshot> StartNewGameWithHints(", hub);
        Assert.Contains("Hints.SetEnabled(state.RoomCode, hintsEnabled)", hub);
        Assert.DoesNotContain("questionCardsEnabled && hintsEnabled", hub);
        Assert.Contains("SearchCardHints", hub);
        Assert.Contains("GetHintsEnabled", hub);
        Assert.Contains("GetCardHints", hub);
        Assert.Contains("GetQuestionResponseHint", hub);

        Assert.Contains("MinimumSearchLength = 3", service);
        Assert.Contains("SearchPageSize = 10", service);
        Assert.Contains("state.QuestionCardsEnabled", service);
        Assert.Contains("row.AnswerYes.HasValue", service);
        Assert.Contains("StringComparison.OrdinalIgnoreCase", service);
        Assert.Contains("entry.PlayerNumber == state.PlayerNumber", service);
        Assert.Contains("state.MySecretCardFileName", service);
        Assert.DoesNotContain("OpponentSecret", service);
    }

    [Fact]
    public void Hint_styles_reveal_info_control_and_support_paged_search_dialog()
    {
        var css = Read("src/BadWolfQuiz.Web/wwwroot/css/minigames-hints.css");

        Assert.Contains(".minigame-card-hint-trigger", css);
        Assert.Contains("position: absolute", css);
        Assert.Contains(".minigame-card:hover .minigame-card-hint-trigger", css);
        Assert.Contains(".minigame-card:focus-within .minigame-card-hint-trigger", css);
        Assert.Contains(".minigames-hint-dialog", css);
        Assert.Contains(".minigames-hint-search-form", css);
        Assert.Contains(".minigames-hint-search-paging", css);
        Assert.Contains(".minigames-question-response-hint", css);
    }

    private static string Read(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate repository file: {relativePath}");
    }
}
