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
        Assert.Contains("values.length === 1", script);
        Assert.Contains("values.length >= 2", script);
        Assert.Contains("values.length <= 4", script);
        Assert.Contains("answerHeading.textContent", script);
    }

    [Fact]
    public void Player_submission_scores_only_correct_answers()
    {
        var root = FindRepositoryRoot();
        var endpoint = Read(root,
            "src/BadWolfQuiz.Web/Pages/AllPlayerQuestion.cshtml.cs");

        Assert.Contains("StringComparison.OrdinalIgnoreCase", endpoint);
        Assert.Contains("isCorrect ? question.Points : 0", endpoint);
        Assert.Contains("resolveQuestionIfAvailable: false", endpoint);
        Assert.Contains("duplicate = true", endpoint);
        Assert.Contains("ResolveQuestionWithoutCorrectAnswer", endpoint);
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
        Assert.Contains("/js/all-player-question.js?v=2", bootstrap);
        Assert.Contains("start-game-form", bootstrap);
        Assert.Contains("MutationObserver", bootstrap);
    }

    [Fact]
    public void Multiple_choice_does_not_reveal_correct_option_marker_to_players()
    {
        var root = FindRepositoryRoot();
        var endpoint = Read(root,
            "src/BadWolfQuiz.Web/Pages/AllPlayerQuestion.cshtml.cs");

        Assert.Contains("RotateOptions", endpoint);
        Assert.Contains("options = isMultipleChoice", endpoint);
        Assert.DoesNotContain("correctOption", endpoint);
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
