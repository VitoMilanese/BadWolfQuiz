using BadWolfQuiz.Game.Runtime;

namespace BadWolfQuiz.Game.Tests;

public sealed class HostControlledTimeoutTests
{
    private static readonly DateTimeOffset InitialTime =
        new(2026, 8, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Expired_timer_can_be_extended_and_resumes()
    {
        var timeProvider = new ManualTimeProvider(InitialTime);
        var timer = new GameTimer(TimeSpan.FromSeconds(10), timeProvider);
        timer.Start();
        timeProvider.Advance(TimeSpan.FromSeconds(10));

        Assert.True(timer.ConsumeExpiration());
        Assert.False(timer.ConsumeExpiration());
        Assert.Equal(GameTimerStatus.Expired, timer.Status);

        timer.Add(TimeSpan.FromSeconds(15));

        Assert.Equal(GameTimerStatus.Running, timer.Status);
        Assert.Equal(TimeSpan.FromSeconds(15), timer.Remaining);
    }
}
