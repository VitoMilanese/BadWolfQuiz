namespace BadWolfQuiz.Web.Tests;

public sealed class PlayerMobileRecoveryRegressionTests
{
    [Fact]
    public void Player_page_loads_mobile_recovery_adapter_before_runtime_connection()
    {
        var helper = File.ReadAllText(FindWebFile(
            "TagHelpers",
            "GameplayPolishAssetsTagHelper.cs"));
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "player-mobile-recovery.js"));

        Assert.Contains(
            "/js/player-mobile-recovery.js?v=1",
            helper,
            StringComparison.Ordinal);
        Assert.Contains(
            "Object.defineProperty(window, \"signalR\"",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "connection.start = startWithRetry;",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "connection.onclose(() =>",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"JoinPlayerSession\"",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "document.visibilityState === \"visible\"",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "connection.invoke(\"SetPlayerVisibility\", true)",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "document.dispatchEvent(new Event(\"badwolf:player-session-ready\"))",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "document.addEventListener(\"visibilitychange\", recoverWhenVisible)",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "window.addEventListener(\"pageshow\", recoverWhenVisible)",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "window.addEventListener(\"online\", recoverWhenVisible)",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "connection.on(\"RejoinApprovalRequired\"",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "return !rejoinApprovalRequired;",
            script,
            StringComparison.Ordinal);
    }

    private static string FindWebFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src",
                "BadWolfQuiz.Web",
                Path.Combine(segments));
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not find BadWolfQuiz.Web file: {Path.Combine(segments)}");
    }
}
