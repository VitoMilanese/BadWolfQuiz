namespace BadWolfQuiz.Web.Tests;

public sealed class WagerKeypadMaximumClampRegressionTests
{
    [Fact]
    public void Player_and_host_wager_keypads_clamp_digit_entry_to_maximum()
    {
        var root = FindRepositoryRoot();
        var allPlayerScript = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "all-player-question.js"));
        var hostPage = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "Games",
            "Lobby.cshtml"));

        Assert.Contains(
            "const nextValue = (display.value + digit)",
            allPlayerScript);
        Assert.Contains("display.value = Math.min(", allPlayerScript);
        Assert.Contains("Number(nextValue),", allPlayerScript);
        Assert.DoesNotContain("display.value += digit;", allPlayerScript);

        Assert.Contains(
            "(display.value + button.dataset.wagerDigit)",
            hostPage);
        Assert.Contains("display.value = Math.min(", hostPage);
        Assert.Contains("Number(nextValue), maximum).toString();", hostPage);
    }

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
