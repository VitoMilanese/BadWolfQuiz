namespace BadWolfQuiz.Web.Tests;

public sealed class PeerRatedQuestionContextTests
{
    [Fact]
    public void Peer_review_shows_the_rendered_question_above_the_reviewed_answer()
    {
        var script = File.ReadAllText(FindRepoFile(
            "src", "BadWolfQuiz.Web", "wwwroot", "js",
            "peer-rated-question-context.js"));

        Assert.Contains("peer-rated-reviewing", script, StringComparison.Ordinal);
        Assert.Contains("peer-rated-question-context", script, StringComparison.Ordinal);
        Assert.Contains("question-presentation", script, StringComparison.Ordinal);
        Assert.Contains(".game-content-blocks", script, StringComparison.Ordinal);
        Assert.Contains("cloneNode(true)", script, StringComparison.Ordinal);
        Assert.Contains("sourceQuestionId", script, StringComparison.Ordinal);
        Assert.Contains(".peer-rated-host-stage", script, StringComparison.Ordinal);
        Assert.Contains("setPixelValue(stage, \"top\"", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Cloned_question_media_does_not_restart_autoplay_during_peer_review()
    {
        var script = File.ReadAllText(FindRepoFile(
            "src", "BadWolfQuiz.Web", "wwwroot", "js",
            "peer-rated-question-context.js"));

        Assert.Contains("[data-autoplay-media]", script, StringComparison.Ordinal);
        Assert.Contains("data-autoplay-media\", \"false", script, StringComparison.Ordinal);
        Assert.Contains("[data-youtube-autoplay]", script, StringComparison.Ordinal);
        Assert.Contains("data-youtube-autoplay\", \"false", script, StringComparison.Ordinal);
        Assert.Contains("[autoplay]", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Peer_review_question_context_respects_sidebar_and_refresh_layout_changes()
    {
        var script = File.ReadAllText(FindRepoFile(
            "src", "BadWolfQuiz.Web", "wwwroot", "js",
            "peer-rated-question-context.js"));

        Assert.Contains(".peer-rated-host-sidebar", script, StringComparison.Ordinal);
        Assert.Contains("--peer-rated-question-context-right-gap", script, StringComparison.Ordinal);
        Assert.Contains("badwolf:host-gameplay-updated", script, StringComparison.Ordinal);
        Assert.Contains("badwolf:host-shell-mounted", script, StringComparison.Ordinal);
        Assert.Contains("pageshow", script, StringComparison.Ordinal);
        Assert.Contains("childList: true", script, StringComparison.Ordinal);
        Assert.Contains("subtree: true", script, StringComparison.Ordinal);
        Assert.DoesNotContain("attributes: true", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Question_context_helper_loads_after_existing_peer_rated_layout_scripts()
    {
        var tagHelper = File.ReadAllText(FindRepoFile(
            "src", "BadWolfQuiz.Web", "TagHelpers",
            "PeerRatedAllPlayerAssetsTagHelper.cs"));

        var runtime = tagHelper.IndexOf(
            "peer-rated-all-player-question.js?v=2",
            StringComparison.Ordinal);
        var polish = tagHelper.IndexOf(
            "peer-rated-all-player-polish.js?v=3",
            StringComparison.Ordinal);
        var context = tagHelper.IndexOf(
            "peer-rated-question-context.js?v=1",
            StringComparison.Ordinal);

        Assert.True(runtime >= 0);
        Assert.True(polish > runtime);
        Assert.True(context > polish);
    }

    private static string FindRepoFile(params string[] relativeParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var parts = new string[relativeParts.Length + 1];
            parts[0] = directory.FullName;
            Array.Copy(relativeParts, 0, parts, 1, relativeParts.Length);
            var candidate = Path.Combine(parts);
            if (File.Exists(candidate))
            {
                return candidate;
            }
            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not find repository file: {Path.Combine(relativeParts)}");
    }
}
