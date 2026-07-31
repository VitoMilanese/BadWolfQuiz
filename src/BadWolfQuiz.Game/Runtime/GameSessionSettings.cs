namespace BadWolfQuiz.Game.Runtime;

public sealed record GameSessionSettings
{
    public static GameSessionSettings Default { get; } = new(
        GameSession.DefaultBuzzerDuration,
        GameSession.DefaultAnswerDuration,
        GamePhaseStartMode.Manual,
        GamePhaseStartMode.Automatic,
        allowNegativeScoreFinalPlayers: true);

    public GameSessionSettings(
        TimeSpan buzzerDuration,
        TimeSpan answerDuration,
        GamePhaseStartMode regularQuestionBuzzerStartMode,
        GamePhaseStartMode wagerQuestionAnswerTimerStartMode,
        bool allowNegativeScoreFinalPlayers = true,
        bool displayHostCard = false,
        string? hostName = null,
        HostVisualSource hostVisualSource = HostVisualSource.None,
        byte[]? hostImageData = null,
        string? hostImageContentType = null,
        string? hostAvatarId = null)
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
        AllowNegativeScoreFinalPlayers = allowNegativeScoreFinalPlayers;
        DisplayHostCard = displayHostCard;
        HostName = hostName?.Trim();
        HostVisualSource = hostVisualSource;
        HostImageData = hostImageData;
        HostImageContentType = hostImageContentType;
        HostAvatarId = hostAvatarId;
    }

    public TimeSpan BuzzerDuration { get; }

    public TimeSpan AnswerDuration { get; }

    public GamePhaseStartMode RegularQuestionBuzzerStartMode { get; }

    public GamePhaseStartMode WagerQuestionAnswerTimerStartMode { get; }

    public bool AllowNegativeScoreFinalPlayers { get; }

    public bool DisplayHostCard { get; }
    public string? HostName { get; }
    public HostVisualSource HostVisualSource { get; }
    public byte[]? HostImageData { get; }
    public string? HostImageContentType { get; }
    public string? HostAvatarId { get; }

    public bool HasHostCard =>
        !string.IsNullOrWhiteSpace(HostName) ||
        HostVisualSource != BadWolfQuiz.Game.Runtime.HostVisualSource.None;
}

public enum HostVisualSource
{
    None = 0,
    Image = 1,
    Webcam = 2,
    Avatar = 3
}

public enum GamePhaseStartMode
{
    Automatic = 1,
    Manual = 2
}
