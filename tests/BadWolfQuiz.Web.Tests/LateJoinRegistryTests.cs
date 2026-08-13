using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Web.Services;

namespace BadWolfQuiz.Web.Tests;

public sealed class LateJoinRegistryTests
{
    [Fact]
    public void Final_question_rejects_a_new_player_without_throwing()
    {
        var registry = new GameSessionRegistry(
            new StubGameCodeGenerator(["ABC123"]));
        var game = registry.Create(CreateFinalQuiz());
        var existing = registry.JoinPlayer("ABC123", "Rose").Player!;
        CompleteOnlyQuestion(registry, existing);
        registry.StartFinalQuestion("ABC123");

        var result = registry.JoinPlayer("ABC123", "Mickey");

        Assert.Equal(PlayerJoinStatus.GameClosed, result.Status);
        Assert.DoesNotContain(
            game.Session.Players,
            player => player.Name == "Mickey");
        Assert.Single(game.Session.Players);
    }

    [Fact]
    public void Running_game_still_accepts_a_new_player_when_enabled()
    {
        var registry = new GameSessionRegistry(
            new StubGameCodeGenerator(["ABC123"]));
        registry.Create(CreateQuiz());
        registry.StartGame("ABC123");

        var result = registry.JoinPlayer("ABC123", "Rose");

        Assert.Equal(PlayerJoinStatus.Success, result.Status);
    }

    private static QuizSnapshot CreateQuiz() =>
        new(
            1,
            "Test Quiz",
            [
                new QuizRoundSnapshot(
                    1,
                    "Round 1",
                    0,
                    [new QuizQuestionSnapshot(1, 1, 0, 100, false)])
            ]);

    private static QuizSnapshot CreateFinalQuiz() =>
        new(
            1,
            "Final Quiz",
            [
                new QuizRoundSnapshot(
                    1,
                    "Round 1",
                    0,
                    [new QuizQuestionSnapshot(1, 1, 0, 100, false)])
            ],
            new FinalQuestionSnapshot(
                [CreateTextBlock(1, "Who are you?")],
                [CreateTextBlock(2, "Bad Wolf")]));

    private static ContentBlockSnapshot CreateTextBlock(int id, string text) =>
        new(
            id,
            ContentBlockKind.Text,
            text,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            0,
            false);

    private static void CompleteOnlyQuestion(
        GameSessionRegistry registry,
        BadWolfQuiz.Game.Runtime.GamePlayer player)
    {
        registry.StartGame("ABC123");
        registry.SelectQuestion("ABC123", 1);
        registry.JudgeQuestionAnswer("ABC123", 1, player.Id, true);
        registry.CloseQuestionAnswer("ABC123", 1);
    }

    private sealed class StubGameCodeGenerator(IEnumerable<string> codes)
        : IGameCodeGenerator
    {
        private readonly Queue<string> _codes = new(codes);

        public string Create() => _codes.Dequeue();
    }
}
