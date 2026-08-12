using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Services;

namespace BadWolfQuiz.Web.Tests;

public sealed class PlayerRemovalChoiceTests
{
    [Fact]
    public void Remove_then_unblock_allows_player_to_join_again_without_blocked_listing()
    {
        var registry = CreateRegistry("ABC123");
        var game = registry.Create(CreateQuiz());
        var joined = registry.JoinPlayer("ABC123", "Rose");
        registry.ConnectPlayer(
            "ABC123",
            joined.AccessToken!,
            "connection-1",
            true);

        var removal = registry.RemovePlayer("ABC123", joined.Player!.Id);
        var unblocked = registry.UnblockPlayer("ABC123", joined.Player.Id);
        var staleAccess = registry.ConnectPlayer(
            "ABC123",
            joined.AccessToken!,
            "connection-2",
            true);
        var rejoined = registry.JoinPlayer("ABC123", "rose");

        Assert.NotNull(removal);
        Assert.Equal("connection-1", Assert.Single(removal.ConnectionIds));
        Assert.True(unblocked);
        Assert.Empty(registry.GetBlockedPlayers(game));
        Assert.Null(staleAccess);
        Assert.Equal(PlayerJoinStatus.Success, rejoined.Status);
        Assert.Same(joined.Player, rejoined.Player);
        Assert.Contains(rejoined.Player!, game.Session.Players);
        Assert.Empty(game.Session.RemovedPlayers);
    }

    private static GameSessionRegistry CreateRegistry(params string[] codes)
    {
        return new GameSessionRegistry(new StubGameCodeGenerator(codes));
    }

    private static QuizSnapshot CreateQuiz()
    {
        return new QuizSnapshot(
            1,
            "Test Quiz",
            [
                new QuizRoundSnapshot(
                    1,
                    "Round 1",
                    0,
                    [new QuizQuestionSnapshot(1, 1, 0, 100, false)])
            ]);
    }

    private sealed class StubGameCodeGenerator(IEnumerable<string> codes) : IGameCodeGenerator
    {
        private readonly Queue<string> _codes = new(codes);

        public string Create()
        {
            return _codes.Dequeue();
        }
    }
}
