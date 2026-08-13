namespace BadWolfQuiz.Web.Tests;

public sealed class LateJoinHandlingTests
{
    [Fact]
    public void Join_page_handles_game_rule_violations_as_a_user_facing_error()
    {
        var source = File.ReadAllText(FindFile(
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Join",
            "Index.cshtml.cs"));

        Assert.Contains("catch (GameRuleViolationException)", source);
        Assert.Contains("localizer[\"Error_GameNoLongerAcceptsPlayers\"]", source);
        Assert.Contains("return Page();", source);
        Assert.Contains("Error_GameNoLongerAcceptsPlayers", File.ReadAllText(FindFile("src", "BadWolfQuiz.Web", "Resources", "Localization", "SharedResource.resx")));
        Assert.Contains("Error_GameNoLongerAcceptsPlayers", File.ReadAllText(FindFile("src", "BadWolfQuiz.Web", "Resources", "Localization", "SharedResource.uk.resx")));
        Assert.Contains("Error_GameNoLongerAcceptsPlayers", File.ReadAllText(FindFile("src", "BadWolfQuiz.Web", "Resources", "Localization", "SharedResource.it.resx")));
    }

    [Fact]
    public void Join_page_keeps_existing_success_and_validation_paths()
    {
        var source = File.ReadAllText(FindFile(
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Join",
            "Index.cshtml.cs"));

        Assert.Contains("case PlayerJoinStatus.Success:", source);
        Assert.Contains("case PlayerJoinStatus.GameNotFound:", source);
        Assert.Contains("case PlayerJoinStatus.NameAlreadyUsed:", source);
        Assert.Contains("case PlayerJoinStatus.GameAlreadyStarted:", source);
        Assert.Contains("case PlayerJoinStatus.PlayerBlocked:", source);
    }

    private static string FindFile(params string[] path)
    {
        var current = AppContext.BaseDirectory;

        while (!string.IsNullOrWhiteSpace(current))
        {
            var candidate = Path.Combine([current, .. path]);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        throw new FileNotFoundException($"Could not find {Path.Combine(path)}.");
    }
}
