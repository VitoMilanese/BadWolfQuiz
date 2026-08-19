namespace BadWolfQuiz.Web.Tests;

public sealed class MandatoryAllPlayerQuestionRegressionTests
{
    [Fact]
    public void Client_exposes_both_answer_modes_and_editor_validation()
    {
        var root = FindRepositoryRoot();
        var script = Read(root,
            "src/BadWolfQuiz.Web/wwwroot/js/all-player-question.js");

        Assert.Contains("All players — text answer", script);
        Assert.Contains("All players — multiple choice", script);
        Assert.Contains("answerCards.length === 1", script);
        Assert.Contains("answerCards.length < 2", script);
        Assert.Contains("answerCards.length > 4", script);
        Assert.Contains("[\"Text\", \"Image\"]", script);
        Assert.Contains("imageCardHasFile", script);
        Assert.Contains("invalidChoiceMedia", script);
    }

    [Fact]
    public void Text_answers_are_submitted_for_zero_points_and_judged_by_host()
    {
        var root = FindRepositoryRoot();
        var endpoint = Read(root,
            "src/BadWolfQuiz.Web/Pages/AllPlayerQuestion.cshtml.cs");

        Assert.Contains("OnPostJudge", endpoint);
        Assert.Contains("isCorrect: false", endpoint);
        Assert.Contains("value: 0", endpoint);
        Assert.Contains("UpdateQuestionAnswerHistoryEntry", endpoint);
        Assert.Contains("review.JudgedPlayers.Add", endpoint);
        Assert.Contains("isCorrect ? question.Points : 0", endpoint);
    }

    [Fact]
    public void Player_and_host_use_live_all_player_panels_without_buzzer_controls()
    {
        var root = FindRepositoryRoot();
        var script = Read(root,
            "src/BadWolfQuiz.Web/wwwroot/js/all-player-question.js");
        var bootstrap = Read(root,
            "src/BadWolfQuiz.Web/wwwroot/js/quick-timer-controls.js");

        Assert.Contains(".player-buzzer-panel", script);
        Assert.Contains("all-player-question-answering", script);
        Assert.Contains("all-player-host-progress", script);
        Assert.Contains("BadWolfHostGameplay.refresh", script);
        Assert.Contains("/js/all-player-question.js?v=5", bootstrap);
        Assert.Contains("start-game-form", bootstrap);
        Assert.Contains("MutationObserver", bootstrap);
    }

    [Fact]
    public void Multiple_choice_options_are_shuffled_per_player_and_support_images()
    {
        var root = FindRepositoryRoot();
        var endpoint = Read(root,
            "src/BadWolfQuiz.Web/Pages/AllPlayerQuestion.cshtml.cs");
        var script = Read(root,
            "src/BadWolfQuiz.Web/wwwroot/js/all-player-question.js");

        Assert.Contains("ShuffleOptions", endpoint);
        Assert.Contains("player.Id.Value.GetHashCode()", endpoint);
        Assert.Contains("OnGetOptionImage", endpoint);
        Assert.Contains("block.Kind == ContentBlockKind.Image", endpoint);
        Assert.Contains("option.imageUrl", script);
        Assert.DoesNotContain("correctOption", endpoint);
    }

    [Fact]
    public void Host_renders_choice_options_as_grid_and_marks_answer_correctness()
    {
        var root = FindRepositoryRoot();
        var script = Read(root,
            "src/BadWolfQuiz.Web/wwwroot/js/all-player-question.js");

        Assert.Contains("all-player-host-choice-grid", script);
        Assert.Contains("grid-template-columns: repeat(2", script);
        Assert.Contains("all-player-multiple-choice-answer", script);
        Assert.Contains("all-player-multiple-choice-answer-layout", script);
        Assert.Contains("document.documentElement.classList.toggle", script);
        Assert.Contains("border: 3px solid #c62828", script);
        Assert.Contains("border-color: #2e7d32", script);
        Assert.Contains("max-height: min(28vh, 20rem)", script);
    }

    [Fact]
    public void Player_client_reinitializes_after_reconnect_dom_replacement()
    {
        var root = FindRepositoryRoot();
        var script = Read(root,
            "src/BadWolfQuiz.Web/wwwroot/js/all-player-question.js");

        Assert.Contains("allPlayerClientInitialized", script);
        Assert.Contains("existingPanel", script);
        Assert.Contains("panel.isConnected", script);
        Assert.Contains("getBuzzerPanel", script);
        Assert.Contains("const observer = new MutationObserver", script);
        Assert.Contains("initializeAll", script);
        Assert.Contains("lobby.isConnected", script);
        Assert.Contains("observer.observe(document.documentElement", script);
    }

    [Fact]
    public void Programmatic_editor_type_restore_is_marked_clean()
    {
        var root = FindRepositoryRoot();
        var script = Read(root,
            "src/BadWolfQuiz.Web/wwwroot/js/all-player-question.js");

        Assert.Contains("markEditorProgrammaticStateClean", script);
        Assert.Contains("editor-state-synchronized", script);
        Assert.Contains("data-question-save-status", script);
    }

    [Fact]
    public void Manual_feedback_uses_server_answer_layout_and_rejoin_refresh_hook()
    {
        var root = FindRepositoryRoot();
        var hostView = Read(root,
            "src/BadWolfQuiz.Web/Pages/Admin/Games/Lobby.cshtml");
        var playerView = Read(root,
            "src/BadWolfQuiz.Web/Pages/Player/Lobby.cshtml");
        var script = Read(root,
            "src/BadWolfQuiz.Web/wwwroot/js/all-player-question.js");
        var styles = Read(root,
            "src/BadWolfQuiz.Web/wwwroot/css/site.css");

        Assert.Contains("all-player-multiple-choice-answer-presentation", hostView);
        Assert.Contains(".all-player-multiple-choice-answer-presentation .game-content-blocks", styles);
        Assert.Contains("width: min(100%, 84rem)", styles);
        Assert.Contains("min-height: clamp(5rem, 10vh, 8rem)", script);
        Assert.Contains("badwolf:player-session-ready", playerView);
        Assert.Contains("badwolf:player-session-ready", script);
        Assert.Contains("playerPollNow", script);
    }

    private static string Read(string root, string relativePath) =>
        File.ReadAllText(Path.Combine(
            root,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src")) &&
                Directory.Exists(Path.Combine(directory.FullName, "tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
