namespace BadWolfQuiz.Web.Tests;

public sealed class PlayerJoinNavigationRegressionTests
{
    [Fact]
    public void Successful_join_redirect_does_not_depend_on_players_changed_broadcast()
    {
        var source = File.ReadAllText(FindWebFile(
            "Pages",
            "Join",
            "Index.cshtml.cs"));

        Assert.Contains("var redirect = RedirectToPage(", source, StringComparison.Ordinal);
        Assert.Contains("CancellationToken.None", source, StringComparison.Ordinal);
        Assert.Contains("catch (Exception exception)", source, StringComparison.Ordinal);
        Assert.Contains("continuing with player redirect", source, StringComparison.Ordinal);
        Assert.Contains("return redirect;", source, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "OnPostAsync(CancellationToken cancellationToken)",
            source,
            StringComparison.Ordinal);
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
