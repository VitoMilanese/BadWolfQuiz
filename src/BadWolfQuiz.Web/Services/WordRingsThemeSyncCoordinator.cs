using System.Collections.Concurrent;
using System.Text.Json;

namespace BadWolfQuiz.Web.Services;

public sealed record WordRingsThemeSnapshot(
    string ThemeId,
    IReadOnlyDictionary<string, string> Variables);

public sealed class WordRingsThemeSyncCoordinator
{
    private static readonly string[] AllowedVariables =
    [
        "--bg",
        "--panel",
        "--panel-2",
        "--line",
        "--text",
        "--muted",
        "--red",
        "--red-bright",
        "--gold",
        "--body-background",
        "--topbar-bg",
        "--panel-glass",
        "--panel-gradient-end",
        "--accent-shadow"
    ];

    private static readonly ConcurrentDictionary<string, Lazy<WordRingsThemeSyncCoordinator>> Instances =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly WordRingsRoomHostCoordinator _host;
    private readonly object _sync = new();
    private readonly Dictionary<string, WordRingsThemeSnapshot> _themes = new(StringComparer.OrdinalIgnoreCase);

    private WordRingsThemeSyncCoordinator(IWebHostEnvironment environment)
    {
        _host = WordRingsRoomHostCoordinator.Get(environment);
    }

    public static WordRingsThemeSyncCoordinator Get(IWebHostEnvironment environment)
    {
        var root = Path.GetFullPath(environment.ContentRootPath);
        return Instances.GetOrAdd(
            root,
            _ => new Lazy<WordRingsThemeSyncCoordinator>(
                () => new WordRingsThemeSyncCoordinator(environment),
                LazyThreadSafetyMode.ExecutionAndPublication)).Value;
    }

    public WordRingsThemeSnapshot? Synchronize(
        string? roomCode,
        string? playerToken,
        string? themeId,
        string? variablesJson)
    {
        var state = _host.GetRoomState(roomCode, playerToken);
        if (state.IsHost)
        {
            var snapshot = Normalize(themeId, variablesJson);
            lock (_sync)
            {
                _themes[state.RoomCode] = snapshot;
            }
            return snapshot;
        }

        lock (_sync)
        {
            return _themes.TryGetValue(state.RoomCode, out var snapshot)
                ? snapshot
                : null;
        }
    }

    private static WordRingsThemeSnapshot Normalize(string? themeId, string? variablesJson)
    {
        var normalizedThemeId = (themeId ?? string.Empty).Trim();
        if (normalizedThemeId.Length > 64)
        {
            normalizedThemeId = normalizedThemeId[..64];
        }

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(variablesJson))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(variablesJson);
                if (parsed is not null)
                {
                    foreach (var variable in AllowedVariables)
                    {
                        if (!parsed.TryGetValue(variable, out var value) || string.IsNullOrWhiteSpace(value)) continue;
                        var normalizedValue = value.Trim();
                        if (normalizedValue.Length > 1024) normalizedValue = normalizedValue[..1024];
                        result[variable] = normalizedValue;
                    }
                }
            }
            catch (JsonException)
            {
                // Invalid client theme payload is treated as an empty custom variable set.
            }
        }

        return new WordRingsThemeSnapshot(normalizedThemeId, result);
    }
}
