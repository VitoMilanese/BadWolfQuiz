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
        Assert.Contains("CanRevealAllPlayerChoiceOptions", lobby, StringComparison.Ordinal);
        Assert.Contains("GameBoard_ShowAnswerOptions", lobby, StringComparison.Ordinal);
        Assert.Contains("OnPostRevealAllPlayerChoiceOptionsAsync", lobbyModel, StringComparison.Ordinal);
        Assert.Contains("AllPlayerChoiceExcludedPlayerIds", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("AllPlayerChoiceRevealLocked", runtime, StringComparison.Ordinal);
        Assert.Contains("Math.Max(1, Points / 2)", runtime, StringComparison.Ordinal);
        Assert.Contains("AllPlayerChoiceExcludedPlayerIds", state, StringComparison.Ordinal);
        Assert.Contains("question.CorrectAnswerValue", endpoint, StringComparison.Ordinal);
        Assert.Contains("isChoiceExcluded", endpoint, StringComparison.Ordinal);
        Assert.Contains("isChoiceExcluded", playerScript, StringComparison.Ordinal);
        Assert.Contains("buzzerAlreadyUsed", playerScript, StringComparison.Ordinal);
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
