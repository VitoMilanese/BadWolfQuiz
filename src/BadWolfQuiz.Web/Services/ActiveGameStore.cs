using System.Text.Json;
using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Game.Runtime;

namespace BadWolfQuiz.Web.Services;

public sealed record ActiveGameSnapshot(
    string PublicCode,
    string HostId,
    bool AllowsNewPlayers,
    QuizSnapshot Quiz,
    GameSessionSettings Settings,
    GameSessionState SessionState);

public sealed class ActiveGameStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IReadOnlyList<ActiveGameSnapshot> _snapshots;
    private string _serialized = "[]";

    public ActiveGameStore(IWebHostEnvironment environment)
    {
        _path = Path.Combine(
            environment.ContentRootPath,
            "App_Data",
            "active-games.json");
        _snapshots = LoadFromDisk();
    }

    public IReadOnlyList<ActiveGameSnapshot> GetAll() => _snapshots;

    public ActiveGameSnapshot? Find(string hostId, int sourceQuizId) =>
        _snapshots.FirstOrDefault(snapshot =>
            snapshot.HostId == hostId &&
            snapshot.Quiz.SourceQuizId == sourceQuizId);

    public async Task ReplaceAsync(IReadOnlyList<ActiveGameSnapshot> snapshots)
    {
        // Do not cancel while waiting for or performing the atomic replacement.
        // A canceled host token used to surface as TaskCanceledException from
        // this method and could make Visual Studio stop the application.
        await _gate.WaitAsync(CancellationToken.None);
        try
        {
            var serialized = JsonSerializer.Serialize(snapshots, JsonOptions);
            if (string.Equals(serialized, _serialized, StringComparison.Ordinal))
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temporaryPath = _path + ".tmp";
            await File.WriteAllTextAsync(
                temporaryPath,
                serialized,
                CancellationToken.None);
            File.Move(temporaryPath, _path, overwrite: true);
            _snapshots = snapshots;
            _serialized = serialized;
        }
        finally
        {
            _gate.Release();
        }
    }

    private IReadOnlyList<ActiveGameSnapshot> LoadFromDisk()
    {
        if (!File.Exists(_path))
        {
            return [];
        }

        try
        {
            _serialized = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<ActiveGameSnapshot[]>(
                _serialized,
                JsonOptions) ?? [];
        }
        catch (Exception exception) when (
            exception is JsonException or NotSupportedException)
        {
            return [];
        }
    }
}
