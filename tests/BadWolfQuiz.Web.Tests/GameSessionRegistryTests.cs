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

    [Fact]
    public void JoinPlayer_returns_access_token_for_presence_connection()
    {
        var registry = CreateRegistry("ABC123");
        registry.Create(CreateQuiz());

        var joined = registry.JoinPlayer("ABC123", "Rose");
        var connected = registry.ConnectPlayer(
            "ABC123",
            joined.AccessToken!,
            "connection-1",
            true);

        Assert.NotNull(joined.AccessToken);
        Assert.NotNull(connected);
        Assert.Equal(
            PlayerPresenceStatus.Active,
            registry.GetPlayerLobbyEntries(joined.Game!).Single().Presence);
    }

    [Fact]
    public void Presence_changes_when_page_becomes_inactive_or_disconnects()
    {
        var registry = CreateRegistry("ABC123");
        registry.Create(CreateQuiz());
        var joined = registry.JoinPlayer("ABC123", "Rose");
        registry.ConnectPlayer("ABC123", joined.AccessToken!, "connection-1", true);

        registry.SetPlayerVisibility("connection-1", false);
        var inactive = registry.GetPlayerLobbyEntries(joined.Game!).Single();

        registry.DisconnectPlayer("connection-1");
        var disconnected = registry.GetPlayerLobbyEntries(joined.Game!).Single();

        Assert.Equal(PlayerPresenceStatus.Inactive, inactive.Presence);
        Assert.Equal(PlayerPresenceStatus.Disconnected, disconnected.Presence);
        Assert.Equal(0, disconnected.Score);
    }

    [Fact]
    public void Running_game_requires_host_approval_before_player_rejoins()
    {
        var registry = CreateRegistry("ABC123");
        var game = registry.Create(CreateQuiz());
        var joined = registry.JoinPlayer("ABC123", "Rose");
        registry.ConnectPlayer("ABC123", joined.AccessToken!, "connection-1", true);
        registry.StartGame("ABC123");
        registry.DisconnectPlayer("connection-1");

        var reconnect = registry.ConnectPlayer(
            "ABC123",
            joined.AccessToken!,
            "connection-2",
            true);

        Assert.True(reconnect!.RequiresApproval);
        Assert.Equal(
            PlayerPresenceStatus.RejoinPending,
            registry.GetPlayerLobbyEntries(game).Single().Presence);

        var approval = registry.ApprovePlayerRejoin(
            "ABC123",
            joined.Player!.Id);

        Assert.Equal("connection-2", Assert.Single(approval!.ConnectionIds));
        var restored = Assert.Single(registry.GetPlayerLobbyEntries(game));
        Assert.Equal(PlayerPresenceStatus.Active, restored.Presence);
        Assert.Equal(joined.Player.Id, restored.Id);
        Assert.Equal(joined.Player.Score, restored.Score);
    }

    [Fact]
    public void SelectQuestion_routes_command_to_runtime_session()
    {
        var registry = CreateRegistry("ABC123");
        var game = registry.Create(CreateQuiz());
        registry.JoinPlayer("ABC123", "Rose");
        registry.StartGame("ABC123");

        var question = registry.SelectQuestion("ABC123", 1);

        Assert.NotNull(question);
        Assert.Equal(
            BadWolfQuiz.Game.Runtime.RuntimeQuestionStatus.Selected,
            question.Status);
        Assert.Same(
            game.Session.Board.Questions.Single(),
            question);
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
