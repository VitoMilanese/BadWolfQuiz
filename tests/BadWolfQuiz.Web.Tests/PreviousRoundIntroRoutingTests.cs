namespace BadWolfQuiz.Web.Tests;

public sealed class PreviousRoundIntroRoutingTests
{
    [Fact]
    public void Previous_round_uses_the_same_intro_routing_layer_as_next_round()
    {
        var script = File.ReadAllText(FindFile("src", "BadWolfQuiz.Web", "wwwroot", "js", "site.js"));
        var intro = File.ReadAllText(FindFile("src", "BadWolfQuiz.Web", "Pages", "Admin", "Games", "RunningRoundIntro.cshtml.cs"));

        Assert.Contains("handler === \"PreviousRound\"", script);
        Assert.Contains("`${runningIntroBase}?handler=Previous`", script);
        Assert.Contains("OnPostPreviousAsync", intro);
        Assert.Contains("ReturnToPreviousUnfinishedRound(game.PublicCode)", intro);
        Assert.Contains("RedirectToPage(new { id, returning = true })", intro);
    }

    private static string FindFile(params string[] path)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine([directory.FullName, .. path]);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate {Path.Combine(path)} from the test output directory.");
    }
}
