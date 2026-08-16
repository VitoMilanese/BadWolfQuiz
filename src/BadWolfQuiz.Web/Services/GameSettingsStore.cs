using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using BadWolfQuiz.Game.Runtime;
using Microsoft.Extensions.Options;

namespace BadWolfQuiz.Web.Services;

public sealed class GameSettingsStore(
    IWebHostEnvironment environment,
    IOptions<SiteDefaultsOptions> siteDefaults)
{
    public GameSettingsStore(IWebHostEnvironment environment)
        : this(environment, Options.Create(new SiteDefaultsOptions()))
    {
    }

    private readonly SemaphoreSlim _sync = new(1, 1);
    private readonly string _path = Path.Combine(
        environment.ContentRootPath,
        "App_Data",
        "game-settings.json");
    private readonly GameSessionSettings _defaultSettings = new(
        GameSessionSettings.Default.BuzzerDuration,
        GameSessionSettings.Default.AnswerDuration,
        GameSessionSettings.Default.RegularQuestionBuzzerStartMode,
        GameSessionSettings.Default.WagerQuestionAnswerTimerStartMode,
        GameSessionSettings.Default.AllowNegativeScoreFinalPlayers,
        siteThemeId: SiteThemeCatalog.Normalize(siteDefaults.Value.ThemeId));
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<GameSessionSettings> LoadAsync(
        string hostId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostId);
        await _sync.WaitAsync(cancellationToken);

        try
        {
            if (!File.Exists(_path))
            {
                return _defaultSettings;
            }

            await using var stream = File.OpenRead(_path);
            using var document = await JsonDocument.ParseAsync(
                stream,
                cancellationToken: cancellationToken);
            var root = document.RootElement;
            StoredGameSettings settings;
            if (root.TryGetProperty(nameof(StoredSettingsFile.Hosts), out _))
            {
                var file = root.Deserialize<StoredSettingsFile>(_jsonOptions)
                    ?? new StoredSettingsFile();
                settings = file.Hosts.GetValueOrDefault(hostId)
                    ?? file.LegacyDefault
                    ?? StoredGameSettings.From(_defaultSettings);
            }
            else
            {
                // Files written before per-host settings remain the initial
                // default for hosts that have not saved their own settings yet.
                settings = root.Deserialize<StoredGameSettings>(_jsonOptions)
                    ?? StoredGameSettings.From(_defaultSettings);
            }

            return settings.ToRuntimeSettings();
        }
        catch (JsonException)
        {
            return _defaultSettings;
        }
        finally
        {
            _sync.Release();
        }
    }

    public async Task SaveAsync(
        string hostId,
        GameSessionSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostId);
        ArgumentNullException.ThrowIfNull(settings);
        await _sync.WaitAsync(cancellationToken);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temporaryPath = _path + ".tmp";

            var file = await ReadFileForUpdateAsync(cancellationToken);
            file.Hosts[hostId] = StoredGameSettings.From(settings);

            await using (var stream = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    file,
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

    public Task InitializeHostAsync(
        string hostId,
        string? hostName,
        CancellationToken cancellationToken = default)
    {
        var settings = new GameSessionSettings(
            _defaultSettings.BuzzerDuration,
            _defaultSettings.AnswerDuration,
            _defaultSettings.RegularQuestionBuzzerStartMode,
            _defaultSettings.WagerQuestionAnswerTimerStartMode,
            _defaultSettings.AllowNegativeScoreFinalPlayers,
            !string.IsNullOrWhiteSpace(hostName),
            hostName,
            siteThemeId: _defaultSettings.SiteThemeId,
            customThemeColors: _defaultSettings.CustomThemeColors);
        return SaveAsync(hostId, settings, cancellationToken);
    }

    private async Task<StoredSettingsFile> ReadFileForUpdateAsync(
        CancellationToken cancellationToken)
    {
        if (!File.Exists(_path))
        {
            return new StoredSettingsFile();
        }

        try
        {
            await using var stream = File.OpenRead(_path);
            using var document = await JsonDocument.ParseAsync(
                stream,
                cancellationToken: cancellationToken);
            var root = document.RootElement;
            if (root.TryGetProperty(nameof(StoredSettingsFile.Hosts), out _))
            {
                return root.Deserialize<StoredSettingsFile>(_jsonOptions)
                    ?? new StoredSettingsFile();
            }

            return new StoredSettingsFile
            {
                LegacyDefault = root.Deserialize<StoredGameSettings>(_jsonOptions)
            };
        }
        catch (JsonException)
        {
            return new StoredSettingsFile();
        }
    }

    private sealed class StoredSettingsFile
    {
        public Dictionary<string, StoredGameSettings> Hosts { get; set; } =
            new(StringComparer.Ordinal);

        public StoredGameSettings? LegacyDefault { get; set; }
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
        public string? HostWebcamUrl { get; set; }
        public bool HostAvatarFrameEnabled { get; set; }
        public string? HostAvatarFrameId { get; set; }
        public byte[]? BrandLogoData { get; set; }
        public string? BrandLogoContentType { get; set; }
        public string SiteThemeId { get; set; } = SiteThemeCatalog.DefaultId;
        public SiteThemeColors CustomThemeColors { get; set; } = SiteThemeColors.Default;

        public bool AnswerRewardDecayEnabled { get; set; }

        public int AnswerRewardDecayStartAfterSeconds { get; set; } =
            GameSessionSettings.DefaultAnswerRewardDecayStartAfterSeconds;

        public int AnswerRewardDecayMinimumPercent { get; set; } =
            GameSessionSettings.DefaultAnswerRewardDecayMinimumPercent;

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
            SiteThemeCatalog.Normalize(CustomThemeColors),
            HostWebcamUrl,
            AnswerRewardDecayEnabled,
            AnswerRewardDecayStartAfterSeconds,
            AnswerRewardDecayMinimumPercent,
            HostAvatarFrameEnabled,
            HostAvatarFrameId);

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
            HostWebcamUrl = settings.HostWebcamUrl,
            HostAvatarFrameEnabled = settings.HostAvatarFrameEnabled,
            HostAvatarFrameId = settings.HostAvatarFrameId,
            BrandLogoData = settings.BrandLogoData,
            BrandLogoContentType = settings.BrandLogoContentType,
            SiteThemeId = SiteThemeCatalog.Normalize(settings.SiteThemeId),
            CustomThemeColors = SiteThemeCatalog.Normalize(settings.CustomThemeColors),
            AnswerRewardDecayEnabled = settings.AnswerRewardDecayEnabled,
            AnswerRewardDecayStartAfterSeconds =
                settings.AnswerRewardDecayStartAfterSeconds,
            AnswerRewardDecayMinimumPercent =
                settings.AnswerRewardDecayMinimumPercent
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
    [MaxLength(80)]
    public string? HostName { get; set; }
    public HostVisualSource HostVisualSource { get; set; }
    public string? HostAvatarId { get; set; }
    [MaxLength(2048)]
    public string? HostWebcamUrl { get; set; }
    public bool HostAvatarFrameEnabled { get; set; }
    public string? HostAvatarFrameId { get; set; }
    public string SiteThemeId { get; set; } = SiteThemeCatalog.DefaultId;
    public SiteThemeColors CustomThemeColors { get; set; } = SiteThemeColors.Default;

    public bool AnswerRewardDecayEnabled { get; set; }

    public int AnswerRewardDecayStartAfterSeconds { get; set; } =
        GameSessionSettings.DefaultAnswerRewardDecayStartAfterSeconds;

    public int AnswerRewardDecayMinimumPercent { get; set; } =
        GameSessionSettings.DefaultAnswerRewardDecayMinimumPercent;

    public bool IsValid =>
        AnswerRewardDecayStartAfterSeconds is >=
            GameSessionSettings.MinimumAnswerRewardDecayStartAfterSeconds and <=
            GameSessionSettings.MaximumAnswerRewardDecayStartAfterSeconds &&

        AnswerRewardDecayMinimumPercent is >=
            GameSessionSettings.MinimumAnswerRewardDecayMinimumPercent and <=
            GameSessionSettings.MaximumAnswerRewardDecayMinimumPercent &&

        (!AnswerRewardDecayEnabled ||
            AnswerRewardDecayStartAfterSeconds < AnswerDurationSeconds) &&

        BuzzerDurationSeconds is >= 1 and <= 3600 &&
        AnswerDurationSeconds is >= 1 and <= 3600 &&
        Enum.IsDefined(RegularQuestionBuzzerStartMode) &&
        Enum.IsDefined(WagerQuestionAnswerTimerStartMode) &&
        Enum.IsDefined(HostVisualSource) &&
        (HostVisualSource != BadWolfQuiz.Game.Runtime.HostVisualSource.WebcamUrl ||
            IsValidWebcamUrl(HostWebcamUrl)) &&
        (!HostAvatarFrameEnabled ||
            ContributorAvatarFrameCatalog.IsValid(HostAvatarFrameId)) &&
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
            SiteThemeCatalog.Normalize(customThemeColors ?? CustomThemeColors),
            HostWebcamUrl,
            AnswerRewardDecayEnabled,
            AnswerRewardDecayStartAfterSeconds,
            AnswerRewardDecayMinimumPercent,
            HostAvatarFrameEnabled,
            HostAvatarFrameId);
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
            HostWebcamUrl = settings.HostWebcamUrl,
            HostAvatarFrameEnabled = settings.HostAvatarFrameEnabled,
            HostAvatarFrameId = settings.HostAvatarFrameId,
            SiteThemeId = SiteThemeCatalog.Normalize(settings.SiteThemeId),
            CustomThemeColors = SiteThemeCatalog.Normalize(settings.CustomThemeColors),
            AnswerRewardDecayEnabled = settings.AnswerRewardDecayEnabled,
            AnswerRewardDecayStartAfterSeconds = settings.AnswerRewardDecayStartAfterSeconds,
            AnswerRewardDecayMinimumPercent = settings.AnswerRewardDecayMinimumPercent
        };
    }

    public static bool IsValidWebcamUrl(string? value) =>
        Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri) &&
        string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(uri.Host) &&
        string.IsNullOrEmpty(uri.UserInfo);
}
