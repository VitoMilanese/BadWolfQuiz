namespace BadWolfQuiz.Web.Tests;

public sealed class MandatoryAllPlayerQuestionRegressionTests
{
    [Fact]
    public void Client_exposes_both_answer_modes_and_editor_validation()
    {
        var root = FindRepositoryRoot();
        var script = Read(root,
            "src/BadWolfQuiz.Web/wwwroot/js/all-player-question.js");
        var editor = Read(root,
            "src/BadWolfQuiz.Web/Pages/Admin/Quizzes/QuestionEditor.cshtml");
        var editorModel = Read(root,
            "src/BadWolfQuiz.Web/Pages/Admin/Quizzes/QuestionEditor.cshtml.cs");
        var compatibility = Read(root,
            "src/BadWolfQuiz.Web/Services/AllPlayerQuestionCompatibility.cs");
        var snapshotFactory = Read(root,
            "src/BadWolfQuiz.Web/Services/QuizSnapshotFactory.cs");

        Assert.Contains("All players — text answer", script);
        Assert.Contains("All players — multiple choice", script);
        Assert.Contains("answerCards.length === 1", script);
        Assert.Contains("answerCards.length < 2", script);
        Assert.Contains("answerCards.length > 4", script);
        Assert.Contains("[\"Text\", \"Image\"]", script);
        Assert.Contains("imageCardHasFile", script);
        Assert.Contains("invalidChoiceMedia", script);
        Assert.Contains("Input.AllPlayerMode", editor);
        Assert.Contains("data-all-player-mode", editor);
        Assert.Contains("QuestionType_AllPlayerText", editor);
        Assert.Contains("QuestionType_AllPlayerMultipleChoice", editor);
        Assert.Contains("modeInput.value", script);
        Assert.Contains("ResolvePostedPresentationType", editorModel);
        Assert.Contains("LooksLikeLegacyImageMultipleChoice", compatibility);
        Assert.Contains("ResolveStoredPresentationType", snapshotFactory);
        Assert.DoesNotContain("special.checked = false", script);
        Assert.DoesNotContain("excludeRandom.checked = true", script);
    }

    [Fact]
    public void All_player_questions_support_explicit_and_random_wagers()
    {
        var root = FindRepositoryRoot();
        var editorModel = Read(root,
            "src/BadWolfQuiz.Web/Pages/Admin/Quizzes/QuestionEditor.cshtml.cs");
        var snapshot = Read(root,
            "src/BadWolfQuiz.Game/Definitions/QuizSnapshot.cs");
        var board = Read(root,
            "src/BadWolfQuiz.Game/Runtime/GameBoard.cs");
        var session = Read(root,
            "src/BadWolfQuiz.Game/Runtime/GameSession.cs");
        var endpoint = Read(root,
            "src/BadWolfQuiz.Web/Pages/AllPlayerQuestion.cshtml.cs");

        Assert.Contains(
            "Input.PresentationType != QuestionPresentationType.FourClues",
            editorModel);
        Assert.Contains("question.IsSpecial || isAllPlayer", editorModel);
        Assert.Contains("IsEligibleForRandomWagerSelection", snapshot);
        Assert.Contains("question.IsEligibleForRandomWagerSelection", board);
        Assert.Contains("IsAllPlayerQuestion ? null : playerId", board);
        Assert.Contains("question.IsSpecial && question.IsAllPlayerQuestion", session);
        Assert.Contains("GetCorrectScoreValue", endpoint);
        Assert.Contains("GetIncorrectScoreValue", endpoint);
        Assert.Contains("GetRequiredWagerAmount", endpoint);
    }

