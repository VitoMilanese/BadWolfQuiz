namespace BadWolfQuiz.Web.Tests;

public sealed class FinalQuestionJudgingTransitionRegressionTests
{
    [Fact]
    public void Ajax_final_commands_return_json_instead_of_redirect_html()
    {
        var pageModel = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "Games",
            "Lobby.cshtml.cs"));
        var methodStart = pageModel.IndexOf(
            "private async Task<IActionResult> ExecuteFinalHostCommand",
            StringComparison.Ordinal);
        var methodEnd = pageModel.IndexOf(
            "private IActionResult LoadPage",
            methodStart,
            StringComparison.Ordinal);

        Assert.True(methodStart >= 0);
        Assert.True(methodEnd > methodStart);
        var method = pageModel[methodStart..methodEnd];

        Assert.Contains("var isAjaxRequest = IsAjaxRequest();", method, StringComparison.Ordinal);
        Assert.Contains("new JsonResult(new { success = true })", method, StringComparison.Ordinal);
        Assert.Contains(
            "BadRequest(new { success = false, error = errorMessage })",
            method,
            StringComparison.Ordinal);
        Assert.Contains("if (!isAjaxRequest)", method, StringComparison.Ordinal);
    }

    [Fact]
    public void Final_judging_hides_stale_submission_list_and_empty_error_bar()
    {
        var styles = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "css",
            "player-admission-menu.css"));

        Assert.Contains("#game-board-error:empty", styles, StringComparison.Ordinal);
        Assert.Contains(
            """.final-question-host[data-game-status="finaljudging"] .final-submission-list""",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            ".final-question-panel.final-judging-mode .final-submission-list",
            styles,
            StringComparison.Ordinal);
        Assert.Contains("display: none !important;", styles, StringComparison.Ordinal);
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
