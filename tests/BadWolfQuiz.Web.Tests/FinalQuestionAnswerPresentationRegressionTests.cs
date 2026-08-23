namespace BadWolfQuiz.Web.Tests;

public sealed class FinalQuestionAnswerPresentationRegressionTests
{
    [Fact]
    public void Final_answer_is_rendered_before_final_results_with_existing_media_support()
    {
        var root = FindRepositoryRoot();
        var lobby = Read(
            root, "src", "BadWolfQuiz.Web", "Pages", "Admin", "Games", "Lobby.cshtml");
        var pageModel = Read(
            root, "src", "BadWolfQuiz.Web", "Pages", "Admin", "Games", "Lobby.cshtml.cs");
        var registry = Read(
            root, "src", "BadWolfQuiz.Web", "Services", "GameSessionRegistry.cs");

        Assert.Contains(
            "final.Status == BadWolfQuiz.Game.Runtime.FinalQuestionStatus.AnswerPresentation",
            lobby,
            StringComparison.Ordinal);
        Assert.Contains("else if (showFinalAnswer)", lobby, StringComparison.Ordinal);
        Assert.Contains("final.Definition.AnswerBlocks", lobby, StringComparison.Ordinal);
        Assert.Contains("data-media-autoplay-state=\"final-answer\"", lobby, StringComparison.Ordinal);
        Assert.Contains(
            "ResolveContentBlockAutoplay(block, answer: true, final: true)",
            lobby,
            StringComparison.Ordinal);
        Assert.Contains("answer = true", lobby, StringComparison.Ordinal);
        Assert.Contains(
            "asp-page-handler=\"CompleteFinalQuestion\"",
            lobby,
            StringComparison.Ordinal);

        var answerIndex = lobby.IndexOf("else if (showFinalAnswer)", StringComparison.Ordinal);
        var resultsIndex = lobby.IndexOf("@if (Model.FinalStandings.Count > 0)", StringComparison.Ordinal);
        Assert.True(answerIndex >= 0);
        Assert.True(resultsIndex > answerIndex);

        Assert.Contains("OnPostCompleteFinalQuestionAsync", pageModel, StringComparison.Ordinal);
        Assert.Contains(
            "sessionRegistry.CompleteFinalQuestion(game.PublicCode)",
            pageModel,
            StringComparison.Ordinal);
        Assert.Contains(
            "public GameSessionRegistration? CompleteFinalQuestion(string publicCode)",
            registry,
            StringComparison.Ordinal);
    }

    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(parts).ToArray()));

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