    [Fact]
    public void Text_answers_are_manual_and_host_can_close_answering()
    {
        var root = FindRepositoryRoot();
        var endpoint = Read(root,
            "src/BadWolfQuiz.Web/Pages/AllPlayerQuestion.cshtml.cs");
        var registry = Read(root,
            "src/BadWolfQuiz.Web/Services/GameSessionRegistry.cs");
        var registration = Read(root,
            "src/BadWolfQuiz.Web/Services/GameSessionRegistration.cs");
        var host = Read(root,
            "src/BadWolfQuiz.Web/Pages/Admin/Games/Lobby.cshtml");

        Assert.Contains("OnPostJudge", endpoint);
        Assert.Contains("GetOrCreateAllPlayerTextReview", endpoint);
        Assert.DoesNotContain("ConcurrentDictionary<TextReviewKey", endpoint);
        Assert.Contains("CloseAllPlayerQuestionAnswering", registry);
        Assert.Contains("review.Accepting = false", registry);
        Assert.Contains("AllPlayerTextReviewState", registration);
        Assert.Contains("CloseAllPlayerQuestion", host);
        Assert.Contains("AllPlayer_ReviewAnswersNow", host);
    }

    [Fact]
    public void Host_and_resolved_answer_use_explicit_compact_grids()
    {
        var root = FindRepositoryRoot();
        var host = Read(root,
            "src/BadWolfQuiz.Web/Pages/Admin/Games/Lobby.cshtml");
        var preview = Read(root,
            "src/BadWolfQuiz.Web/Pages/Admin/Games/_GameContentPreview.cshtml");
        var styles = Read(root,
            "src/BadWolfQuiz.Web/wwwroot/css/site.css");
        var script = Read(root,
            "src/BadWolfQuiz.Web/wwwroot/js/all-player-question.js");
        var editor = Read(root,
            "src/BadWolfQuiz.Web/Pages/Admin/Quizzes/QuestionEditor.cshtml");
        var editorPreview = Read(root,
            "src/BadWolfQuiz.Web/Pages/Admin/Quizzes/Shared/_QuestionPreviewModal.cshtml");

        Assert.Contains("data-all-player-server-preview", host);
        Assert.Contains("GetAllPlayerHostChoiceBlocks", host);
        Assert.Contains("answer = true", host);
        Assert.Contains("all-player-answer-grid", host);
        Assert.Contains("all-player-answer-option-correct", host);
        Assert.Contains("all-player-answer-option-incorrect", host);
        Assert.Contains("all-player-answer-grid", preview);
        Assert.Contains("all-player-answer-option-correct", preview);
        Assert.Contains("all-player-answer-option-incorrect", preview);
        Assert.Contains(".all-player-answer-grid", styles);
        Assert.Contains("grid-template-columns: repeat(2", styles);
        Assert.Contains("justify-items: stretch", styles);
        Assert.Contains("overflow: hidden", styles);
        Assert.Contains("min-height: clamp(4.5rem, 13vh, 8rem)", styles);
        Assert.DoesNotContain("min-height: clamp(10rem, 28vh, 22rem)", styles);
        Assert.Contains("height: min(14vh, 9rem)", styles);
        Assert.Contains("border-color: #2e7d32", styles);
        Assert.Contains("grid-auto-rows: minmax(4.5rem, auto)", script);
        Assert.Contains("justify-items: stretch", script);
        Assert.DoesNotContain("grid-auto-rows: minmax(0, 1fr)", script);
        Assert.Contains("height: min(14vh, 9rem)", script);
        Assert.Contains("isAllPlayerChoiceAnswerPreview", editor);
        Assert.Contains("all-player-answer-option-correct", editor);
        Assert.Contains("all-player-answer-option-incorrect", editor);
        Assert.Contains(
            ".question-preview-content.all-player-answer-grid",
            editorPreview);
        Assert.Contains(".question-preview-image", editorPreview);
    }

