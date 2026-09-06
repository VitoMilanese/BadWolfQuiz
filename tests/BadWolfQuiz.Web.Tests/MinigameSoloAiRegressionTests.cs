namespace BadWolfQuiz.Web.Tests;

public sealed class MinigameSoloAiRegressionTests
{
    [Fact]
    public void Solo_ai_ui_forces_question_cards_and_uses_dedicated_start_contract()
    {
        var page = Read("src/BadWolfQuiz.Web/Pages/GuessWhatIPlay.cshtml");
        var script = Read("src/BadWolfQuiz.Web/wwwroot/js/minigames-solo-ai.js");
        var hub = Read("src/BadWolfQuiz.Web/Hubs/MinigameHub.cs");

        Assert.Contains("data-new-game-solo-ai", page);
        Assert.Contains("SoloAiDescription", page);
        Assert.Contains("minigames-solo-ai.js", page);
        Assert.True(
            page.IndexOf("minigames-solo-ai.js", StringComparison.Ordinal) <
            page.IndexOf("minigames-hints.js", StringComparison.Ordinal));

        Assert.Contains("newGameQuestionCards.checked = true", script);
        Assert.Contains("newGameQuestionCards.disabled = true", script);
        Assert.Contains("'StartNewSoloGame'", script);
        Assert.Contains("event.stopImmediatePropagation()", script);
        Assert.Contains("GetSoloAiAvailability", script);
        Assert.Contains("GetSoloAiStatus", script);
        Assert.Contains("SoloAiUnknownAnswer", page);

        Assert.Contains("public Task<MinigameRoomSnapshot> StartNewSoloGame(", hub);
        Assert.Contains("questionCardsEnabled: true", hub);
        Assert.Contains("SoloAi.StartSoloGameAsync", hub);
        Assert.Contains("SoloAi.AdvanceAsync", hub);
    }

    [Fact]
    public void Solo_ai_server_filters_games_and_keeps_ai_identity_server_side()
    {
        var service = Read("src/BadWolfQuiz.Web/Services/MinigameSoloAiService.cs");
        var roomStore = Read("src/BadWolfQuiz.Web/Services/MinigameRoomStore.Solo.cs");

        Assert.Contains("MinimumAnswerCoveragePercent = 80", service);
        Assert.Contains("game.AssignedAnswerCount", service);
        Assert.Contains("counts.QuestionCount", service);
        Assert.Contains("EnsureSoloOpponent", service);
        Assert.Contains("RandomNumberGenerator.GetInt32", service);
        Assert.Contains("PendingQuestionResponsePlayerNumber == 2", service);
        Assert.Contains("GetAnswerForGameAsync", service);
        Assert.Contains("UnknownAnswerHistoryIndexes", service);
        Assert.Contains("GetAssignedAnswersForQuestionAsync", service);
        Assert.Contains("solo.Candidates", service);

        Assert.Contains("private readonly Dictionary<string, string> _soloOpponentTokens", roomStore);
        Assert.Contains("RoomFull", roomStore);
        Assert.Contains("RemoveSoloOpponent", roomStore);
        Assert.DoesNotContain("AiPlayerToken", Read("src/BadWolfQuiz.Web/Services/MinigameRoomStore.cs"));
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
