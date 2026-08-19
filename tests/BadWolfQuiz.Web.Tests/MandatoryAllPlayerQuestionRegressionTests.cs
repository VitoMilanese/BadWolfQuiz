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
    public void All_player_questions_use_private_per_player_wagers()
    {
        var root = FindRepositoryRoot();
        var editorModel = Read(root,
            "src/BadWolfQuiz.Web/Pages/Admin/Quizzes/QuestionEditor.cshtml.cs");
        var snapshot = Read(root,
            "src/BadWolfQuiz.Game/Definitions/QuizSnapshot.cs");
        var board = Read(root,
            "src/BadWolfQuiz.Game/Runtime/GameBoard.cs");
        var state = Read(root,
            "src/BadWolfQuiz.Game/Runtime/GameSessionState.cs");
        var session = Read(root,
            "src/BadWolfQuiz.Game/Runtime/GameSession.cs");
        var endpoint = Read(root,
            "src/BadWolfQuiz.Web/Pages/AllPlayerQuestion.cshtml.cs");
        var script = Read(root,
            "src/BadWolfQuiz.Web/wwwroot/js/all-player-question.js");
        var host = Read(root,
            "src/BadWolfQuiz.Web/Pages/Admin/Games/Lobby.cshtml");

        Assert.Contains(
            "Input.PresentationType != QuestionPresentationType.FourClues",
            editorModel);
        Assert.Contains("IsEligibleForRandomWagerSelection", snapshot);
        Assert.Contains("question.IsEligibleForRandomWagerSelection", board);
        Assert.Contains("AllPlayerWagers", board);
        Assert.Contains("AllPlayerWagers", state);
        Assert.Contains("SubmitAllPlayerQuestionWager", session);
        Assert.Contains("StartAllPlayerQuestionAfterWagers", session);
        Assert.Contains("OnPostWager", endpoint);
        Assert.Contains("OnPostMinimumWager", endpoint);
        Assert.Contains("OnPostStartQuestion", endpoint);
        Assert.Contains("GetScoreMagnitude", endpoint);
        Assert.Contains("state.phase === \"wagering\"", script);
        Assert.Contains("text.minimumWager", script);
        Assert.Contains("question-wager-form", script);
        Assert.Contains(
            "var eligiblePlayers = Model.CurrentQuestion.IsAllPlayerQuestion",
            host);
        var allPlayerGuardIndex = host.IndexOf(
            "var eligiblePlayers = Model.CurrentQuestion.IsAllPlayerQuestion",
            StringComparison.Ordinal);
        var sharedWagerDereferenceIndex = host.IndexOf(
            "Model.CurrentQuestion.Wager!.PlayerId",
            allPlayerGuardIndex,
            StringComparison.Ordinal);
        Assert.True(allPlayerGuardIndex >= 0 &&
            sharedWagerDereferenceIndex > allPlayerGuardIndex);
        Assert.DoesNotContain("GetRequiredWagerAmount", endpoint);
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
        Assert.Contains("OnPostEmptyAnswer", endpoint);
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
            "translateY(calc(100% - 2.75rem))",
            styles);
        Assert.Contains(
            "translateX(calc(100% - 2.75rem))",
            styles);
        Assert.Contains(".all-player-host-choice-preview:hover", styles);
        Assert.Contains(".all-player-host-progress:hover", styles);
        Assert.Contains("--all-player-choice-action-width", styles);
        Assert.Contains("right: 0.5rem", styles);
        Assert.Contains("left: 0.5rem", styles);
        Assert.Contains("bottom: 0.15rem", styles);
        Assert.Contains("padding-bottom: 3.5rem", styles);
        Assert.Contains("overflow: hidden", styles);
        Assert.Contains("border-radius: 0.85rem 0.85rem 0 0", styles);
        Assert.Contains("getProgressRenderKey", script);
        Assert.Contains("progress.dataset.renderKey", script);
        Assert.Contains("renderPrimaryAction", script);
        Assert.DoesNotContain("progress.appendChild(start)", script);
        Assert.Contains("data-all-player-primary-action", host);
        Assert.Contains(".all-player-host-primary-action", styles);
        Assert.Contains(":has(.all-player-wager-waiting)", styles);
        var multipleChoiceIndex = host.IndexOf(
            "if (isAllPlayerMultipleChoiceQuestion)",
            StringComparison.Ordinal);
        var closeIndex = host.IndexOf(
            "all-player-host-close-form",
            multipleChoiceIndex,
            StringComparison.Ordinal);
        var previewIndex = host.IndexOf(
            "data-all-player-server-preview",
            closeIndex,
            StringComparison.Ordinal);
        var gridIndex = host.IndexOf(
            "all-player-host-choice-grid",
            previewIndex,
            StringComparison.Ordinal);
        Assert.True(multipleChoiceIndex >= 0 &&
            closeIndex > multipleChoiceIndex &&
            previewIndex > closeIndex &&
            gridIndex > previewIndex);
        Assert.Contains("▦", styles);
        Assert.Contains("👥", styles);
    }

    [Fact]
    public void Text_answers_are_reviewed_in_the_main_area_not_the_player_drawer()
    {
        var root = FindRepositoryRoot();
        var endpoint = Read(root,
            "src/BadWolfQuiz.Web/Pages/AllPlayerQuestion.cshtml.cs");
        var script = Read(root,
            "src/BadWolfQuiz.Web/wwwroot/js/all-player-question.js");
        var styles = Read(root,
            "src/BadWolfQuiz.Web/wwwroot/css/site.css");

        Assert.Contains("phase == \"judging\"", endpoint);
        Assert.Contains("AllCurrentPlayersSubmitted(game, question)", endpoint);
        Assert.Contains("renderTextReview", script);
        Assert.Contains("all-player-host-review", script);
        Assert.Contains("text.emptyAnswer", script);
        Assert.Contains("\"-\"", endpoint);
        Assert.Contains("event.stopPropagation()", script);
        Assert.Contains("getProgressRenderKey", script);
        Assert.Contains("getTextReviewRenderKey", script);
        Assert.Contains("review.dataset.renderKey", script);
        Assert.Contains("review.isConnected", script);
        Assert.DoesNotContain("progress.appendChild(judge)", script);
        Assert.Contains("all-player-text-reviewing", styles);
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
        Assert.Contains("currentControlsKey", script);
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
    public void Player_all_player_runtime_hides_buzzer_and_restores_scrolling()
    {
        var root = FindRepositoryRoot();
        var script = Read(root,
            "src/BadWolfQuiz.Web/wwwroot/js/all-player-question.js");
        var styles = Read(root,
            "src/BadWolfQuiz.Web/wwwroot/css/site.css");

        Assert.Contains("all-player-runtime-active", script);
        Assert.Contains("setRuntimeActive(true)", script);
        Assert.Contains(
            ".player-lobby.all-player-runtime-active .player-buzzer-panel",
            styles);
        Assert.Contains(
            ".page-shell:has(.player-lobby.all-player-runtime-active)",
            styles);
        Assert.Contains("overflow-y: auto", styles);
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
