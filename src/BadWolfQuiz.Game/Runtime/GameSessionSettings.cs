namespace BadWolfQuiz.Game.Runtime;

public sealed record GameSessionSettings
{
    public static GameSessionSettings Default { get; } = new(
        GameSession.DefaultBuzzerDuration,
        GameSession.DefaultAnswerDuration,
        GamePhaseStartMode.Manual,
        GamePhaseStartMode.Automatic);

    public GameSessionSettings(
        TimeSpan buzzerDuration,
        TimeSpan answerDuration,
        GamePhaseStartMode regularQuestionBuzzerStartMode,
        GamePhaseStartMode wagerQuestionAnswerTimerStartMode)
    {
        if (buzzerDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(buzzerDuration),
                "Buzzer duration must be positive.");
        }

        if (answerDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(answerDuration),
                "Answer duration must be positive.");
        }

        BuzzerDuration = buzzerDuration;
        AnswerDuration = answerDuration;
        RegularQuestionBuzzerStartMode = regularQuestionBuzzerStartMode;
        WagerQuestionAnswerTimerStartMode = wagerQuestionAnswerTimerStartMode;
    }

    public TimeSpan BuzzerDuration { get; }

    public TimeSpan AnswerDuration { get; }

    public GamePhaseStartMode RegularQuestionBuzzerStartMode { get; }

    public GamePhaseStartMode WagerQuestionAnswerTimerStartMode { get; }
}

public enum GamePhaseStartMode
{
    Automatic = 1,
    Manual = 2
}
