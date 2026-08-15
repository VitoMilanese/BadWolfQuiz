namespace BadWolfQuiz.Web.Tests;

public sealed class HostTimeoutNotificationRegressionTests
{
    [Fact]
    public void Host_receives_non_blocking_animated_timeout_notification()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "Pages", "Admin", "Games", "Lobby.cshtml"));
        var css = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "wwwroot", "css", "site.css"));
        var hub = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "Hubs", "GameHub.cs"));

        Assert.Contains("data-question-timeout-notification", markup);
        Assert.Contains("connection.on(\"QuestionTimerExpired\"", markup);
        Assert.Contains("question-timeout-notification.is-visible", css);
        Assert.Contains("pointer-events: none", css);
        Assert.Contains("@keyframes question-timeout-impact", css);
        Assert.Contains("Group(HostGroupName(tick.Game.PublicCode))", hub);
        Assert.Contains("\"QuestionTimerExpired\"", hub);
    }

    [Fact]
    public void Host_timeout_notification_plays_a_web_audio_sting()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "Pages", "Admin", "Games", "Lobby.cshtml"));

        Assert.Contains("const playQuestionTimeoutSting = () =>", markup);
        Assert.Contains("window.AudioContext || window.webkitAudioContext", markup);
        Assert.Contains("createOscillator()", markup);
        Assert.Contains("createGain()", markup);
        Assert.Contains("playQuestionTimeoutSting();", markup);
        Assert.DoesNotContain("new Audio(", markup);
    }

    [Fact]
    public void Host_timer_warns_during_the_final_five_running_seconds()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "Pages", "Admin", "Games", "Lobby.cshtml"));
        var css = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "wwwroot", "css", "site.css"));

        Assert.Contains("Math.ceil(update.remainingMilliseconds / 500)", markup);
        Assert.Contains("warningSlot >= 1", markup);
        Assert.Contains("warningSlot <= 10", markup);
        Assert.Contains("update.status === \"running\"", markup);
        Assert.Contains("timerPanel.classList.toggle(\"timer-warning\", isWarning)", markup);
        Assert.Contains("timerPanel.classList.add(\"timer-warning-pulse\")", markup);
        Assert.Contains("playQuestionTimerWarningTick(warningSlot, update.remainingMilliseconds)", markup);
        Assert.Contains("playQuestionTimerWarningAlternatingTick(warningSlot % 2 === 0)", markup);
        Assert.Contains("const baseFrequency = isHighPitch ? 1380 : 980", markup);
        Assert.Contains("host-game-timer.timer-warning.timer-warning-pulse", css);
        Assert.Contains("animation: game-timer-warning-pulse 500ms ease-out 1", css);
        Assert.Contains("@keyframes game-timer-warning-pulse", css);
    }

    [Fact]
    public void Host_timer_warning_sound_mode_is_configurable()
    {
        var root = FindRepositoryRoot();
        var appsettings = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "appsettings.json"));
        var model = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "Pages", "Admin", "Games", "Lobby.cshtml.cs"));
        var markup = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "Pages", "Admin", "Games", "Lobby.cshtml"));

        Assert.Contains("\"QuestionTimerWarningSound\": \"Alternating\"", appsettings);
        Assert.Contains("Game:QuestionTimerWarningSound", model);
        Assert.Contains("data-question-timer-warning-sound=\"@Model.QuestionTimerWarningSound\"", markup);
        Assert.Contains("playQuestionTimerWarningRisingTick", markup);
        Assert.Contains("1180 + (5 - remainingSeconds) * 90", markup);
        Assert.Contains("if (warningSlot % 2 === 0)", markup);
        Assert.Contains("playQuestionTimerWarningAlternatingTick", markup);
        Assert.Contains("isHighPitch ? 1380 : 980", markup);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src")) && Directory.Exists(Path.Combine(directory.FullName, "tests")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
