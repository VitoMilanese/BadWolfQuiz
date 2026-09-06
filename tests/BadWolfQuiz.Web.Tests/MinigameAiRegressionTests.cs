namespace BadWolfQuiz.Web.Tests;

public sealed class MinigameAiRegressionTests
{
    [Fact]
    public void New_game_dialog_exposes_ai_mode_and_forces_question_cards()
    {
        var page = ReadWebFile("Pages", "GuessWhatIPlay.cshtml");
        var script = ReadWebFile("wwwroot", "js", "minigames-ai.js");

        Assert.Contains("data-new-game-ai", page);
        Assert.Contains("PlayAgainstAi", page);
        Assert.Contains("minigames-ai.js", page);
        Assert.Contains("questionCardsCheckbox.checked = true", script);
        Assert.Contains("questionCardsCheckbox.disabled = true", script);
        Assert.Contains("invokeArgs[4] = aiEnabled", script);
    }

    [Fact]
    public void Hub_and_room_store_keep_ai_server_authoritative()
    {
        var hub = ReadWebFile("Hubs", "MinigameHub.cs");
        var aiRoomStore = ReadWebFile("Services", "MinigameAiRoomStore.cs");
        var ai = ReadWebFile("Services", "MinigameAiOpponent.cs");
        var catalog = ReadWebFile("Services", "MinigameAiCatalogStore.cs");

        Assert.Contains("bool playAgainstAi = false", hub);
        Assert.Contains("MinigameAiCatalogStore", hub);
        Assert.Contains("AiMaximumCardCount", hub);
        Assert.Contains("MinigameAiRoomStore", hub);
        Assert.Contains("RunAiTurn", aiRoomStore);
        Assert.Contains("FinishDraw", aiRoomStore);
        Assert.Contains("MinimumCoveragePercent = 80", ai);
        Assert.Contains("FinalPhasePercent = 15", ai);
        Assert.Contains("HasRequiredCoverage", catalog);
    }

    [Fact]
    public void Ai_history_supports_dont_know_and_documentation_describes_strategy()
    {
        var filter = ReadWebFile("wwwroot", "js", "minigames-history-filter.js");
        var docs = ReadRepositoryFile("docs", "features", "minigames-ai-opponent.md");

        Assert.Contains("dontKnow", filter);
        Assert.Contains("80%", docs);
        Assert.Contains("15%", docs);
        Assert.Contains("Don't know", docs);
        Assert.Contains("Draw", docs);
    }

    private static string ReadWebFile(params string[] parts) =>
        ReadRepositoryFile(new[] { "src", "BadWolfQuiz.Web" }.Concat(parts).ToArray());

    private static string ReadRepositoryFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate file: {Path.Combine(parts)}");
    }
}
