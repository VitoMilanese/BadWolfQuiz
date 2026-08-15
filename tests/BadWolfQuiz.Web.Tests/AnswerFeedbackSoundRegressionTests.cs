namespace BadWolfQuiz.Web.Tests;

public sealed class AnswerFeedbackSoundRegressionTests
{
    [Fact]
    public void Answer_feedback_sound_modes_are_configurable()
    {
        var root = FindRepositoryRoot();
        var appsettings = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "appsettings.json"));
        var model = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "Pages", "Admin", "Games", "Lobby.cshtml.cs"));
        var markup = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "Pages", "Admin", "Games", "Lobby.cshtml"));

        Assert.Contains("\"CorrectAnswerSound\": \"Triumph\"", appsettings);
        Assert.Contains("\"IncorrectAnswerSound\": \"Descent\"", appsettings);
        Assert.Contains("Game:CorrectAnswerSound", model);
        Assert.Contains("Game:IncorrectAnswerSound", model);
        Assert.Contains("data-correct-answer-sound=\"@Model.CorrectAnswerSound\"", markup);
        Assert.Contains("data-incorrect-answer-sound=\"@Model.IncorrectAnswerSound\"", markup);
    }

    [Fact]
    public void Correct_answer_has_multiple_web_audio_profiles()
    {
        var markup = File.ReadAllText(FindLobbyView());

        Assert.Contains("const playCorrectAnswerTriumph = context =>", markup);
        Assert.Contains("const playCorrectAnswerArcade = context =>", markup);
        Assert.Contains("const playCorrectAnswerChime = context =>", markup);
        Assert.Contains("mode === \"Arcade\"", markup);
        Assert.Contains("mode === \"Chime\"", markup);
        Assert.Contains("playCorrectAnswerTriumph(context);", markup);
    }

    [Fact]
    public void Incorrect_answer_has_multiple_web_audio_profiles()
    {
        var markup = File.ReadAllText(FindLobbyView());

        Assert.Contains("const playIncorrectAnswerDescent = context =>", markup);
        Assert.Contains("const playIncorrectAnswerArcadeFall = context =>", markup);
        Assert.Contains("const playIncorrectAnswerChimeFall = context =>", markup);
        Assert.Contains("[1046.5, 783.99, 659.25, 523.25]", markup);
        Assert.Contains("mode === \"ArcadeFall\"", markup);
        Assert.Contains("mode === \"ChimeFall\"", markup);
        Assert.Contains("playIncorrectAnswerDescent(context);", markup);
    }

    [Fact]
    public void Feedback_plays_only_after_successful_judging_command()
    {
        var markup = File.ReadAllText(FindLobbyView());
        var handlerStart = markup.IndexOf(
            "event.target.matches(\".question-judge-actions\")",
            StringComparison.Ordinal);
        var handlerEnd = markup.IndexOf(
            "for (const form of document.querySelectorAll(",
            handlerStart,
            StringComparison.Ordinal);
        var handler = markup[handlerStart..handlerEnd];

        var submitIndex = handler.IndexOf(
            "await submitGameControl(form, event.submitter);",
            StringComparison.Ordinal);
        var soundIndex = handler.IndexOf(
            "playAnswerFeedbackSound(judgingButton.value === \"true\");",
            StringComparison.Ordinal);

        Assert.True(submitIndex >= 0);
        Assert.True(soundIndex > submitIndex);
        Assert.Contains("judgingButton?.name === \"isCorrect\"", handler);
        Assert.Contains("await requestHostGameplayRefresh();", handler);
    }

    [Fact]
    public void Feedback_uses_web_audio_without_external_assets()
    {
        var markup = File.ReadAllText(FindLobbyView());

        Assert.Contains("window.AudioContext || window.webkitAudioContext", markup);
        Assert.Contains("createAnswerFeedbackAudioContext", markup);
        Assert.Contains("createOscillator()", markup);
        Assert.Contains("createGain()", markup);
        Assert.DoesNotContain("new Audio(", markup);
    }

    private static string FindLobbyView() =>
        Path.Combine(
            FindRepositoryRoot(),
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "Games",
            "Lobby.cshtml");

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
