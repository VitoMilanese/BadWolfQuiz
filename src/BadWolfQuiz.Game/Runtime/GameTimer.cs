namespace BadWolfQuiz.Game.Runtime;

public sealed class GameTimer
{
    private readonly TimeProvider _timeProvider;
    private DateTimeOffset? _lastStartedAtUtc;
    private TimeSpan _remaining;

    public GameTimer(TimeSpan duration, TimeProvider? timeProvider = null)
    {
        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "Timer duration must be positive.");
        }

        Duration = duration;
        _remaining = duration;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public TimeSpan Duration { get; }

    public GameTimerStatus Status { get; private set; } = GameTimerStatus.Stopped;

    public bool IsPaused => Status == GameTimerStatus.Paused;

    public TimeSpan Remaining
    {
        get
        {
            RefreshExpiration();
            return CalculateRemaining();
        }
    }

    public void Start()
    {
        if (Status != GameTimerStatus.Stopped)
        {
            throw new GameRuleViolationException("Only a stopped timer can be started.");
        }

        _remaining = Duration;
        _lastStartedAtUtc = _timeProvider.GetUtcNow();
        Status = GameTimerStatus.Running;
    }

    public void Pause()
    {
        RefreshExpiration();

        if (Status != GameTimerStatus.Running)
        {
            throw new GameRuleViolationException("Only a running timer can be paused.");
        }

        _remaining = CalculateRemaining();
        _lastStartedAtUtc = null;
        Status = GameTimerStatus.Paused;
    }

    public void Resume()
    {
        if (Status != GameTimerStatus.Paused)
        {
            throw new GameRuleViolationException("Only a paused timer can be resumed.");
        }

        _lastStartedAtUtc = _timeProvider.GetUtcNow();
        Status = GameTimerStatus.Running;
    }

    private TimeSpan CalculateRemaining()
    {
        if (Status != GameTimerStatus.Running || _lastStartedAtUtc is null)
        {
            return _remaining;
        }

        var remaining = _remaining - (_timeProvider.GetUtcNow() - _lastStartedAtUtc.Value);
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    private void RefreshExpiration()
    {
        if (Status == GameTimerStatus.Running && CalculateRemaining() == TimeSpan.Zero)
        {
            _remaining = TimeSpan.Zero;
            _lastStartedAtUtc = null;
            Status = GameTimerStatus.Expired;
        }
    }
}

public enum GameTimerStatus
{
    Stopped = 1,
    Running = 2,
    Paused = 3,
    Expired = 4
}
