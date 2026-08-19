namespace BadWolfQuiz.Web.Tests;

public sealed class MandatoryAllPlayerQuestionRegressionTests
{
    [Fact]
    public void Client_exposes_both_answer_modes_and_editor_validation()
    {
        var root = FindRepositoryRoot();
        var script = Read(root,
            "src/BadWolfQuiz.Web/wwwroot/js/all-player-question.js");

        Assert.Contains("All players — text answer", script);
        Assert.Contains("All players — multiple choice", script);
        Assert.Contains("answerCards.length === 1", script);
        Assert.Contains("answerCards.length < 2", script);
        Assert.Contains("answerCards.length > 4", script);
        Assert.Contains("[\"Text\", \"Image\"]", script);
        Assert.Contains("imageCardHasFile", script);
        Assert.Contains("invalidChoiceMedia", script);
    }

    [Fact]
    public void Text_answers_are_manual_and_host_can_close_answering()
    {
        var root = FindRepositoryRoot();
        var endpoint = Read(root,
            "src/BadWolfQuiz.Web/Pages/AllPlayerQuestion.cshtml.cs");
        var registry = Read(root,
            "src/BadWolfQuiz.Web/Services/GameSessionRegistry.cs");
        var registration = Read(root,
            "src/BadWolfQuiz.Web/Services/GameSessionRegistration.cs");
        var host = Read(root,
            "src/BadWolfQuiz.Web/Pages/Admin/Games/Lobby.cshtml");

        Assert.Contains("OnPostJudge", endpoint);
        Assert.Contains("GetOrCreateAllPlayerTextReview", endpoint);
        Assert.DoesNotContain("ConcurrentDictionary<TextReviewKey", endpoint);
        Assert.Contains("CloseAllPlayerQuestionAnswering", registry);
        Assert.Contains("review.Accepting = false", registry);
        Assert.Contains("AllPlayerTextReviewState", registration);
        Assert.Contains("CloseAllPlayerQuestion", host);
        Assert.Contains("AllPlayer_ReviewAnswersNow", host);
    }

    [Fact]
    public void Host_choices_and_answer_preview_are_server_rendered_as_grids()
    {
        var root = FindRepositoryRoot();
        var host = Read(root,
            "src/BadWolfQuiz.Web/Pages/Admin/Games/Lobby.cshtml");
        var preview = Read(root,
            "src/BadWolfQuiz.Web/Pages/Admin/Games/_GameContentPreview.cshtml");
        var styles = Read(root,
            "src/BadWolfQuiz.Web/wwwroot/css/site.css");

        Assert.Contains("data-all-player-server-preview", host);
        Assert.Contains("GetAllPlayerHostChoiceBlocks", host);
        Assert.Contains("answer = true", host);
        Assert.Contains("all-player-multiple-choice-answer-presentation", host);
        Assert.Contains("all-player-multiple-choice-answer-presentation", preview);
        Assert.Contains("grid-template-columns: repeat(2", styles);
        Assert.Contains("display: flex !important", styles);
        Assert.Contains("justify-content: center", styles);
        Assert.Contains("border: 3px solid #c62828", styles);
        Assert.Contains("border-color: #2e7d32", styles);
    }

    [Fact]
    public void Choice_images_are_inline_and_controls_rebuild_after_reconnect()
    {
        var root = FindRepositoryRoot();
        var endpoint = Read(root,
            "src/BadWolfQuiz.Web/Pages/AllPlayerQuestion.cshtml.cs");
        var script = Read(root,
            "src/BadWolfQuiz.Web/wwwroot/js/all-player-question.js");
        var player = Read(root,
            "src/BadWolfQuiz.Web/Pages/Player/Lobby.cshtml");

        Assert.Contains("return File(block.FileData, block.FileContentType);", endpoint);
        Assert.DoesNotContain(
            "File(block.FileData, block.FileContentType, block.FileName)",
            endpoint);
        Assert.Contains("controlsMissing", script);
        Assert.Contains("currentOptionsKey", script);
        Assert.Contains("badwolf:player-session-ready", script);
        Assert.Contains("hostPollNow", script);
        Assert.Contains("badwolf:host-gameplay-updated", script);
        Assert.Contains("refreshTransitionToken", player);
        Assert.Contains("TimeSpan.FromHours(1)", Read(root,
            "src/BadWolfQuiz.Web/Services/GameSessionRegistry.cs"));
    }

    [Fact]
    public void All_player_asset_is_hash_versioned_in_layout()
    {
        var root = FindRepositoryRoot();
        var layout = Read(root,
            "src/BadWolfQuiz.Web/Pages/Shared/_Layout.cshtml");
        var bootstrap = Read(root,
            "src/BadWolfQuiz.Web/wwwroot/js/quick-timer-controls.js");

        Assert.Contains("~/js/all-player-question.js", layout);
        Assert.Contains("asp-append-version=\"true\"", layout);
        Assert.DoesNotContain("loadAllPlayerQuestionClient", bootstrap);
        Assert.DoesNotContain("all-player-question.js?v=", bootstrap);
    }

    private static string Read(string root, string relativePath) =>
        File.ReadAllText(Path.Combine(
            root,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));

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
