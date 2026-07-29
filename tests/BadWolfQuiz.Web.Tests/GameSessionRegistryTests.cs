using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Web.Services;

namespace BadWolfQuiz.Web.Tests;

public sealed class GameSessionRegistryTests
{
    [Fact]
    public void Create_registers_runtime_session()
    {
        var registry = new GameSessionRegistry();
        var quiz = new QuizSnapshot(
            1,
            "Test Quiz",
            [
                new QuizRoundSnapshot(
                    1,
                    "Round 1",
                    0,
                    [new QuizQuestionSnapshot(1, 1, 0, 100, false)])
            ]);

        var session = registry.Create(quiz);

        Assert.Same(session, registry.Find(session.Id));
    }
}
