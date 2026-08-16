namespace BadWolfQuiz.Web.Tests;

public sealed class PlayerFinalAnswerPresentationRegressionTests
{
    [Fact]
    public void Final_answering_hides_question_content_without_affecting_other_final_states()
    {
        var root = FindRepositoryRoot();
        var player = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Player",
            "Lobby.cshtml"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "css",
            "player-admission-menu.css"));

        Assert.Contains(
            "data-final-status=\"@Model.Game.Session.Status.ToString().ToLowerInvariant()\"",
            player,
            StringComparison.Ordinal);
        Assert.Contains(
            ".player-lobby[data-final-status=\"finalanswering\"] #player-final-panel > .game-content-blocks { display: none; }",
            css,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            ".player-lobby[data-final-status=\"finaljudging\"] #player-final-panel > .game-content-blocks { display: none; }",
            css,
            StringComparison.Ordinal);
        Assert.Contains("id=\"final-answer-form\"", player, StringComparison.Ordinal);
        Assert.Contains("id=\"final-wager-form\"", player, StringComparison.Ordinal);
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
