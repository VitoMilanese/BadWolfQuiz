using System.Text.Json;
using System.Text.Json.Serialization;
using BadWolfQuiz.Game.Runtime;

namespace BadWolfQuiz.Web.Services;

public sealed class GameSettingsStore(IWebHostEnvironment environment)
{
    private readonly SemaphoreSlim _sync = new(1, 1);
    private readonly string _path = Path.Combine(
        environment.ContentRootPath,
        "App_Data",
        "game-settings.json");
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<GameSessionSettings> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        await _sync.WaitAsync(cancellationToken);

        try
        {
            if (!File.Exists(_path))
            {
                return GameSessionSettings.Default;
            }

            await using var stream = File.OpenRead(_path);
            using var document = await JsonDocument.ParseAsync(
                stream,
                cancellationToken: cancellationToken);
            var settings = document.RootElement.Deserialize<StoredGameSettings>(
                _jsonOptions)
                ?? StoredGameSettings.From(GameSessionSettings.Default);

            return settings.ToRuntimeSettings();
        }
        catch (JsonException)
        {
            return GameSessionSettings.Default;
        }
        finally
        {
            _sync.Release();
        }
    }

    public async Task SaveAsync(
        GameSessionSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        await _sync.WaitAsync(cancellationToken);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temporaryPath = _path + ".tmp";

            await using (var stream = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    StoredGameSettings.From(settings),
                    _jsonOptions,
                    cancellationToken);
            }

            File.Move(temporaryPath, _path, true);
        }
        finally
        {
            _sync.Release();
        }
    }

    private sealed class StoredGameSettings
    {
        public StoredGameSettings()
        {
        }

        public TimeSpan BuzzerDuration { get; set; } = TimeSpan.FromSeconds(30);
        public TimeSpan AnswerDuration { get; set; } = TimeSpan.FromSeconds(10);
        public GamePhaseStartMode RegularQuestionBuzzerStartMode { get; set; } =
            GamePhaseStartMode.Manual;
        public GamePhaseStartMode WagerQuestionAnswerTimerStartMode { get; set; } =
            GamePhaseStartMode.Automatic;
        public bool AllowNegativeScoreFinalPlayers { get; set; } = true;
        public bool DisplayHostCard { get; set; }
        public string? HostName { get; set; }
        public HostVisualSource HostVisualSource { get; set; }
        public byte[]? HostImageData { get; set; }
        public string? HostImageContentType { get; set; }
        public string? HostAvatarId { get; set; }
        public byte[]? BrandLogoData { get; set; }
        public string? BrandLogoContentType { get; set; }
        public string SiteThemeId { get; set; } = SiteThemeCatalog.DefaultId;
        public SiteThemeColors CustomThemeColors { get; set; } = SiteThemeColors.Default;

        public GameSessionSettings ToRuntimeSettings() => new(
            BuzzerDuration,
            AnswerDuration,
            RegularQuestionBuzzerStartMode,
            WagerQuestionAnswerTimerStartMode,
            AllowNegativeScoreFinalPlayers,
            DisplayHostCard,
            HostName,
            HostVisualSource,
            HostImageData,
            HostImageContentType,
            HostAvatarId,
            BrandLogoData,
            BrandLogoContentType,
            SiteThemeCatalog.Normalize(SiteThemeId),
            SiteThemeCatalog.Normalize(CustomThemeColors));

        public static StoredGameSettings From(GameSessionSettings settings) => new()
        {
            BuzzerDuration = settings.BuzzerDuration,
            AnswerDuration = settings.AnswerDuration,
            RegularQuestionBuzzerStartMode = settings.RegularQuestionBuzzerStartMode,
            WagerQuestionAnswerTimerStartMode = settings.WagerQuestionAnswerTimerStartMode,
            AllowNegativeScoreFinalPlayers = settings.AllowNegativeScoreFinalPlayers,
            DisplayHostCard = settings.DisplayHostCard,
            HostName = settings.HostName,
            HostVisualSource = settings.HostVisualSource,
            HostImageData = settings.HostImageData,
            HostImageContentType = settings.HostImageContentType,
            HostAvatarId = settings.HostAvatarId,
            BrandLogoData = settings.BrandLogoData,
            BrandLogoContentType = settings.BrandLogoContentType,
            SiteThemeId = SiteThemeCatalog.Normalize(settings.SiteThemeId),
            CustomThemeColors = SiteThemeCatalog.Normalize(settings.CustomThemeColors)
        };
    }
}

