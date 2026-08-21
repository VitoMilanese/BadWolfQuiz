namespace BadWolfQuiz.Web.Tests;

public sealed class QuestionBuzzerWebRegressionTests
{
    [Fact]
    public void Editor_binds_and_persists_buzzer_delay()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "Quizzes",
            "QuestionEditor.cshtml.cs"));

        Assert.Contains("BuzzDelaySeconds = question.BuzzDelaySeconds", source);
        Assert.Contains("question.BuzzDelaySeconds =", source);
        Assert.Contains("public int BuzzDelaySeconds { get; set; }", source);
    }

    [Fact]
    public void Authoring_ui_removes_disabled_mode_for_buzzer_questions()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "question-buzzer-modes.js"));

        Assert.Contains("const disabledWasSelected = modeSelect.value === \"5\";", source);
        Assert.Contains("modeSelect.querySelector('option[value=\"5\"]')?.remove();", source);
        Assert.Contains("modeSelect.value = \"1\";", source);
    }

    [Fact]
    public void Host_helper_handles_delay_and_media_completion()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "question-buzzer-modes.js"));
        var youtube = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "youtube-auto-expand.js"));

        Assert.Contains("afterdelay", source);
        Assert.Contains("aftermedia", source);
        Assert.Contains("completedMediaSourceQuestionId", source);
        Assert.Contains("hasGateMediaCompleted", source);
        Assert.Contains("completedMediaSourceQuestionId = sourceQuestionId;", source);
        Assert.Contains("currentSourceQuestionId() !== sourceQuestionId", source);
        Assert.Contains("badwolf:youtube-ended", source);
        Assert.Contains("badwolf:youtube-ended", youtube);
        Assert.Contains("badwolf:youtube-error", youtube);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
            !File.Exists(Path.Combine(directory.FullName, "BadWolfQuiz.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
