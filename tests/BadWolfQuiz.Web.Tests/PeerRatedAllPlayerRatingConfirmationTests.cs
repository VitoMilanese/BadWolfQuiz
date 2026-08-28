namespace BadWolfQuiz.Web.Tests;

public sealed class PeerRatedAllPlayerRatingConfirmationTests
{
    [Fact]
    public void Player_rating_is_drafted_locally_until_confirmation()
    {
        var script = File.ReadAllText(FindRepoFile(
            "src", "BadWolfQuiz.Web", "wwwroot", "js",
            "peer-rated-all-player-rating-confirmation.js"));

        Assert.Contains("confirmRating", script, StringComparison.Ordinal);
        Assert.Contains("peer-rated-confirm-rating-button", script, StringComparison.Ordinal);
        Assert.Contains("peerRatedDraftRating", script, StringComparison.Ordinal);
        Assert.Contains("event.stopImmediatePropagation()", script, StringComparison.Ordinal);
        Assert.Contains("submitBypass = true", script, StringComparison.Ordinal);
        Assert.Contains(
            "input.dispatchEvent(new Event(\"change\", { bubbles: true }))",
            script,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Zero_star_action_is_centered_and_uses_the_same_confirmation_step()
    {
        var script = File.ReadAllText(FindRepoFile(
            "src", "BadWolfQuiz.Web", "wwwroot", "js",
            "peer-rated-all-player-rating-confirmation.js"));

        Assert.Contains(
            ".peer-rated-rating-editor .peer-rated-zero-button",
            script,
            StringComparison.Ordinal);
        Assert.Contains("margin: .6rem auto 0 !important", script, StringComparison.Ordinal);
        Assert.Contains("setDraft(editor, 0)", script, StringComparison.Ordinal);
        Assert.Contains("editor.zero.click()", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Star_labels_have_stable_mobile_touch_targets_for_all_rating_controls()
    {
        var script = File.ReadAllText(FindRepoFile(
            "src", "BadWolfQuiz.Web", "wwwroot", "js",
            "peer-rated-all-player-rating-confirmation.js"));

        Assert.Contains(".star-rating label:not(.zero-rating)", script, StringComparison.Ordinal);
        Assert.Contains("min-inline-size: 44px", script, StringComparison.Ordinal);
        Assert.Contains("min-block-size: 44px", script, StringComparison.Ordinal);
        Assert.Contains("touch-action: manipulation", script, StringComparison.Ordinal);
        Assert.Contains("activateStarLabel", script, StringComparison.Ordinal);
        Assert.Contains("document.getElementById(label.htmlFor)", script, StringComparison.Ordinal);
        Assert.Contains("event.preventDefault()", script, StringComparison.Ordinal);
        Assert.Contains("input.checked = true", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Host_vote_values_are_masked_until_the_results_pass()
    {
        var script = File.ReadAllText(FindRepoFile(
            "src", "BadWolfQuiz.Web", "wwwroot", "js",
            "peer-rated-all-player-rating-confirmation.js"));

        Assert.Contains("ratedStatus", script, StringComparison.Ordinal);
        Assert.Contains("peer-rated-result-summary", script, StringComparison.Ordinal);
        Assert.Contains("if (ui.querySelector(\".peer-rated-result-summary\"))", script, StringComparison.Ordinal);
        Assert.Contains("status.textContent = text.ratedStatus", script, StringComparison.Ordinal);
        Assert.Contains("[0-5]", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Confirmation_behavior_is_loaded_after_the_base_peer_rating_script()
    {
        var tagHelper = File.ReadAllText(FindRepoFile(
            "src", "BadWolfQuiz.Web", "TagHelpers",
            "PeerRatedAllPlayerAssetsTagHelper.cs"));

        var baseScript = tagHelper.IndexOf(
            "peer-rated-all-player-question.js?v=2",
            StringComparison.Ordinal);
        var confirmationScript = tagHelper.IndexOf(
            "peer-rated-all-player-rating-confirmation.js?v=2",
            StringComparison.Ordinal);

        Assert.True(baseScript >= 0);
        Assert.True(confirmationScript > baseScript);
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
            $"Could not locate repository file: {Path.Combine(relativeParts)}");
    }
}
