using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Game.Runtime;

namespace BadWolfQuiz.Game.Tests;

public sealed class GameTimerTests
{
    private static readonly DateTimeOffset InitialTime =
        new(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Pause_and_resume_preserve_remaining_time()
    {
        var timeProvider = new ManualTimeProvider(InitialTime);
        var timer = new GameTimer(TimeSpan.FromSeconds(30), timeProvider);

        timer.Start();
        timeProvider.Advance(TimeSpan.FromSeconds(8));
        timer.Pause();

        Assert.True(timer.IsPaused);
        Assert.Equal(TimeSpan.FromSeconds(22), timer.Remaining);

        timeProvider.Advance(TimeSpan.FromSeconds(10));
        Assert.Equal(TimeSpan.FromSeconds(22), timer.Remaining);

        timer.Resume();
        timeProvider.Advance(TimeSpan.FromSeconds(2));

        Assert.Equal(GameTimerStatus.Running, timer.Status);
        Assert.Equal(TimeSpan.FromSeconds(20), timer.Remaining);
    }

    [Fact]
    public void Remaining_marks_elapsed_timer_as_expired()
    {
        var timeProvider = new ManualTimeProvider(InitialTime);
        var timer = new GameTimer(TimeSpan.FromSeconds(5), timeProvider);

        timer.Start();
        timeProvider.Advance(TimeSpan.FromSeconds(5));

        Assert.Equal(TimeSpan.Zero, timer.Remaining);
        Assert.Equal(GameTimerStatus.Expired, timer.Status);
    }

    [Fact]
    public void Pause_does_not_change_game_session_status()
    {
        var timeProvider = new ManualTimeProvider(InitialTime);
        var session = GameSession.Create(CreateQuiz(), timeProvider);
        session.AddPlayer("Rose");
        session.Start();
        session.Timer.Start();

        session.Timer.Pause();

        Assert.True(session.Timer.IsPaused);
        Assert.Equal(GameSessionStatus.Running, session.Status);
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
                    [new QuizQuestionSnapshot(100, 10, 0, 100, false)])
            ]);
    }
}
