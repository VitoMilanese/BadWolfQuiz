using BadWolfQuiz.Web.Services;

namespace BadWolfQuiz.Web.Tests;

public sealed class MinigamesRegressionTests
{
    [Fact]
    public void Main_menu_and_runtime_expose_room_based_minigames()
    {
        var layout = ReadWebFile("Pages", "Shared", "_Layout.cshtml");
        var program = ReadWebFile("Program.cs");

        Assert.Contains("asp-page=\"/Minigames\"", layout);
        Assert.Contains("Menu_Minigames", layout);
        Assert.Contains("AddOptions<MinigameOptions>()", program);
        Assert.Contains("MinigameCardSetStore", program);
        Assert.Contains("MinigameQuestionStore", program);
        Assert.Contains("AddSingleton<MinigameRoomStore>()", program);
        Assert.Contains("AddHostedService<MinigameRoomCleanupService>()", program);
        Assert.Contains("MapHub<MinigameHub>(\"/hubs/minigames\")", program);
    }

    [Fact]
    public void Minigames_menu_opens_catalog_before_the_game()
    {
        var catalog = ReadWebFile("Pages", "Minigames.cshtml");
        var game = ReadWebFile("Pages", "GuessWhatIPlay.cshtml");
        var catalogStyles = ReadWebFile("wwwroot", "css", "minigames-catalog.css");
        var illustration = ReadWebFile(
            "wwwroot",
            "images",
            "minigames",
            "guess-what-i-play.svg");

        Assert.Contains("asp-page=\"/GuessWhatIPlay\"", catalog);
        Assert.Contains("GuessWhatIPlay_Title", catalog);
        Assert.Contains("aspect-ratio: 16 / 9", catalogStyles);
        Assert.Contains("@page \"/minigames/guess-what-i-play\"", game);
        Assert.Contains("data-minigames-root", game);
        Assert.DoesNotContain("<text", illustration, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Game_exposes_copyable_room_links_with_social_metadata()
    {
        var page = ReadWebFile("Pages", "GuessWhatIPlay.cshtml");
        var sharing = ReadWebFile("wwwroot", "js", "minigames-sharing.js");
        var sharingStyles = ReadWebFile("wwwroot", "css", "minigames-sharing.css");

        Assert.Contains("data-copy-room-link", page);
        Assert.Contains("SocialTitle", page);
        Assert.Contains("SocialDescription", page);
        Assert.Contains("SocialImageVariant", page);
        Assert.Contains("GuessWhatIPlay_RoomSocialTitle", page);
        Assert.Contains("searchParams.set('room', roomCode)", sharing);
        Assert.Contains("navigator.clipboard", sharing);
        Assert.Contains("TouchRoom", sharing);
        Assert.Contains("badwolf-minigame-player:", sharing);
        Assert.Contains("minigames-copy-room-link", sharingStyles);
    }

    [Fact]
    public void Default_new_game_count_is_ten_when_not_configured()
    {
        var options = new MinigameOptions();

        Assert.Equal(10, options.CardCount);
    }

    [Fact]
    public void Page_supports_room_creation_join_and_new_game_setup()
    {
        var page = ReadWebFile("Pages", "GuessWhatIPlay.cshtml");
        var script = ReadWebFile("wwwroot", "js", "minigames.js");

        Assert.Contains("data-create-room", page);
        Assert.Contains("data-join-room-form", page);
        Assert.Contains("data-new-game-dialog", page);
        Assert.Contains("data-new-game-count", page);
        Assert.Contains("data-new-game-question-cards", page);
        Assert.Contains("data-question-panel", page);
        Assert.Contains("data-history-guess-correct", page);
        Assert.Contains("data-history-guess-incorrect", page);
        Assert.Contains("data-history-turn-ended", page);
        Assert.Contains("data-history-turn-timed-out", page);
        Assert.Contains("CreateRoom", script);
        Assert.Contains("JoinRoom", script);
        Assert.Contains("StartNewGame", script);
        Assert.Contains("EndTurn", script);
        Assert.Contains("ExpireTurn", script);
        Assert.Contains("SubmitGuess", script);
        Assert.Contains("SelectQuestion", script);
        Assert.Contains("SubmitQuestionResponse", script);
        Assert.Contains("RestartGame", script);
        Assert.Contains("data-question-response-dialog", page);
        Assert.Contains("data-restart-game", page);
        Assert.Contains("historyKind", script);
        Assert.Contains("historyGuessCorrect", script);
        Assert.Contains("historyQuestionAnswer", script);
        Assert.Contains("badwolf-minigame-player:", script);
        Assert.Contains("withAutomaticReconnect()", script);
        Assert.Contains("pendingQuestionResponsePlayerOf", script);
        Assert.Contains("inactiveCards.clear()", script);
    }

    [Fact]
    public void Page_keeps_room_cards_in_the_viewport_with_equal_grid_cells()
    {
        var page = ReadWebFile("Pages", "GuessWhatIPlay.cshtml");
        var styles = ReadWebFile("wwwroot", "css", "minigames.css");
        var script = ReadWebFile("wwwroot", "js", "minigames.js");

        Assert.Contains("ViewData[\"HidePortalFooter\"] = true", page);
        Assert.Contains("minigame-card", script);
        Assert.Contains("overflow: hidden", styles);
        Assert.Contains("height: calc(100dvh", styles);
        Assert.Contains("gridTemplateColumns", script);
        Assert.Contains("gridTemplateRows", script);
        Assert.Contains("ResizeObserver", script);
        Assert.Contains("grid-template-areas: \"stage questions\"", styles);
        Assert.Contains("grid-column: 2", styles);
        Assert.Contains("minimumSpacing", script);
        Assert.Contains("grid.style.gridTemplateColumns", script);
        Assert.Contains("justify-content: space-evenly", styles);
        Assert.Contains("align-content: space-evenly", styles);
        Assert.Contains("aspect-ratio: 1 / 1", styles);
    }

    [Fact]
    public void Question_panel_distinguishes_players_and_syncs_creator_theme()
    {
        var styles = ReadWebFile("wwwroot", "css", "minigames.css");
        var script = ReadWebFile("wwwroot", "js", "minigames.js");
        var hub = ReadWebFile("Hubs", "MinigameHub.cs");

        Assert.Contains("li.is-player-1", styles);
        Assert.Contains("li.is-player-2", styles);
        Assert.Contains("var(--gold)", styles);
        Assert.Contains("var(--red-bright)", styles);
        Assert.Contains("item.classList.add(`is-player-${historyPlayer}`)", script);
        Assert.Contains("captureTheme()", script);
        Assert.Contains("applyTheme(roomThemeOf(state))", script);
        Assert.Contains("CreateRoom', captureTheme()", script);
        Assert.Contains("MinigameThemeSnapshot? theme", hub);
    }

    [Fact]
    public void Question_file_contains_a_large_yes_no_question_pool()
    {
        var questions = ReadWebFile("Resources", "Minigames", "GameCards", "questions.txt")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Assert.True(questions.Length >= 400, $"Only {questions.Length} questions were generated.");
        Assert.Equal(questions.Length, questions.Distinct(StringComparer.Ordinal).Count());
        Assert.All(questions, question => Assert.EndsWith("?", question));
    }

    [Fact]
    public void Room_updates_are_group_scoped_and_local_card_state_stays_local()
    {
        var script = ReadWebFile("wwwroot", "js", "minigames.js");
        var hub = ReadWebFile("Hubs", "MinigameHub.cs");

        Assert.Contains("const inactiveCards = new Set()", script);
        Assert.Contains("roomChanged", script);
        Assert.Contains("synchronize(false)", script);
        Assert.Contains("ToggleExclusion", script);
        Assert.Contains("GetSignalRGroupName", hub);
        Assert.Contains("Clients", hub);
        Assert.DoesNotContain("Clients.All.SendAsync", hub);
    }

    private static string ReadWebFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = System.IO.Path.Combine(
                new[] { directory.FullName, "src", "BadWolfQuiz.Web" }
                    .Concat(parts)
                    .ToArray());
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate BadWolfQuiz.Web file: {System.IO.Path.Combine(parts)}");
    }
}
