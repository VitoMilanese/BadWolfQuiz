namespace BadWolfQuiz.Web.Tests;

public sealed class PlayerJoinNavigationRegressionTests
{
    [Fact]
    public void Successful_join_redirect_is_returned_without_awaiting_players_changed_broadcast()
    {
        var source = File.ReadAllText(FindWebFile(
            "Pages",
            "Join",
            "Index.cshtml.cs"));

        Assert.Contains("public IActionResult OnPost()", source, StringComparison.Ordinal);
        Assert.Contains("var redirect = RedirectToPage(", source, StringComparison.Ordinal);
        Assert.Contains(
            "_ = BroadcastPlayersChangedBestEffortAsync(game, player.Id);",
            source,
            StringComparison.Ordinal);
        Assert.Contains("return redirect;", source, StringComparison.Ordinal);
        Assert.Contains(
            "new CancellationTokenSource(TimeSpan.FromSeconds(2))",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "player navigation already continues",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "await BroadcastPlayersChangedBestEffortAsync",
            source,
            StringComparison.Ordinal);

        var postStart = source.IndexOf(
            "public IActionResult OnPost()",
            StringComparison.Ordinal);
        var helperStart = source.IndexOf(
            "private async Task BroadcastPlayersChangedBestEffortAsync",
            StringComparison.Ordinal);
        Assert.True(postStart >= 0 && helperStart > postStart);
        var postBody = source[postStart..helperStart];
        Assert.DoesNotContain("await gameHub.Clients", postBody, StringComparison.Ordinal);
    }

    [Fact]
    public void Join_form_does_not_reuse_stale_current_player_navigation()
    {
        var source = File.ReadAllText(FindWebFile(
            "Pages",
            "Join",
            "Index.cshtml"));

        Assert.Contains(
            "localStorage.removeItem(\n                    `badwolfquiz:${gameCode}:current-player`);",
            source.ReplaceLineEndings("\n"),
            StringComparison.Ordinal);
        Assert.DoesNotContain("window.location.assign(identity.path)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("const storedIdentity = localStorage.getItem(", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Join_form_blocks_duplicate_submissions_while_navigation_is_in_progress()
    {
        var source = File.ReadAllText(FindWebFile(
            "Pages",
            "Join",
            "Index.cshtml"));

        Assert.Contains("let joinSubmissionInProgress = false", source, StringComparison.Ordinal);
        Assert.Contains("if (joinSubmissionInProgress)", source, StringComparison.Ordinal);
        Assert.Contains("event.preventDefault();", source, StringComparison.Ordinal);
        Assert.Contains("submitButton.disabled = true", source, StringComparison.Ordinal);
        Assert.Contains("lockJoinSubmission();", source, StringComparison.Ordinal);
    }

    private static string FindWebFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                new[] { directory.FullName, "src", "BadWolfQuiz.Web" }
                    .Concat(parts)
                    .ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate BadWolfQuiz.Web file: {Path.Combine(parts)}");
    }
}
