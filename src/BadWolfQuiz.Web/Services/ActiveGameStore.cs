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
        Converters = { new QuizSnapshotJsonConverter() }
    };

    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IReadOnlyList<ActiveGameSnapshot> _snapshots;

    internal int WriteCount { get; private set; }

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
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temporaryPath = _path + ".tmp";
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 64 * 1024,
                useAsync: true))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    snapshots,
                    JsonOptions,
                    CancellationToken.None);
                await stream.FlushAsync(CancellationToken.None);
            }

            await ReplaceFileWithRetryAsync(temporaryPath);
            _snapshots = snapshots;
            WriteCount++;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task ReplaceFileWithRetryAsync(string temporaryPath)
    {
        const int maxAttempts = 6;
        var retryDelay = TimeSpan.FromMilliseconds(50);

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                File.Move(temporaryPath, _path, overwrite: true);
                return;
            }
            catch (Exception exception) when (
                attempt < maxAttempts &&
                exception is IOException or UnauthorizedAccessException)
            {
                // Windows, antivirus software, or an editor may briefly hold the
                // destination without delete sharing. Preserve the atomic move
                // and retry instead of terminating the persistence worker.
                await Task.Delay(retryDelay, CancellationToken.None);
                retryDelay *= 2;
            }
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
            using var stream = File.OpenRead(_path);
            return JsonSerializer.Deserialize<ActiveGameSnapshot[]>(
                stream,
                JsonOptions) ?? [];
        }
        catch (Exception exception) when (
            exception is JsonException or
                NotSupportedException or
                InvalidOperationException or
                ArgumentException)
        {
            return [];
        }
    }
}
