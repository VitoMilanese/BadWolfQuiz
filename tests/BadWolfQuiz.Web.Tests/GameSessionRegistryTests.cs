using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Web.Services;

namespace BadWolfQuiz.Web.Tests;

public sealed class GameSessionRegistryTests
{
    [Fact]
    public void Create_registers_runtime_session()
    {
        var registry = CreateRegistry("ABC123");
        var quiz = CreateQuiz();

        var game = registry.Create(quiz);

        Assert.Same(game, registry.Find(game.Session.Id));
        Assert.Same(game, registry.Find("abc123"));
        Assert.Equal("ABC123", game.PublicCode);
    }

    [Fact]
    public void Create_retries_when_public_code_already_exists()
    {
        var registry = CreateRegistry("ABC123", "ABC123", "WOLF42");

        registry.Create(CreateQuiz());
        var secondGame = registry.Create(CreateQuiz());

        Assert.Equal("WOLF42", secondGame.PublicCode);
    }

    [Fact]
    public void JoinPlayer_adds_player_to_matching_game()
    {
        var registry = CreateRegistry("ABC123");
        var game = registry.Create(CreateQuiz());

        var result = registry.JoinPlayer(" abc123 ", "  Rose  ");

        Assert.Equal(PlayerJoinStatus.Success, result.Status);
        Assert.Same(game, result.Game);
        Assert.Equal("Rose", result.Player!.Name);
        Assert.Single(registry.GetPlayers(game));
    }

    [Fact]
    public void JoinPlayer_rejects_duplicate_name_ignoring_case()
    {
        var registry = CreateRegistry("ABC123");
        registry.Create(CreateQuiz());
        registry.JoinPlayer("ABC123", "Rose");

        var result = registry.JoinPlayer("ABC123", "rose");

        Assert.Equal(PlayerJoinStatus.NameAlreadyUsed, result.Status);
    }

    [Fact]
    public void JoinPlayer_returns_not_found_for_unknown_code()
    {
        var registry = CreateRegistry("ABC123");

        var result = registry.JoinPlayer("BAD999", "Rose");

        Assert.Equal(PlayerJoinStatus.GameNotFound, result.Status);
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