public sealed class GameSettingsInput
{
    public int BuzzerDurationSeconds { get; set; } = 30;

    public int AnswerDurationSeconds { get; set; } = 10;

    public GamePhaseStartMode RegularQuestionBuzzerStartMode { get; set; } =
        GamePhaseStartMode.Manual;

    public GamePhaseStartMode WagerQuestionAnswerTimerStartMode { get; set; } =
        GamePhaseStartMode.Automatic;

    public bool AllowNegativeScoreFinalPlayers { get; set; } = true;
    public bool DisplayHostCard { get; set; }
    public string? HostName { get; set; }
    public HostVisualSource HostVisualSource { get; set; }
    public string? HostAvatarId { get; set; }
    public string SiteThemeId { get; set; } = SiteThemeCatalog.DefaultId;
    public SiteThemeColors CustomThemeColors { get; set; } = SiteThemeColors.Default;

    public bool IsValid =>
        BuzzerDurationSeconds is >= 1 and <= 3600 &&
        AnswerDurationSeconds is >= 1 and <= 3600 &&
        Enum.IsDefined(RegularQuestionBuzzerStartMode) &&
        Enum.IsDefined(WagerQuestionAnswerTimerStartMode) &&
        Enum.IsDefined(HostVisualSource) &&
        SiteThemeCatalog.IsValid(SiteThemeId) &&
        SiteThemeCatalog.AreValid(CustomThemeColors);

    public GameSessionSettings ToRuntimeSettings(
        byte[]? hostImageData = null,
        string? hostImageContentType = null,
        byte[]? brandLogoData = null,
        string? brandLogoContentType = null,
        string? siteThemeId = null,
        SiteThemeColors? customThemeColors = null)
    {
        if (!IsValid)
        {
            throw new ArgumentOutOfRangeException(
                nameof(GameSettingsInput),
                "Timer durations must be between 1 and 3600 seconds.");
        }

        return new GameSessionSettings(
            TimeSpan.FromSeconds(BuzzerDurationSeconds),
            TimeSpan.FromSeconds(AnswerDurationSeconds),
            RegularQuestionBuzzerStartMode,
            WagerQuestionAnswerTimerStartMode,
            AllowNegativeScoreFinalPlayers,
            !string.IsNullOrWhiteSpace(HostName) ||
                HostVisualSource != BadWolfQuiz.Game.Runtime.HostVisualSource.None,
            HostName,
            HostVisualSource,
            hostImageData,
            hostImageContentType,
            HostAvatarId,
            brandLogoData,
            brandLogoContentType,
            SiteThemeCatalog.Normalize(siteThemeId ?? SiteThemeId),
            SiteThemeCatalog.Normalize(customThemeColors ?? CustomThemeColors));
    }

    public static GameSettingsInput From(GameSessionSettings settings)
    {
        return new GameSettingsInput
        {
            BuzzerDurationSeconds = checked((int)settings.BuzzerDuration.TotalSeconds),
            AnswerDurationSeconds = checked((int)settings.AnswerDuration.TotalSeconds),
            RegularQuestionBuzzerStartMode =
                settings.RegularQuestionBuzzerStartMode,
            WagerQuestionAnswerTimerStartMode =
                settings.WagerQuestionAnswerTimerStartMode,
            AllowNegativeScoreFinalPlayers =
                settings.AllowNegativeScoreFinalPlayers,
            DisplayHostCard = settings.DisplayHostCard,
            HostName = settings.HostName,
            HostVisualSource = settings.HostVisualSource,
            HostAvatarId = settings.HostAvatarId,
            SiteThemeId = SiteThemeCatalog.Normalize(settings.SiteThemeId),
            CustomThemeColors = SiteThemeCatalog.Normalize(settings.CustomThemeColors)
        };
    }
}