    [Fact]
    public void Host_answering_uses_hover_drawers_for_choices_and_progress()
    {
        var root = FindRepositoryRoot();
        var host = Read(root,
            "src/BadWolfQuiz.Web/Pages/Admin/Games/Lobby.cshtml");
        var styles = Read(root,
            "src/BadWolfQuiz.Web/wwwroot/css/site.css");
        var script = Read(root,
            "src/BadWolfQuiz.Web/wwwroot/js/all-player-question.js");

        Assert.Contains("data-all-player-server-preview", host);
        Assert.Contains("tabindex=", host);
        Assert.Contains("preview.tabIndex = 0", script);
        Assert.Contains("progress.tabIndex = 0", script);
        Assert.Contains("/* All-player host hover drawers */", styles);
        Assert.Contains("@media (hover: hover)", styles);
        Assert.Contains(
            "translate(-50%, calc(100% - 2.75rem))",
            styles);
        Assert.Contains(
            "translateX(calc(100% - 2.75rem))",
            styles);
        Assert.Contains(".all-player-host-choice-preview:hover", styles);
        Assert.Contains(".all-player-host-progress:hover", styles);
        Assert.Contains("top: -3.65rem", styles);
        Assert.Contains("padding-bottom: 6.75rem", styles);
        var previewIndex = host.IndexOf(
            "data-all-player-server-preview",
            StringComparison.Ordinal);
        var closeIndex = host.IndexOf(
            "all-player-host-close-form",
            previewIndex,
            StringComparison.Ordinal);
        var gridIndex = host.IndexOf(
            "all-player-host-choice-grid",
            previewIndex,
            StringComparison.Ordinal);
        Assert.True(previewIndex >= 0 &&
            closeIndex > previewIndex &&
            gridIndex > closeIndex);
        Assert.Contains("▦", styles);
        Assert.Contains("👥", styles);
    }

    [Fact]
    public void Reconnect_requires_approval_and_restores_all_player_controls()
    {
        var root = FindRepositoryRoot();
        var endpoint = Read(root,
            "src/BadWolfQuiz.Web/Pages/AllPlayerQuestion.cshtml.cs");
        var script = Read(root,
            "src/BadWolfQuiz.Web/wwwroot/js/all-player-question.js");
        var player = Read(root,
            "src/BadWolfQuiz.Web/Pages/Player/Lobby.cshtml");
        var registry = Read(root,
            "src/BadWolfQuiz.Web/Services/GameSessionRegistry.cs");

        Assert.Contains("return File(block.FileData, block.FileContentType);", endpoint);
        Assert.DoesNotContain(
            "File(block.FileData, block.FileContentType, block.FileName)",
            endpoint);
        Assert.Contains("controlsMissing", script);
        Assert.Contains("currentOptionsKey", script);
        Assert.Contains("playerAccessStorageKey", script);
        Assert.Contains("localStorage.getItem(playerAccessStorageKey)", script);
        Assert.Contains("let playerSessionPending = true", script);
        Assert.Contains("badwolf:player-session-pending", script);
        Assert.Contains("badwolf:player-session-ready", script);
        Assert.Contains("badwolf:player-session-pending", player);
        Assert.Contains("rejoinApprovalRequired", player);
        Assert.DoesNotContain("refreshTransitionToken", player);
        Assert.Contains("TimeSpan.FromSeconds(30)", registry);
        Assert.DoesNotContain("TimeSpan.FromHours(1)", registry);
        Assert.Contains("hostPollNow", script);
        Assert.Contains("badwolf:host-gameplay-updated", script);
    }

    [Fact]
    public void All_player_asset_is_hash_versioned_in_layout()
    {
        var root = FindRepositoryRoot();
        var layout = Read(root,
            "src/BadWolfQuiz.Web/Pages/Shared/_Layout.cshtml");
        var bootstrap = Read(root,
            "src/BadWolfQuiz.Web/wwwroot/js/quick-timer-controls.js");

        Assert.Contains("~/js/all-player-question.js", layout);
        Assert.Contains("asp-append-version=\"true\"", layout);
        Assert.DoesNotContain("loadAllPlayerQuestionClient", bootstrap);
        Assert.DoesNotContain("all-player-question.js?v=", bootstrap);
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
