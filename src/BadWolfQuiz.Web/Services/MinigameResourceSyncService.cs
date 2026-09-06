using System.Data.Common;
using System.Globalization;
using BadWolfQuiz.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Services;

public sealed class MinigameResourceSyncService
{
    private const int MaximumGameNameLength = 200;
    private static readonly HashSet<string> SupportedImageExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".png",
            ".jpg",
            ".jpeg",
            ".webp",
            ".gif"
        };

    private readonly IDbContextFactory<QuizDbContext> _dbFactory;
    private readonly string _resourceRootPath;

    public MinigameResourceSyncService(
        IDbContextFactory<QuizDbContext> dbFactory,
        string? resourceRootPath = null)
    {
        _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        _resourceRootPath = string.IsNullOrWhiteSpace(resourceRootPath)
            ? ResolveResourceRootPath()
            : Path.GetFullPath(resourceRootPath);
    }

    public string ResourceRootPath => _resourceRootPath;

    public async Task<MinigameResourceSyncResult> SynchronizeAsync(
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_resourceRootPath))
        {
            return MinigameResourceSyncResult.Failed(
                MinigameResourceSyncError.FolderNotFound);
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.OpenConnectionAsync(cancellationToken);
        var connection = db.Database.GetDbConnection();
        var questionIds = await ReadQuestionIdsAsync(connection, cancellationToken);

        var scan = await ScanResourceGamesAsync(questionIds.Count, cancellationToken);
        if (!scan.Success)
        {
            return MinigameResourceSyncResult.Failed(
                scan.Error,
                scan.ErrorItem,
                scan.InvalidLineNumber,
                scan.ExpectedAnswerCount,
                scan.ActualAnswerCount);
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var existingGames = await ReadGamesAsync(connection, transaction, cancellationToken);
            var existingByName = existingGames.ToDictionary(
                game => game.Name,
                StringComparer.OrdinalIgnoreCase);
            var addedCount = 0;
            var updatedCount = 0;

            foreach (var resourceGame in scan.Games)
            {
                int gameId;
                if (existingByName.TryGetValue(resourceGame.Name, out var existing))
                {
                    gameId = existing.Id;
                    await UpdateGameImageAsync(
                        connection,
                        transaction,
                        gameId,
                        resourceGame.ImageData,
                        resourceGame.ContentType,
                        cancellationToken);
                    updatedCount++;
                }
                else
                {
                    gameId = await InsertGameAsync(
                        connection,
                        transaction,
                        resourceGame.Name,
                        resourceGame.ImageData,
                        resourceGame.ContentType,
                        cancellationToken);
                    existingByName[resourceGame.Name] = new MinigameResourceDatabaseGame(
                        gameId,
                        resourceGame.Name);
                    addedCount++;
                }

                if (resourceGame.Answers is not null)
                {
                    await ReplaceAnswersAsync(
                        connection,
                        transaction,
                        gameId,
                        questionIds,
                        resourceGame.Answers,
                        cancellationToken);
                }
            }

            var resourceNames = scan.Games
                .Select(game => game.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var finalGames = await ReadGamesAsync(connection, transaction, cancellationToken);
            var missingGames = finalGames
                .Where(game => !resourceNames.Contains(game.Name))
                .OrderBy(game => game.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(game => game.Id)
                .Select(game => new MinigameResourceMissingGame(game.Id, game.Name))
                .ToArray();

            await transaction.CommitAsync(cancellationToken);
            return MinigameResourceSyncResult.Succeeded(
                addedCount,
                updatedCount,
                missingGames);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<MinigameResourceDeleteResult> DeleteMissingGamesAsync(
        IReadOnlyCollection<int>? gameIds,
        CancellationToken cancellationToken = default)
    {
        if (gameIds is null || gameIds.Count == 0)
        {
            return MinigameResourceDeleteResult.Succeeded(0);
        }
        if (!Directory.Exists(_resourceRootPath))
        {
            return MinigameResourceDeleteResult.Failed(
                MinigameResourceSyncError.FolderNotFound);
        }

        var resourceNamesResult = ScanResourceGameNames();
        if (!resourceNamesResult.Success)
        {
            return MinigameResourceDeleteResult.Failed(
                resourceNamesResult.Error,
                resourceNamesResult.ErrorItem);
        }

        var selectedIds = gameIds.Where(id => id > 0).ToHashSet();
        if (selectedIds.Count == 0)
        {
            return MinigameResourceDeleteResult.Succeeded(0);
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.OpenConnectionAsync(cancellationToken);
        var connection = db.Database.GetDbConnection();
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var games = await ReadGamesAsync(connection, transaction, cancellationToken);
            var deletableIds = games
                .Where(game => selectedIds.Contains(game.Id))
                .Where(game => !resourceNamesResult.Names.Contains(game.Name))
                .Select(game => game.Id)
                .ToArray();

            var deletedCount = 0;
            foreach (var gameId in deletableIds)
            {
                await using (var deleteAnswers = connection.CreateCommand())
                {
                    deleteAnswers.Transaction = transaction;
                    deleteAnswers.CommandText =
                        "DELETE FROM MinigameCatalogAnswers WHERE GameId = $gameId;";
                    AddParameter(deleteAnswers, "$gameId", gameId);
                    await deleteAnswers.ExecuteNonQueryAsync(cancellationToken);
                }

                await using var deleteGame = connection.CreateCommand();
                deleteGame.Transaction = transaction;
                deleteGame.CommandText =
                    "DELETE FROM MinigameCatalogGames WHERE Id = $gameId;";
                AddParameter(deleteGame, "$gameId", gameId);
                deletedCount += await deleteGame.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return MinigameResourceDeleteResult.Succeeded(deletedCount);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task<MinigameResourceScanResult> ScanResourceGamesAsync(
        int questionCount,
        CancellationToken cancellationToken)
    {
        var nameScan = ScanResourceGameNames();
        if (!nameScan.Success)
        {
            return MinigameResourceScanResult.Failed(
                nameScan.Error,
                nameScan.ErrorItem);
        }

        var answerFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in Directory.EnumerateFiles(_resourceRootPath, "*.txt", SearchOption.TopDirectoryOnly))
        {
            var fileName = Path.GetFileName(path);
            if (string.Equals(fileName, "questions.txt", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var name = Path.GetFileNameWithoutExtension(path).Trim();
            if (!answerFiles.TryAdd(name, path))
            {
                return MinigameResourceScanResult.Failed(
                    MinigameResourceSyncError.DuplicateAnswerFile,
                    name);
            }
        }

        var games = new List<MinigameResourceGame>(nameScan.Paths.Count);
        foreach (var pair in nameScan.Paths.OrderBy(
                     item => item.Key,
                     StringComparer.OrdinalIgnoreCase))
        {
            var name = pair.Key;
            var imagePath = pair.Value;
            byte[] imageData;
            try
            {
                imageData = await File.ReadAllBytesAsync(imagePath, cancellationToken);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return MinigameResourceScanResult.Failed(
                    MinigameResourceSyncError.ResourceReadFailed,
                    Path.GetFileName(imagePath));
            }

            IReadOnlyList<bool?>? answers = null;
            if (answerFiles.TryGetValue(name, out var answerPath))
            {
                string answerContent;
                try
                {
                    answerContent = await File.ReadAllTextAsync(answerPath, cancellationToken);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    return MinigameResourceScanResult.Failed(
                        MinigameResourceSyncError.ResourceReadFailed,
                        Path.GetFileName(answerPath));
                }

                var parsed = MinigameAnswerFileParser.Parse(answerContent, questionCount);
                if (!parsed.Success)
                {
                    return MinigameResourceScanResult.Failed(
                        MinigameResourceSyncError.InvalidAnswerFile,
                        Path.GetFileName(answerPath),
                        parsed.InvalidLineNumber,
                        parsed.ExpectedCount,
                        parsed.ActualCount);
                }
                answers = parsed.Answers;
            }

            games.Add(new MinigameResourceGame(
                name,
                imageData,
                GetContentType(imagePath),
                answers));
        }

        return MinigameResourceScanResult.Succeeded(games);
    }

    private MinigameResourceNameScanResult ScanResourceGameNames()
    {
        var paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in Directory.EnumerateFiles(_resourceRootPath, "*", SearchOption.TopDirectoryOnly)
                     .Where(path => SupportedImageExtensions.Contains(Path.GetExtension(path))))
        {
            var name = Path.GetFileNameWithoutExtension(path).Trim();
            if (name.Length == 0 || name.Length > MaximumGameNameLength)
            {
                return MinigameResourceNameScanResult.Failed(
                    MinigameResourceSyncError.InvalidGameName,
                    Path.GetFileName(path));
            }
            if (!paths.TryAdd(name, path))
            {
                return MinigameResourceNameScanResult.Failed(
                    MinigameResourceSyncError.DuplicateGameName,
                    name);
            }
        }

        return MinigameResourceNameScanResult.Succeeded(paths);
    }

    private static async Task<IReadOnlyList<int>> ReadQuestionIdsAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        var questionIds = new List<int>();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT Id FROM MinigameCatalogQuestions ORDER BY SortOrder, Id;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            questionIds.Add(reader.GetInt32(0));
        }
        return questionIds;
    }

    private static async Task<IReadOnlyList<MinigameResourceDatabaseGame>> ReadGamesAsync(
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken)
    {
        var games = new List<MinigameResourceDatabaseGame>();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "SELECT Id, Name FROM MinigameCatalogGames ORDER BY Name COLLATE NOCASE, Id;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            games.Add(new MinigameResourceDatabaseGame(
                reader.GetInt32(0),
                reader.GetString(1)));
        }
        return games;
    }

    private static async Task UpdateGameImageAsync(
        DbConnection connection,
        DbTransaction transaction,
        int gameId,
        byte[] imageData,
        string contentType,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            UPDATE MinigameCatalogGames
            SET ImageData = $imageData,
                ImageContentType = $contentType
            WHERE Id = $id;
            """;
        AddParameter(command, "$imageData", imageData);
        AddParameter(command, "$contentType", contentType);
        AddParameter(command, "$id", gameId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<int> InsertGameAsync(
        DbConnection connection,
        DbTransaction transaction,
        string name,
        byte[] imageData,
        string contentType,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO MinigameCatalogGames (Name, ImageData, ImageContentType)
            VALUES ($name, $imageData, $contentType);
            SELECT last_insert_rowid();
            """;
        AddParameter(command, "$name", name);
        AddParameter(command, "$imageData", imageData);
        AddParameter(command, "$contentType", contentType);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    private static async Task ReplaceAnswersAsync(
        DbConnection connection,
        DbTransaction transaction,
        int gameId,
        IReadOnlyList<int> questionIds,
        IReadOnlyList<bool?> answers,
        CancellationToken cancellationToken)
    {
        await using (var delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText =
                "DELETE FROM MinigameCatalogAnswers WHERE GameId = $gameId;";
            AddParameter(delete, "$gameId", gameId);
            await delete.ExecuteNonQueryAsync(cancellationToken);
        }

        for (var index = 0; index < questionIds.Count; index++)
        {
            if (answers[index] is not bool answer)
            {
                continue;
            }

            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText =
                """
                INSERT INTO MinigameCatalogAnswers (GameId, QuestionId, AnswerYes)
                VALUES ($gameId, $questionId, $answerYes);
                """;
            AddParameter(insert, "$gameId", gameId);
            AddParameter(insert, "$questionId", questionIds[index]);
            AddParameter(insert, "$answerYes", answer ? 1 : 0);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static string ResolveResourceRootPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Resources", "Minigames", "GameCards"),
            Path.Combine(Directory.GetCurrentDirectory(), "Resources", "Minigames", "GameCards")
        };

        return candidates.FirstOrDefault(Directory.Exists) ?? candidates[0];
    }

    private static string GetContentType(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "application/octet-stream"
        };

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private sealed record MinigameResourceGame(
        string Name,
        byte[] ImageData,
        string ContentType,
        IReadOnlyList<bool?>? Answers);

    private sealed record MinigameResourceDatabaseGame(int Id, string Name);

    private sealed record MinigameResourceScanResult(
        bool Success,
        IReadOnlyList<MinigameResourceGame> Games,
        MinigameResourceSyncError Error,
        string? ErrorItem,
        int InvalidLineNumber,
        int ExpectedAnswerCount,
        int ActualAnswerCount)
    {
        public static MinigameResourceScanResult Succeeded(
            IReadOnlyList<MinigameResourceGame> games) =>
            new(true, games, MinigameResourceSyncError.None, null, 0, 0, 0);

        public static MinigameResourceScanResult Failed(
            MinigameResourceSyncError error,
            string? errorItem = null,
            int invalidLineNumber = 0,
            int expectedAnswerCount = 0,
            int actualAnswerCount = 0) =>
            new(false, [], error, errorItem, invalidLineNumber, expectedAnswerCount, actualAnswerCount);
    }

    private sealed record MinigameResourceNameScanResult(
        bool Success,
        IReadOnlyDictionary<string, string> Paths,
        IReadOnlySet<string> Names,
        MinigameResourceSyncError Error,
        string? ErrorItem)
    {
        public static MinigameResourceNameScanResult Succeeded(
            IReadOnlyDictionary<string, string> paths) =>
            new(
                true,
                paths,
                paths.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase),
                MinigameResourceSyncError.None,
                null);

        public static MinigameResourceNameScanResult Failed(
            MinigameResourceSyncError error,
            string? errorItem) =>
            new(
                false,
                new Dictionary<string, string>(),
                new HashSet<string>(),
                error,
                errorItem);
    }
}

public enum MinigameResourceSyncError
{
    None,
    FolderNotFound,
    InvalidGameName,
    DuplicateGameName,
    DuplicateAnswerFile,
    InvalidAnswerFile,
    ResourceReadFailed
}

public sealed record MinigameResourceMissingGame(int Id, string Name);

public sealed record MinigameResourceSyncResult(
    bool Success,
    int AddedCount,
    int UpdatedCount,
    IReadOnlyList<MinigameResourceMissingGame> MissingGames,
    MinigameResourceSyncError Error,
    string? ErrorItem,
    int InvalidLineNumber,
    int ExpectedAnswerCount,
    int ActualAnswerCount)
{
    public static MinigameResourceSyncResult Succeeded(
        int addedCount,
        int updatedCount,
        IReadOnlyList<MinigameResourceMissingGame> missingGames) =>
        new(
            true,
            addedCount,
            updatedCount,
            missingGames,
            MinigameResourceSyncError.None,
            null,
            0,
            0,
            0);

    public static MinigameResourceSyncResult Failed(
        MinigameResourceSyncError error,
        string? errorItem = null,
        int invalidLineNumber = 0,
        int expectedAnswerCount = 0,
        int actualAnswerCount = 0) =>
        new(
            false,
            0,
            0,
            [],
            error,
            errorItem,
            invalidLineNumber,
            expectedAnswerCount,
            actualAnswerCount);
}

public sealed record MinigameResourceDeleteResult(
    bool Success,
    int DeletedCount,
    MinigameResourceSyncError Error,
    string? ErrorItem)
{
    public static MinigameResourceDeleteResult Succeeded(int deletedCount) =>
        new(true, deletedCount, MinigameResourceSyncError.None, null);

    public static MinigameResourceDeleteResult Failed(
        MinigameResourceSyncError error,
        string? errorItem = null) =>
        new(false, 0, error, errorItem);
}
