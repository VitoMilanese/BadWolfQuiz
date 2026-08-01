using System.Text.Json;
using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Services;

namespace BadWolfQuiz.Web.Tests;

public sealed class ActiveGameSnapshotTests
{
    [Fact]
    public void Snapshot_round_trips_through_json()
    {
        var quiz = new QuizSnapshot(
            42,
            "Recovery quiz",
            [
                new QuizRoundSnapshot(
                    7,
                    "Round 1",
                    0,
                    [new QuizQuestionSnapshot(9, 3, 0, 100, false)])
            ]);
        var session = GameSession.Create(quiz);
        var player = session.AddPlayer("Rose");
        session.AdjustPlayerScore(player.Id, 250);
        session.Start();
        var snapshot = new ActiveGameSnapshot(
            "ABC123",
            "host-1",
            true,
            quiz,
            session.Settings,
            session.CaptureState());

        var json = JsonSerializer.Serialize(snapshot);
        var restored = JsonSerializer.Deserialize<ActiveGameSnapshot>(json);

        Assert.NotNull(restored);
        Assert.Equal(42, restored.Quiz.SourceQuizId);
        Assert.Equal(session.Id, restored.SessionState.Id);
        Assert.Equal(250, restored.SessionState.Players.Single().Score);
    }
}
