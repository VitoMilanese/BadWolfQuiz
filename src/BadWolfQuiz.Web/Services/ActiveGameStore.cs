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
    GameSessionState SessionState,
    DateTimeOffset? SavedAtUtc = null,
    IReadOnlyList<QuestionOpenSequenceState>? QuestionOpenSequence = null,
    IReadOnlyList<PeerRatedAllPlayerReviewSnapshot>? PeerRatedAllPlayerReviews = null,
    AnonymousSharedWagerState? AnonymousSharedWager = null);

public sealed class ActiveGameStore
{
    private readonly string _path;
    private readonly string _blobDirectory;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ExternalByteArrayJsonConverter _binaryConverter;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly Dictionary<(string HostId, int SourceQuizId), DateTimeOffset>
        _deletionCutoffs = [];
    private IReadOnlyList<ActiveGameSnapshot> _snapshots;

    internal int WriteCount { get; private set; }

    public ActiveGameStore(IWebHostEnvironment environment)
    {
        _path = Path.Combine(
            environment.ContentRootPath,
            "App_Data",
            "active-games.json");
        _blobDirectory = Path.Combine(
            environment.ContentRootPath,
            "App_Data",
            "active-game-blobs");
        _binaryConverter = new ExternalByteArrayJsonConverter(_blobDirectory);
        _jsonOptions = new JsonSerializerOptions
        {
            Converters =
            {
                new QuizSnapshotJsonConverter(),
                _binaryConverter
            }
        };
        _snapshots = LoadFromDisk();
    }

    public IReadOnlyList<ActiveGameSnapshot> GetAll() => _snapshots;

    public ActiveGameSnapshot? Find(string hostId, int sourceQuizId) =>
        _snapshots.FirstOrDefault(snapshot =>
            snapshot.HostId == hostId &&
            snapshot.Quiz.SourceQuizId == sourceQuizId);

    public async Task ReplaceAsync(IReadOnlyList<ActiveGameSnapshot> snapshots)
    {
        await _gate.WaitAsync(CancellationToken.None);
        try
        {
            var accepted = ApplyDeletionCutoffs(snapshots);
            await WriteSnapshotsAsync(accepted);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> RemoveAsync(string hostId, int sourceQuizId)
    {
        await _gate.WaitAsync(CancellationToken.None);
        try
        {
            var key = (hostId, sourceQuizId);
            _deletionCutoffs[key] = DateTimeOffset.UtcNow;

            var remaining = _snapshots
                .Where(snapshot =>
                    !string.Equals(
                        snapshot.HostId,
                        hostId,
                        StringComparison.Ordinal) ||
                    snapshot.Quiz.SourceQuizId != sourceQuizId)
                .ToArray();

            if (remaining.Length == _snapshots.Count)
            {
                return false;
            }

            await WriteSnapshotsAsync(remaining);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private IReadOnlyList<ActiveGameSnapshot> ApplyDeletionCutoffs(
        IReadOnlyList<ActiveGameSnapshot> snapshots)
    {
        if (_deletionCutoffs.Count == 0)
        {
            return snapshots;
        }

        var accepted = snapshots.Where(snapshot =>
        {
            var key = (snapshot.HostId, snapshot.Quiz.SourceQuizId);
            return !_deletionCutoffs.TryGetValue(key, out var cutoff) ||
                snapshot.SessionState.CreatedAtUtc > cutoff;
        }).ToArray();

        foreach (var snapshot in accepted)
        {
            var key = (snapshot.HostId, snapshot.Quiz.SourceQuizId);
            if (_deletionCutoffs.TryGetValue(key, out var cutoff) &&
                snapshot.SessionState.CreatedAtUtc > cutoff)
            {
                _deletionCutoffs.Remove(key);
            }
        }

        return accepted;
    }

    private async Task WriteSnapshotsAsync(
        IReadOnlyList<ActiveGameSnapshot> snapshots)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temporaryPath = _path + ".tmp";
        _binaryConverter.BeginWrite();
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
                _jsonOptions,
                CancellationToken.None);
            await stream.FlushAsync(CancellationToken.None);
        }

        await ReplaceFileWithRetryAsync(temporaryPath);
        DeleteUnreferencedBlobs();
        _snapshots = snapshots;
        WriteCount++;
    }

    private void DeleteUnreferencedBlobs()
    {
        if (!Directory.Exists(_blobDirectory))
        {
            return;
        }

        foreach (var path in Directory.EnumerateFiles(_blobDirectory, "*.bin"))
        {
            var blobName = Path.GetFileNameWithoutExtension(path);
            if (!_binaryConverter.ReferencedBlobs.Contains(blobName))
            {
                try
                {
                    File.Delete(path);
                }
                catch (Exception exception) when (
                    exception is IOException or UnauthorizedAccessException)
                {
                }
            }
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
                _jsonOptions) ?? [];
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
