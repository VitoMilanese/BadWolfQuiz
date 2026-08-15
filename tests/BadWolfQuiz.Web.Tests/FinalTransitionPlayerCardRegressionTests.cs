using System.Text.RegularExpressions;

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
        var guardMatch = Regex.Match(
            handler,
            @"if\s*\(isExternalHostFlowActive\(\)\)\s*\{\s*return;\s*\}",
            RegexOptions.CultureInvariant);
        var fingerprint = handler.IndexOf(
            "const fingerprint = playerFingerprint(update.players);",
            StringComparison.Ordinal);
        var boardRender = handler.IndexOf(
            "renderBoardPlayers(update.players);",
            StringComparison.Ordinal);
        var listReplacement = handler.IndexOf(
            "playerList.replaceChildren();",
            StringComparison.Ordinal);

        Assert.True(guardMatch.Success);
        Assert.True(fingerprint > guardMatch.Index);
        Assert.True(boardRender > guardMatch.Index);
        Assert.True(listReplacement > guardMatch.Index);
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
