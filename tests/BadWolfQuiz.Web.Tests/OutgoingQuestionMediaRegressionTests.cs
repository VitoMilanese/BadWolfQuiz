namespace BadWolfQuiz.Web.Tests;

public sealed class OutgoingQuestionMediaRegressionTests
{
    [Fact]
    public void Manual_all_player_text_review_request_stops_question_media_immediately()
    {
        var root = FindRepositoryRoot();
        var autoplay = Read(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "media-autoplay.js");
        var lobby = Read(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "Games",
            "Lobby.cshtml");

        Assert.Contains(
            "data-all-player-review-action",
            lobby,
            StringComparison.Ordinal);
        Assert.Contains(
            "const stopOnAllPlayerReviewRequest = event =>",
            autoplay,
            StringComparison.Ordinal);
        Assert.Contains(
            "form.matches(\"[data-all-player-review-action]\")",
            autoplay,
            StringComparison.Ordinal);
        Assert.Contains(
            "stopActivePlayback(null);",
            autoplay,
            StringComparison.Ordinal);
        Assert.Contains(
            "document.addEventListener(\"submit\", stopOnAllPlayerReviewRequest, true);",
            autoplay,
            StringComparison.Ordinal);
        Assert.Contains("media.pause();", autoplay, StringComparison.Ordinal);
        Assert.Contains("media.currentTime = 0;", autoplay, StringComparison.Ordinal);
    }

    [Fact]
    public void Automatic_all_player_text_review_stops_question_media_when_review_is_added()
    {
        var root = FindRepositoryRoot();
        var autoplay = Read(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "media-autoplay.js");
        var allPlayer = Read(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "all-player-question.js");

        Assert.Contains(
            "const findAllPlayerTextReview = node =>",
            autoplay,
            StringComparison.Ordinal);
        Assert.Contains(
            "node.matches(\".all-player-host-review\")",
            autoplay,
            StringComparison.Ordinal);
        Assert.Contains(
            "const observeAllPlayerTextReview = () =>",
            autoplay,
            StringComparison.Ordinal);
        Assert.Contains(
            "record.addedNodes",
            autoplay,
            StringComparison.Ordinal);
        Assert.Contains(
            "if (findAllPlayerTextReview(node))",
            autoplay,
            StringComparison.Ordinal);
        Assert.Contains(
            "stopActivePlayback(null);",
            autoplay,
            StringComparison.Ordinal);
        Assert.Contains(
            "observer.observe(view, { childList: true, subtree: true });",
            autoplay,
            StringComparison.Ordinal);

        var renderStart = allPlayer.IndexOf(
            "const renderTextReview = (board, state) =>",
            StringComparison.Ordinal);
        var renderEnd = allPlayer.IndexOf(
            "const requestRefresh = (state, expectedSelector) =>",
            renderStart,
            StringComparison.Ordinal);

        Assert.True(renderStart >= 0);
        Assert.True(renderEnd > renderStart);

        var renderTextReview = allPlayer[renderStart..renderEnd];
        Assert.Contains(
            "state.phase !== \"judging\"",
            renderTextReview,
            StringComparison.Ordinal);
        Assert.Contains(
            "review.className = \"all-player-host-review final-judging-list\";",
            renderTextReview,
            StringComparison.Ordinal);
        Assert.Contains(
            "target.appendChild(review);",
            renderTextReview,
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
