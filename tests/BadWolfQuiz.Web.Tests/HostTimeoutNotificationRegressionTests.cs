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
