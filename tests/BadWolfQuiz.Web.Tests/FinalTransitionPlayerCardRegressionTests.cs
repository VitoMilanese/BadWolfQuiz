namespace BadWolfQuiz.Web.Tests;

public sealed class FinalTransitionPlayerCardRegressionTests
{
    [Fact]
    public void Player_updates_do_not_rebuild_persistent_cards_during_external_host_flow()
    {
        var markup = File.ReadAllText(FindLobbyView());
        var handlerStart = markup.IndexOf(
            "connection.on(\"PlayersChanged\"",
            StringComparison.Ordinal);
        var handlerEnd = markup.IndexOf(
            "connection.on(\"GameStatusChanged\"",
            handlerStart,
            StringComparison.Ordinal);

        Assert.True(handlerStart >= 0);
        Assert.True(handlerEnd > handlerStart);

        var handler = markup[handlerStart..handlerEnd];
        var guard = handler.IndexOf(
            "if (isExternalHostFlowActive())",
            StringComparison.Ordinal);
        var fingerprint = handler.IndexOf(
            "const fingerprint = playerFingerprint(update.players);",
            StringComparison.Ordinal);
        var boardRender = handler.IndexOf(
            "renderBoardPlayers(update.players);",
            StringComparison.Ordinal);
        var listReplacement = handler.IndexOf(
            "playerList.replaceChildren();",
            StringComparison.Ordinal);

        Assert.True(guard >= 0);
        Assert.Contains(
            "if (isExternalHostFlowActive()) {\n                    return;\n                }",
            handler);
        Assert.True(fingerprint > guard);
        Assert.True(boardRender > guard);
        Assert.True(listReplacement > guard);
    }

    private static string FindLobbyView() => FindWebFile(
        "Pages",
        "Admin",
        "Games",
        "Lobby.cshtml");

    private static string FindWebFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidateParts = new[]
            {
                directory.FullName,
                "src",
                "BadWolfQuiz.Web"
            }.Concat(parts).ToArray();
            var candidate = Path.Combine(candidateParts);
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate BadWolfQuiz.Web file: {Path.Combine(parts)}");
    }
}
