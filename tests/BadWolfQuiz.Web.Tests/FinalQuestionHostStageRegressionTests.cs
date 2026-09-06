namespace BadWolfQuiz.Web.Tests;

public sealed class FinalQuestionHostStageRegressionTests
{
    [Fact]
    public void Lobby_loads_scoped_final_question_host_stage_styles()
    {
        var imports = File.ReadAllText(FindWebFile("Pages", "_ViewImports.cshtml"));
        var tagHelper = File.ReadAllText(FindWebFile(
            "TagHelpers",
            "FinalQuestionHostStageAssetsTagHelper.cs"));
        var styles = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "final-question-host-stage.css"));
        var lobby = File.ReadAllText(FindWebFile(
            "Pages",
            "Admin",
            "Games",
            "Lobby.cshtml"));

        Assert.Contains("FinalQuestionHostStageAssetsTagHelper", imports);
        Assert.Contains("/Admin/Games/Lobby", tagHelper);
        Assert.Contains("/css/final-question-host-stage.css?v=1", tagHelper);

        Assert.Contains(".host-game-board.final-question-host", styles);
        Assert.Contains("data-game-status=\"finalwagering\"", styles);
        Assert.Contains("data-game-status=\"finalanswering\"", styles);
        Assert.Contains("data-game-status=\"finaljudging\"", styles);
        Assert.Contains(".final-player-answer-presentation", styles);
        Assert.Contains(".answer-presentation", styles);
        Assert.Contains("prefers-reduced-motion", styles);

        Assert.Contains("class=\"host-game-board final-question-host\"", lobby);
        Assert.Contains("FinalWagering", lobby);
        Assert.Contains("FinalAnswering", lobby);
        Assert.Contains("FinalJudging", lobby);
    }

    private static string FindWebFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                new[]
                {
                    directory.FullName,
                    "src",
                    "BadWolfQuiz.Web"
                }.Concat(parts).ToArray());

            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate BadWolfQuiz.Web file: {string.Join('/', parts)}");
    }
}
