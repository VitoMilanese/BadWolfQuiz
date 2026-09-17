namespace BadWolfQuiz.Web.Tests;

public sealed class OnDemandAllPlayerChoiceRegressionTests
{
    [Fact]
    public void Editor_and_runtime_expose_the_on_demand_choice_flow()
    {
        var root = FindRepositoryRoot();
        var editor = Read(root, "src", "BadWolfQuiz.Web", "Pages", "Admin", "Quizzes", "QuestionEditor.cshtml");
        var editorModel = Read(root, "src", "BadWolfQuiz.Web", "Pages", "Admin", "Quizzes", "QuestionEditor.cshtml.cs");
        var editorScript = Read(root, "src", "BadWolfQuiz.Web", "wwwroot", "js", "multiple-choice-answer-options.js");
        var playerScript = Read(root, "src", "BadWolfQuiz.Web", "wwwroot", "js", "all-player-question.js");
        var lobby = Read(root, "src", "BadWolfQuiz.Web", "Pages", "Admin", "Games", "Lobby.cshtml");
        var lobbyModel = Read(root, "src", "BadWolfQuiz.Web", "Pages", "Admin", "Games", "Lobby.cshtml.cs");
        var endpoint = Read(root, "src", "BadWolfQuiz.Web", "Pages", "AllPlayerQuestion.cshtml.cs");
        var runtime = Read(root, "src", "BadWolfQuiz.Game", "Runtime", "GameBoard.cs");
        var state = Read(root, "src", "BadWolfQuiz.Game", "Runtime", "GameSessionState.cs");
        var registration = Read(root, "src", "BadWolfQuiz.Web", "Services", "GameSessionRegistration.cs");

        Assert.Contains("grid-column: 1 / -1", editor, StringComparison.Ordinal);
        Assert.Contains("RevealAnswerOptionsOnDemand", editor, StringComparison.Ordinal);
        Assert.Contains("data-all-player-choice-on-demand-setting", editor, StringComparison.Ordinal);
        Assert.Contains("data-all-player-choice-on-demand", editor, StringComparison.Ordinal);
        Assert.Contains("RevealAnswerOptionsOnDemand { get; set; }", editorModel, StringComparison.Ordinal);
        Assert.Contains("MultipleChoiceOnDemandMode", editorModel, StringComparison.Ordinal);
        Assert.Contains("multipleChoiceOnDemand", editorScript, StringComparison.Ordinal);
        Assert.Contains("onDemandCheckbox.addEventListener", editorScript, StringComparison.Ordinal);
        Assert.Contains("specialCheckbox.checked = false", editorScript, StringComparison.Ordinal);
        Assert.Contains("excludeCheckbox.checked = true", editorScript, StringComparison.Ordinal);
        Assert.Contains("buzzSelect.disabled = false", editorScript, StringComparison.Ordinal);
        Assert.Contains("badwolf:question-editor-on-demand-synced", editorScript, StringComparison.Ordinal);
        Assert.Contains("data-on-demand-choice", editor, StringComparison.Ordinal);
        Assert.Contains("Model.Input.AllPlayerMode ==", editor, StringComparison.Ordinal);
        Assert.Contains("Model.Input.RevealAnswerOptionsOnDemand = isOnDemandChoice", editor, StringComparison.Ordinal);
        Assert.Contains("questionEditorForm?.dataset.onDemandChoice === \"true\"", editor, StringComparison.Ordinal);
        Assert.Contains("answerRewardModifierCheckbox.disabled = isOnDemandChoice", editor, StringComparison.Ordinal);
        Assert.Contains("form.dataset.onDemandChoice = onDemand ? \"true\" : \"false\"", editorScript, StringComparison.Ordinal);
        Assert.Contains("waitForEditorMount();", editorScript, StringComparison.Ordinal);
        Assert.Contains("form.dataset.multipleChoiceAnswerOptionsController", editorScript, StringComparison.Ordinal);
        Assert.DoesNotContain("window.badWolfMultipleChoiceAnswerOptionsEditorLoaded", editorScript, StringComparison.Ordinal);
        Assert.Contains("const supportsWagerMode = type === \"0\";", editorScript, StringComparison.Ordinal);
        Assert.DoesNotContain("type === \"0\" || onDemand", editorScript, StringComparison.Ordinal);
        Assert.Contains("const hideBuzzMode = isWagerQuestion && !isOnDemandChoice", editor, StringComparison.Ordinal);
        Assert.Contains("Model.Input.PresentationType != BadWolfQuiz.Game.Definitions.QuestionPresentationType.Standard ? \"hidden\" : null", editor, StringComparison.Ordinal);
        Assert.Contains("wagerModeSetting.hidden = !supportsWagerMode", editorScript, StringComparison.Ordinal);
        Assert.Contains("answerRewardModifierSetting.hidden = onDemand", editorScript, StringComparison.Ordinal);
        Assert.Contains("answerRewardModifierCheckbox.checked = false", editorScript, StringComparison.Ordinal);
        Assert.Contains("hidden=\"@(isOnDemandChoice ? \"hidden\" : null)\"", editor, StringComparison.Ordinal);
        Assert.Contains("isOnDemandChoice;", editor, StringComparison.Ordinal);
        Assert.Contains("Input.AllowAnswerRewardModifiers = false", editorModel, StringComparison.Ordinal);
        Assert.Contains("!isAllPlayerMultipleChoiceOnDemand &&", editorModel, StringComparison.Ordinal);
        Assert.Contains("CanRevealAllPlayerChoiceOptions", lobby, StringComparison.Ordinal);
        Assert.Contains("GameBoard_ShowAnswerOptions", lobby, StringComparison.Ordinal);
        Assert.Contains("OnPostRevealAllPlayerChoiceOptionsAsync", lobbyModel, StringComparison.Ordinal);
        Assert.Contains("AllPlayerChoiceExcludedPlayerIds", runtime, StringComparison.Ordinal);
        Assert.Contains(".Where(attempt => !attempt.IsCorrect)", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("_allPlayerChoiceExcludedPlayerIds.Add(answeringPlayerId)", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("AllPlayerChoiceRevealLocked", runtime, StringComparison.Ordinal);
        Assert.Contains("Math.Max(1, Points / 2)", runtime, StringComparison.Ordinal);
        Assert.Contains("AllPlayerChoiceExcludedPlayerIds", state, StringComparison.Ordinal);
        Assert.Contains("question.CorrectAnswerValue", endpoint, StringComparison.Ordinal);
        Assert.Contains("isChoiceExcluded", endpoint, StringComparison.Ordinal);
        Assert.Contains("isChoiceExcluded", playerScript, StringComparison.Ordinal);
        Assert.Contains("buzzerAlreadyUsed", playerScript, StringComparison.Ordinal);
        Assert.Contains("const answeringSelector = state.mode === \"multipleChoice\"", playerScript, StringComparison.Ordinal);
        Assert.Contains(".question-presentation [data-all-player-server-preview]", playerScript, StringComparison.Ordinal);
        Assert.Contains("getHostChoicesRenderKey", playerScript, StringComparison.Ordinal);
        Assert.Contains("preview.dataset.renderKey = renderKey", playerScript, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "const renderHostChoices = (board, state) => {\n            removeHostChoices(board);",
            playerScript,
            StringComparison.Ordinal);
        Assert.Contains("data-all-player-review-action", lobby, StringComparison.Ordinal);
        Assert.Contains("AllPlayer_ReviewAnswersNow", lobby, StringComparison.Ordinal);
        Assert.Contains("const sameAllPlayerChoiceLayout =", lobby, StringComparison.Ordinal);
        Assert.Contains(
            "currentView.querySelector(\"[data-all-player-server-preview]\")",
            lobby,
            StringComparison.Ordinal);
        Assert.Contains(
            "nextView.querySelector(\"[data-all-player-server-preview]\")",
            lobby,
            StringComparison.Ordinal);
        Assert.Contains(
            "sameMediaAutoplayState &&\n                    sameAllPlayerChoiceLayout &&",
            lobby,
            StringComparison.Ordinal);
        Assert.Contains("item.IsAllPlayerQuestion", registration, StringComparison.Ordinal);
    }

    [Fact]
    public void On_demand_choice_is_not_backed_by_a_new_database_column()
    {
        var root = FindRepositoryRoot();
        var model = Read(root, "src", "BadWolfQuiz.Web", "Models", "QuizModels.cs");
        var definition = Read(root, "src", "BadWolfQuiz.Game", "Definitions", "QuizSnapshot.cs");
        var compatibility = Read(root, "src", "BadWolfQuiz.Web", "Services", "AllPlayerQuestionCompatibility.cs");

        Assert.DoesNotContain("RevealAnswerOptionsOnDemand", model, StringComparison.Ordinal);
        Assert.Contains("AllPlayerMultipleChoiceOnDemand = 7", definition, StringComparison.Ordinal);
        Assert.Contains("GetContentPresentationType", compatibility, StringComparison.Ordinal);
    }

    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(Path.Combine([root, .. parts]));

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
