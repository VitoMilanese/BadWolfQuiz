namespace BadWolfQuiz.Game.Runtime;

public sealed record GameSessionSettings
{
    public const int DefaultAnswerRewardDecayStartAfterSeconds = 10;
    public const int MinimumAnswerRewardDecayStartAfterSeconds = 5;
    public const int MaximumAnswerRewardDecayStartAfterSeconds = 45;

    public const int DefaultAnswerRewardDecayMinimumPercent = 25;
    public const int MinimumAnswerRewardDecayMinimumPercent = 10;
    public const int MaximumAnswerRewardDecayMinimumPercent = 90;

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
        string? hostAvatarId = null,
        byte[]? brandLogoData = null,
        string? brandLogoContentType = null,
        string siteThemeId = "classic-wolf",
        SiteThemeColors? customThemeColors = null,
        string? hostWebcamUrl = null,
        bool answerRewardDecayEnabled = false,
        int answerRewardDecayStartAfterSeconds =
            DefaultAnswerRewardDecayStartAfterSeconds,
        int answerRewardDecayMinimumPercent =
            DefaultAnswerRewardDecayMinimumPercent,
        bool hostAvatarFrameEnabled = false,
        string? hostAvatarFrameId = null)
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

        if (answerRewardDecayStartAfterSeconds is
            < MinimumAnswerRewardDecayStartAfterSeconds or
            > MaximumAnswerRewardDecayStartAfterSeconds)
        {
            throw new ArgumentOutOfRangeException(
                nameof(answerRewardDecayStartAfterSeconds));
        }

        if (answerRewardDecayMinimumPercent is
            < MinimumAnswerRewardDecayMinimumPercent or
            > MaximumAnswerRewardDecayMinimumPercent)
        {
            throw new ArgumentOutOfRangeException(
                nameof(answerRewardDecayMinimumPercent));
        }

        if (answerRewardDecayEnabled &&
            answerRewardDecayStartAfterSeconds >= answerDuration.TotalSeconds)
        {
            throw new ArgumentOutOfRangeException(
                nameof(answerRewardDecayStartAfterSeconds),
                "Reward decay must start before the answer timer expires.");
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
        HostWebcamUrl = hostWebcamUrl?.Trim();
        BrandLogoData = brandLogoData;
        BrandLogoContentType = brandLogoContentType;
        SiteThemeId = siteThemeId;
        CustomThemeColors = customThemeColors ?? SiteThemeColors.Default;
        AnswerRewardDecayEnabled = answerRewardDecayEnabled;
        AnswerRewardDecayStartAfterSeconds = answerRewardDecayStartAfterSeconds;
        AnswerRewardDecayMinimumPercent = answerRewardDecayMinimumPercent;
        HostAvatarFrameEnabled = hostAvatarFrameEnabled;
        HostAvatarFrameId = hostAvatarFrameId?.Trim();
    }

    public TimeSpan BuzzerDuration { get; }

    public TimeSpan AnswerDuration { get; }

    public GamePhaseStartMode RegularQuestionBuzzerStartMode { get; }

    public GamePhaseStartMode WagerQuestionAnswerTimerStartMode { get; }

    public bool AllowNegativeScoreFinalPlayers { get; }

    public bool AnswerRewardDecayEnabled { get; }

    public int AnswerRewardDecayStartAfterSeconds { get; }

    public int AnswerRewardDecayMinimumPercent { get; }

    public bool DisplayHostCard { get; }
    public string? HostName { get; }
    public HostVisualSource HostVisualSource { get; }
    public byte[]? HostImageData { get; }
    public string? HostImageContentType { get; }
    public string? HostAvatarId { get; }
    public string? HostWebcamUrl { get; }
    public bool HostAvatarFrameEnabled { get; }
    public string? HostAvatarFrameId { get; }
    public byte[]? BrandLogoData { get; }
    public string? BrandLogoContentType { get; }
    public string SiteThemeId { get; }
    public SiteThemeColors CustomThemeColors { get; }

    public bool HasHostCard =>
        !string.IsNullOrWhiteSpace(HostName) ||
        HostVisualSource != BadWolfQuiz.Game.Runtime.HostVisualSource.None;
}

public sealed record SiteThemeColors
{
    public SiteThemeColors()
    {
    }

    public SiteThemeColors(
        string background,
        string panel,
        string panelSecondary,
        string text,
        string mutedText,
        string accent,
        string accentBright,
        string highlight)
    {
        Background = background;
        Panel = panel;
        PanelSecondary = panelSecondary;
        Text = text;
        MutedText = mutedText;
        Accent = accent;
        AccentBright = accentBright;
        Highlight = highlight;
    }

    public string Background { get; set; } = "#080b12";
    public string Panel { get; set; } = "#121826";
    public string PanelSecondary { get; set; } = "#1c2638";
    public string Text { get; set; } = "#f4f7fb";
    public string MutedText { get; set; } = "#9eabc0";
    public string Accent { get; set; } = "#2563eb";
    public string AccentBright { get; set; } = "#60a5fa";
    public string Highlight { get; set; } = "#f59e0b";

    public static SiteThemeColors Default => new();
}

public enum HostVisualSource
{
    None = 0,
    Image = 1,
    Webcam = 2,
    Avatar = 3,
    WebcamUrl = 4
}

public enum GamePhaseStartMode
{
    Automatic = 1,
    Manual = 2
}
