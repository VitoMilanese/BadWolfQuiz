namespace BadWolfQuiz.Web.Tests;

public sealed class MinigameQuestionSearchRegressionTests
{
    [Fact]
    public void Free_question_selection_is_available_for_multiplayer_and_solo_ai()
    {
        var page = Read("src/BadWolfQuiz.Web/Pages/GuessWhatIPlay.cshtml");
        var script = Read("src/BadWolfQuiz.Web/wwwroot/js/minigames-question-search.js");
        var hub = Read("src/BadWolfQuiz.Web/Hubs/MinigameHub.cs");

        Assert.Contains("data-new-game-free-question-selection", page);
        Assert.Contains("data-question-search-dialog", page);
        Assert.Contains("data-question-search-input", page);
        Assert.Contains("minigames-question-search.js", page);
        Assert.True(
            page.IndexOf("minigames-question-search.js", StringComparison.Ordinal) <
            page.IndexOf("minigames-solo-ai.js", StringComparison.Ordinal));

        Assert.Contains("minimumQueryLength = 3", script);
        Assert.Contains("SearchAvailableQuestions", script);
        Assert.Contains("SelectQuestionByText", script);
        Assert.Contains("StartNewGameWithOptions", script);
        Assert.Contains("StartNewSoloGameWithOptions", script);
        Assert.Contains("event.stopImmediatePropagation()", script);

        Assert.Contains("public Task<MinigameRoomSnapshot> StartNewGameWithOptions(", hub);
        Assert.Contains("public Task<MinigameRoomSnapshot> StartNewSoloGameWithOptions(", hub);
        Assert.Contains("SearchAvailableQuestions(", hub);
        Assert.Contains("SelectQuestionByText(", hub);
    }

    [Fact]
    public void Search_mode_is_server_authoritative_and_ai_scores_all_remaining_questions()
    {
        var roomStore = Read("src/BadWolfQuiz.Web/Services/MinigameRoomStore.QuestionSearch.cs");
        var ai = Read("src/BadWolfQuiz.Web/Services/MinigameSoloAiService.cs");

        Assert.Contains("MinimumQuestionSearchLength = 3", roomStore);
        Assert.Contains("QuestionSearchPageSize = 10", roomStore);
        Assert.Contains("GetRemainingSearchQuestions(playerNumber)", roomStore);
        Assert.Contains("room.QuestionGame.SelectQuestion(playerNumber, question)", roomStore);

        Assert.Contains("ChooseBestSearchQuestionAsync", ai);
        Assert.Contains("GetQuestionEliminationScore", ai);
        Assert.Contains("Math.Min(yesCount, noCount)", ai);
        Assert.Contains("GetRemainingSearchQuestions", ai);
        Assert.Contains("SelectQuestionByText", ai);
    }

    [Fact]
    public void Free_selection_card_hints_have_tabs_and_search_the_remaining_question_pool()
    {
        var script = Read("src/BadWolfQuiz.Web/wwwroot/js/minigames-question-search.js");
        var hints = Read("src/BadWolfQuiz.Web/Services/MinigameHintService.cs");

        Assert.Contains("minigames-hint-tabs", script);
        Assert.Contains("minigames-hint-search-panel", script);
        Assert.Contains("hintSearchInput", script);
        Assert.Contains("'SearchCardHints'", script);
        Assert.Contains("hintSearchTotalPages", script);
        Assert.Contains("hintSearchGameKey", script);
        Assert.Contains("hintHistoryTab: root.dataset.hintsPreviousQuestions", script);
        Assert.Contains("hintCurrentSection.classList.toggle('is-hidden', Boolean(available))", script);
        Assert.Contains("hintCardsTab.textContent = available ? text.hintHistoryTab : text.hintCardsTab", script);

        Assert.Contains("MinigameQuestionSelectionMode.Search", hints);
        Assert.Contains("GetRemainingSearchQuestions(roomCode, playerToken)", hints);
        Assert.Contains("state.QuestionCardsEnabled || row.AnswerYes.HasValue", hints);
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
