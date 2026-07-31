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
            var settings = document.RootElement.Deserialize<GameSessionSettings>(
                _jsonOptions)
                ?? GameSessionSettings.Default;

            return new GameSessionSettings(
                settings.BuzzerDuration,
                settings.AnswerDuration,
                settings.RegularQuestionBuzzerStartMode,
                settings.WagerQuestionAnswerTimerStartMode,
                allowNegativeScoreFinalPlayers: document.RootElement.TryGetProperty(
                    nameof(GameSessionSettings.AllowNegativeScoreFinalPlayers), out _)
                        ? settings.AllowNegativeScoreFinalPlayers
                        : true,
                settings.DisplayHostCard,
                settings.HostName,
                settings.HostVisualSource,
                settings.HostImageData,
                settings.HostImageContentType);
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
                    settings,
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

    public bool IsValid =>
        BuzzerDurationSeconds is >= 1 and <= 3600 &&
        AnswerDurationSeconds is >= 1 and <= 3600 &&
        Enum.IsDefined(RegularQuestionBuzzerStartMode) &&
        Enum.IsDefined(WagerQuestionAnswerTimerStartMode) &&
        Enum.IsDefined(HostVisualSource);

    public bool IsHostCardValid(bool hasImage) =>
        !DisplayHostCard ||
        (!string.IsNullOrWhiteSpace(HostName) &&
         HostVisualSource != BadWolfQuiz.Game.Runtime.HostVisualSource.None &&
         (HostVisualSource != BadWolfQuiz.Game.Runtime.HostVisualSource.Image || hasImage));

    public GameSessionSettings ToRuntimeSettings(
        byte[]? hostImageData = null,
        string? hostImageContentType = null)
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
            DisplayHostCard,
            HostName,
            HostVisualSource,
            hostImageData,
            hostImageContentType);
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
            HostVisualSource = settings.HostVisualSource
        };
    }
}
