namespace BadWolfQuiz.Web.Tests;

public sealed class FinalQuestionLiveProgressRegressionTests
{
    [Fact]
    public void Shared_gameplay_bootstrap_guards_final_progress_before_page_handlers_run()
    {
        var script = File.ReadAllText(FindFile(
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "gameplay-escape-shortcuts.js"));

        Assert.Contains("installFinalQuestionProgressGuard();", script);
        Assert.Contains("window.signalR?.HubConnection?.prototype", script);
        Assert.Contains("methodName.toLowerCase() !== \"finalquestionprogresschanged\"", script);
        Assert.Contains("hubConnectionPrototype.on = function(methodName, handler)", script);
    }

    [Fact]
    public void Other_players_final_progress_does_not_run_the_page_reload_handler()
    {
        var script = File.ReadAllText(FindFile(
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "gameplay-escape-shortcuts.js"));
        var guardedHandler = ExtractBetween(
            script,
            "return registerHandler.call(this, methodName, (...args) => {",
            "return handler(...args);");

        Assert.Contains(".player-lobby[data-player-id]", guardedHandler);
        Assert.Contains("return;", guardedHandler);
        Assert.Contains(".host-game-board.final-question-host", guardedHandler);
        Assert.Contains("requestHostFinalProgressSync();", guardedHandler);
    }

    [Fact]
    public void Host_final_progress_updates_only_submission_rows_and_lock_controls()
    {
        var script = File.ReadAllText(FindFile(
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "gameplay-escape-shortcuts.js"));
        var sync = ExtractBetween(
            script,
            "const syncFinalSubmissionRows = (currentBoard, nextBoard) => {",
            "const syncHostFinalProgressOnce = async () => {");

        Assert.Contains(".final-submission-list", sync);
        Assert.Contains(":scope > strong + span", sync);
        Assert.Contains("currentStatus.textContent = nextStatus.textContent;", sync);
        Assert.Contains("currentFallback.remove();", sync);
        Assert.Contains("syncLockButton(currentBoard, nextBoard, \"LockFinalWagers\")", sync);
        Assert.Contains("syncLockButton(currentBoard, nextBoard, \"LockFinalAnswers\")", sync);
        Assert.DoesNotContain("replaceChildren", sync);
        Assert.DoesNotContain("window.location.reload", sync);
    }

    [Fact]
    public void Player_own_submission_still_uses_final_state_change_to_lock_the_form()
    {
        var playerPage = File.ReadAllText(FindFile(
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Player",
            "Lobby.cshtml"));

        Assert.Contains("connection.on(\"FinalQuestionStateChanged\"", playerPage);
        Assert.Contains("reloadForGameTransition().catch(console.error);", playerPage);
        Assert.Contains("connection.on(\"FinalQuestionProgressChanged\"", playerPage);
    }

    private static string ExtractBetween(
        string source,
        string startMarker,
        string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Could not find {startMarker}.");

        var end = source.IndexOf(
            endMarker,
            start + startMarker.Length,
            StringComparison.Ordinal);
        Assert.True(end > start, $"Could not find {endMarker}.");

        return source[start..end];
    }

    private static string FindFile(params string[] pathParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var parts = new[] { directory.FullName }.Concat(pathParts).ToArray();
            var candidate = Path.Combine(parts);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate {Path.Combine(pathParts)} from the test output directory.");
    }
}
