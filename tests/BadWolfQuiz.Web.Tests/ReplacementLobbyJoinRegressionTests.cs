using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Services;

namespace BadWolfQuiz.Web.Tests;

public sealed class ReplacementLobbyJoinRegressionTests
{
    [Fact]
    public void Replacement_lobby_accepts_player_while_previous_unfinished_game_is_still_registered()
    {
        var registry = new GameSessionRegistry(
            new SequenceGameCodeGenerator(["OLD111", "NEW222"]));

        var oldGame = registry.Create(
            CreateQuiz(),
            GameSessionSettings.Default,
            "host-1");
        var oldJoin = registry.JoinPlayer(oldGame.PublicCode, "Rose");
        Assert.Equal(PlayerJoinStatus.Success, oldJoin.Status);
        registry.StartGame(oldGame.PublicCode);
        registry.SelectQuestion(oldGame.PublicCode, 100);

        var replacement = registry.Create(
            CreateQuiz(),
            GameSessionSettings.Default);
        replacement.AssignHost("host-1");

        var replacementJoin = registry.JoinPlayer(
            replacement.PublicCode,
            "Mickey");

        Assert.Equal(PlayerJoinStatus.Success, replacementJoin.Status);
        Assert.Same(replacement, replacementJoin.Game);
        Assert.NotNull(replacementJoin.Player);
        Assert.Equal("Mickey", replacementJoin.Player.Name);
        Assert.False(string.IsNullOrWhiteSpace(replacementJoin.AccessToken));
        Assert.Same(oldGame, registry.Find(oldGame.PublicCode));
        Assert.Same(replacement, registry.Find(replacement.PublicCode));
    }

    private static QuizSnapshot CreateQuiz()
    {
        var question = new QuizQuestionSnapshot(
            100,
            10,
            0,
            200,
            false,
            "Question");
        return new QuizSnapshot(
            1,
            "Replacement lobby join",
            [new QuizRoundSnapshot(1, "Round 1", 0, [question])]);
    }

    private sealed class SequenceGameCodeGenerator(string[] codes)
        : IGameCodeGenerator
    {
        private int _index;

        public string Create()
        {
            if (_index >= codes.Length)
            {
                throw new InvalidOperationException("No test game codes remain.");
            }

            return codes[_index++];
        }
    }
}
