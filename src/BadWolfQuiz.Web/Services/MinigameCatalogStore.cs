using System.Data.Common;
using System.Globalization;
using BadWolfQuiz.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Services;

public sealed class MinigameCatalogStore
{
    private const int MaximumGameNameLength = 200;
    private const int MaximumQuestionLength = 500;
    private static readonly SemaphoreSlim InitializationGate = new(1, 1);
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
    private readonly int _defaultCardCount;
    private readonly string? _legacyRootPath;

    public MinigameCatalogStore(
        IDbContextFactory<QuizDbContext> dbFactory,
        int defaultCardCount,
        string? legacyRootPath = null)
    {
        _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(defaultCardCount);
        _defaultCardCount = defaultCardCount;
        _legacyRootPath = string.IsNullOrWhiteSpace(legacyRootPath)
            ? ResolveLegacyRootPath()
            : Path.GetFullPath(legacyRootPath);
    }

    public int DefaultCardCount => _defaultCardCount;

    public async Task<MinigameCatalogCounts> GetCountsAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.OpenConnectionAsync(cancellationToken);
        var connection = db.Database.GetDbConnection();

        return new MinigameCatalogCounts(
            await ExecuteCountAsync(connection, "MinigameCatalogGames", cancellationToken),
            await ExecuteCountAsync(connection, "MinigameCatalogQuestions", cancellationToken));
    }

    public async Task<IReadOnlyList<MinigameCardDescriptor>> GenerateCardsAsync(
        int cardCount,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cardCount);
        await EnsureInitializedAsync(cancellationToken);
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.OpenConnectionAsync(cancellationToken);
        var connection = db.Database.GetDbConnection();

        var cards = new List<MinigameCardDescriptor>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                "SELECT Id, Name FROM MinigameCatalogGames ORDER BY Name COLLATE NOCASE, Id;";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                cards.Add(new MinigameCardDescriptor(
                    reader.GetInt64(0).ToString(CultureInfo.InvariantCulture),
                    reader.GetString(1)));
            }
        }

        if (cardCount > cards.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cardCount),
                cardCount,
                "The requested card count exceeds the available game count.");
        }

        Shuffle(cards);
        return cards.Take(cardCount).ToArray();
    }

    public async Task<IReadOnlyList<string>> GetQuestionsAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.OpenConnectionAsync(cancellationToken);
        var connection = db.Database.GetDbConnection();
        var questions = new List<string>();

        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT Text FROM MinigameCatalogQuestions ORDER BY SortOrder, Id;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            questions.Add(reader.GetString(0));
        }

        return questions;
    }

    public async Task<MinigameCatalogImage?> GetGameImageAsync(
        string? gameKey,
        CancellationToken cancellationToken = default)
    {
        if (!int.TryParse(gameKey, NumberStyles.None, CultureInfo.InvariantCulture, out var gameId) ||
            gameId <= 0)
        {
            return null;
        }

        return await GetGameImageAsync(gameId, cancellationToken);
    }

    public async Task<MinigameCatalogImage?> GetGameImageAsync(
        int gameId,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.OpenConnectionAsync(cancellationToken);
        var connection = db.Database.GetDbConnection();

        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT ImageData, ImageContentType FROM MinigameCatalogGames WHERE Id = $id;";
        AddParameter(command, "$id", gameId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new MinigameCatalogImage(
            (byte[])reader.GetValue(0),
            reader.GetString(1));
    }

    public async Task<IReadOnlyList<MinigameCatalogGameItem>> GetGamesAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.OpenConnectionAsync(cancellationToken);
        var connection = db.Database.GetDbConnection();
        var games = new List<MinigameCatalogGameItem>();

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT g.Id,
                   g.Name,
                   g.ImageContentType,
                   length(g.ImageData),
                   COUNT(a.QuestionId)
            FROM MinigameCatalogGames g
            LEFT JOIN MinigameCatalogAnswers a ON a.GameId = g.Id
            GROUP BY g.Id, g.Name, g.ImageContentType, g.ImageData
            ORDER BY g.Name COLLATE NOCASE, g.Id;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            games.Add(new MinigameCatalogGameItem(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt64(3),
                reader.GetInt32(4)));
        }

        return games;
    }

    public async Task<IReadOnlyList<MinigameCatalogQuestionItem>> GetQuestionItemsAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.OpenConnectionAsync(cancellationToken);
        var connection = db.Database.GetDbConnection();
        return await ReadQuestionItemsAsync(connection, cancellationToken);
    }

    public async Task<MinigameCatalogGameItem?> GetGameAsync(
        int gameId,
        CancellationToken cancellationToken = default)
    {
        var games = await GetGamesAsync(cancellationToken);
        return games.FirstOrDefault(game => game.Id == gameId);
    }

    public async Task<IReadOnlyList<MinigameCatalogAnswerItem>> GetAnswerItemsAsync(
        int gameId,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.OpenConnectionAsync(cancellationToken);
        var connection = db.Database.GetDbConnection();
        var rows = new List<MinigameCatalogAnswerItem>();

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT q.Id,
                   q.SortOrder,
                   q.Text,
                   a.AnswerYes
            FROM MinigameCatalogQuestions q
            LEFT JOIN MinigameCatalogAnswers a
              ON a.QuestionId = q.Id AND a.GameId = $gameId
            ORDER BY q.SortOrder, q.Id;
            """;
        AddParameter(command, "$gameId", gameId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new MinigameCatalogAnswerItem(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetInt32(3) != 0));
        }

        return rows;
    }

    public async Task<MinigameCatalogMutationResult> CreateGameAsync(
        string? name,
        byte[]? imageData,
        string? imageContentType,
        CancellationToken cancellationToken = default)
    {
        var normalizedName = NormalizeName(name);
        if (normalizedName is null || imageData is null || imageData.Length == 0 ||
            !IsSupportedContentType(imageContentType))
        {
            return MinigameCatalogMutationResult.Invalid;
        }

        await EnsureInitializedAsync(cancellationToken);
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.OpenConnectionAsync(cancellationToken);
        var connection = db.Database.GetDbConnection();
        if (await GameNameExistsAsync(connection, normalizedName, null, cancellationToken))
        {
            return MinigameCatalogMutationResult.Duplicate;
        }

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO MinigameCatalogGames (Name, ImageData, ImageContentType)
            VALUES ($name, $imageData, $contentType);
            """;
        AddParameter(command, "$name", normalizedName);
        AddParameter(command, "$imageData", imageData);
        AddParameter(command, "$contentType", imageContentType!);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return MinigameCatalogMutationResult.Success;
    }

    public async Task<MinigameCatalogMutationResult> UpdateGameAsync(
        int gameId,
        string? name,
        byte[]? imageData,
        string? imageContentType,
        CancellationToken cancellationToken = default)
    {
        var normalizedName = NormalizeName(name);
        if (normalizedName is null ||
            (imageData is not null &&
             (imageData.Length == 0 || !IsSupportedContentType(imageContentType))))
        {
            return MinigameCatalogMutationResult.Invalid;
        }

        await EnsureInitializedAsync(cancellationToken);
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.OpenConnectionAsync(cancellationToken);
        var connection = db.Database.GetDbConnection();
        if (!await GameExistsAsync(connection, gameId, cancellationToken))
        {
            return MinigameCatalogMutationResult.NotFound;
        }
        if (await GameNameExistsAsync(connection, normalizedName, gameId, cancellationToken))
        {
            return MinigameCatalogMutationResult.Duplicate;
        }

        await using var command = connection.CreateCommand();
        if (imageData is null)
        {
            command.CommandText =
                "UPDATE MinigameCatalogGames SET Name = $name WHERE Id = $id;";
        }
        else
        {
            command.CommandText =
                """
                UPDATE MinigameCatalogGames
                SET Name = $name,
                    ImageData = $imageData,
                    ImageContentType = $contentType
                WHERE Id = $id;
                """;
            AddParameter(command, "$imageData", imageData);
            AddParameter(command, "$contentType", imageContentType!);
        }
        AddParameter(command, "$name", normalizedName);
        AddParameter(command, "$id", gameId);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return MinigameCatalogMutationResult.Success;
    }

    public async Task<bool> DeleteGameAsync(
        int gameId,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.OpenConnectionAsync(cancellationToken);
        var connection = db.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM MinigameCatalogGames WHERE Id = $id;";
        AddParameter(command, "$id", gameId);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<MinigameCatalogMutationResult> CreateQuestionAsync(
        string? text,
        CancellationToken cancellationToken = default)
    {
        var normalizedText = NormalizeQuestion(text);
        if (normalizedText is null)
        {
            return MinigameCatalogMutationResult.Invalid;
        }

        await EnsureInitializedAsync(cancellationToken);
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.OpenConnectionAsync(cancellationToken);
        var connection = db.Database.GetDbConnection();
        if (await QuestionTextExistsAsync(connection, normalizedText, null, cancellationToken))
        {
            return MinigameCatalogMutationResult.Duplicate;
        }

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO MinigameCatalogQuestions (Text, SortOrder)
            SELECT $text, COALESCE(MAX(SortOrder), -1) + 1
            FROM MinigameCatalogQuestions;
            """;
        AddParameter(command, "$text", normalizedText);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return MinigameCatalogMutationResult.Success;
    }

    public async Task<MinigameCatalogMutationResult> UpdateQuestionAsync(
        int questionId,
        string? text,
        CancellationToken cancellationToken = default)
    {
        var normalizedText = NormalizeQuestion(text);
        if (normalizedText is null)
        {
            return MinigameCatalogMutationResult.Invalid;
        }

        await EnsureInitializedAsync(cancellationToken);
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.OpenConnectionAsync(cancellationToken);
        var connection = db.Database.GetDbConnection();
        if (!await QuestionExistsAsync(connection, questionId, cancellationToken))
        {
            return MinigameCatalogMutationResult.NotFound;
        }
        if (await QuestionTextExistsAsync(
                connection,
                normalizedText,
                questionId,
                cancellationToken))
        {
            return MinigameCatalogMutationResult.Duplicate;
        }

        await using var command = connection.CreateCommand();
        command.CommandText =
            "UPDATE MinigameCatalogQuestions SET Text = $text WHERE Id = $id;";
        AddParameter(command, "$text", normalizedText);
        AddParameter(command, "$id", questionId);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return MinigameCatalogMutationResult.Success;
    }

    public async Task<bool> DeleteQuestionAsync(
        int questionId,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.OpenConnectionAsync(cancellationToken);
        var connection = db.Database.GetDbConnection();
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        int sortOrder;
        await using (var select = connection.CreateCommand())
        {
            select.Transaction = transaction;
            select.CommandText =
                "SELECT SortOrder FROM MinigameCatalogQuestions WHERE Id = $id;";
            AddParameter(select, "$id", questionId);
            var result = await select.ExecuteScalarAsync(cancellationToken);
            if (result is null || result == DBNull.Value)
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }
            sortOrder = Convert.ToInt32(result, CultureInfo.InvariantCulture);
        }

        await using (var delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM MinigameCatalogQuestions WHERE Id = $id;";
            AddParameter(delete, "$id", questionId);
            await delete.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var shiftHigh = connection.CreateCommand())
        {
            shiftHigh.Transaction = transaction;
            shiftHigh.CommandText =
                "UPDATE MinigameCatalogQuestions SET SortOrder = SortOrder + 1000000 WHERE SortOrder > $sortOrder;";
            AddParameter(shiftHigh, "$sortOrder", sortOrder);
            await shiftHigh.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var shiftBack = connection.CreateCommand())
        {
            shiftBack.Transaction = transaction;
            shiftBack.CommandText =
                "UPDATE MinigameCatalogQuestions SET SortOrder = SortOrder - 1000001 WHERE SortOrder >= 1000000;";
            await shiftBack.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<MinigameCatalogMutationResult> SaveAnswersAsync(
        int gameId,
        IReadOnlyDictionary<int, bool?> values,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        await EnsureInitializedAsync(cancellationToken);
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.OpenConnectionAsync(cancellationToken);
        var connection = db.Database.GetDbConnection();
        if (!await GameExistsAsync(connection, gameId, cancellationToken))
        {
            return MinigameCatalogMutationResult.NotFound;
        }

        var questions = await ReadQuestionItemsAsync(connection, cancellationToken);
        var questionIds = questions.Select(question => question.Id).ToHashSet();
        if (values.Keys.Any(id => !questionIds.Contains(id)))
        {
            return MinigameCatalogMutationResult.Invalid;
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await DeleteAnswersAsync(connection, transaction, gameId, cancellationToken);
        var assigned = questions
            .Where(question => values.TryGetValue(question.Id, out var answer) && answer.HasValue)
            .Select(question => (question.Id, values[question.Id]!.Value))
            .ToArray();
        await InsertAnswersAsync(
            connection,
            transaction,
            gameId,
            assigned,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return MinigameCatalogMutationResult.Success;
    }

    public async Task<MinigameCatalogMutationResult> ReplaceAnswersAsync(
        int gameId,
        IReadOnlyList<bool> answers,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(answers);
        await EnsureInitializedAsync(cancellationToken);
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.OpenConnectionAsync(cancellationToken);
        var connection = db.Database.GetDbConnection();
        if (!await GameExistsAsync(connection, gameId, cancellationToken))
        {
            return MinigameCatalogMutationResult.NotFound;
        }

        var questions = await ReadQuestionItemsAsync(connection, cancellationToken);
        if (questions.Count != answers.Count)
        {
            return MinigameCatalogMutationResult.Invalid;
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await DeleteAnswersAsync(connection, transaction, gameId, cancellationToken);
        await InsertAnswersAsync(
            connection,
            transaction,
            gameId,
            questions.Select((question, index) => (question.Id, answers[index])).ToArray(),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return MinigameCatalogMutationResult.Success;
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        await InitializationGate.WaitAsync(cancellationToken);
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
            await db.Database.OpenConnectionAsync(cancellationToken);
            var connection = db.Database.GetDbConnection();
            await EnsureSchemaAsync(connection, cancellationToken);

            var gameCount = await ExecuteCountAsync(
                connection,
                "MinigameCatalogGames",
                cancellationToken);
            var questionCount = await ExecuteCountAsync(
                connection,
                "MinigameCatalogQuestions",
                cancellationToken);
            if (gameCount == 0 && questionCount == 0 &&
                !string.IsNullOrWhiteSpace(_legacyRootPath) &&
                Directory.Exists(_legacyRootPath))
            {
                await SeedLegacyCatalogAsync(connection, _legacyRootPath, cancellationToken);
            }
        }
        finally
        {
            InitializationGate.Release();
        }
    }

    private static async Task EnsureSchemaAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            PRAGMA foreign_keys = ON;

            CREATE TABLE IF NOT EXISTS MinigameCatalogGames (
                Id INTEGER NOT NULL CONSTRAINT PK_MinigameCatalogGames PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                ImageData BLOB NOT NULL,
                ImageContentType TEXT NOT NULL
            );

            CREATE UNIQUE INDEX IF NOT EXISTS IX_MinigameCatalogGames_Name
                ON MinigameCatalogGames (Name COLLATE NOCASE);

            CREATE TABLE IF NOT EXISTS MinigameCatalogQuestions (
                Id INTEGER NOT NULL CONSTRAINT PK_MinigameCatalogQuestions PRIMARY KEY AUTOINCREMENT,
                Text TEXT NOT NULL,
                SortOrder INTEGER NOT NULL
            );

            CREATE UNIQUE INDEX IF NOT EXISTS IX_MinigameCatalogQuestions_Text
                ON MinigameCatalogQuestions (Text);
            CREATE UNIQUE INDEX IF NOT EXISTS IX_MinigameCatalogQuestions_SortOrder
                ON MinigameCatalogQuestions (SortOrder);

            CREATE TABLE IF NOT EXISTS MinigameCatalogAnswers (
                GameId INTEGER NOT NULL,
                QuestionId INTEGER NOT NULL,
                AnswerYes INTEGER NOT NULL,
                CONSTRAINT PK_MinigameCatalogAnswers PRIMARY KEY (GameId, QuestionId),
                CONSTRAINT FK_MinigameCatalogAnswers_Games_GameId
                    FOREIGN KEY (GameId) REFERENCES MinigameCatalogGames (Id) ON DELETE CASCADE,
                CONSTRAINT FK_MinigameCatalogAnswers_Questions_QuestionId
                    FOREIGN KEY (QuestionId) REFERENCES MinigameCatalogQuestions (Id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS IX_MinigameCatalogAnswers_QuestionId
                ON MinigameCatalogAnswers (QuestionId);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task SeedLegacyCatalogAsync(
        DbConnection connection,
        string rootPath,
        CancellationToken cancellationToken)
    {
        var questionPath = Path.Combine(rootPath, "questions.txt");
        if (!File.Exists(questionPath))
        {
            return;
        }

        string[] questions;
        try
        {
            questions = File.ReadLines(questionPath)
                .Select(question => question.Trim())
                .Where(question => !string.IsNullOrWhiteSpace(question))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }
        catch (IOException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }

        if (questions.Length < MinigameQuestionStore.MinimumQuestionCount)
        {
            return;
        }

        string[] imagePaths;
        try
        {
            imagePaths = Directory
                .EnumerateFiles(rootPath, "*", SearchOption.TopDirectoryOnly)
                .Where(path => SupportedImageExtensions.Contains(Path.GetExtension(path)))
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .GroupBy(Path.GetFileNameWithoutExtension, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToArray();
        }
        catch (IOException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }

        if (imagePaths.Length == 0)
        {
            return;
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var questionIds = new List<int>(questions.Length);
        for (var index = 0; index < questions.Length; index++)
        {
            await using var questionCommand = connection.CreateCommand();
            questionCommand.Transaction = transaction;
            questionCommand.CommandText =
                """
                INSERT INTO MinigameCatalogQuestions (Text, SortOrder)
                VALUES ($text, $sortOrder);
                SELECT last_insert_rowid();
                """;
            AddParameter(questionCommand, "$text", questions[index]);
            AddParameter(questionCommand, "$sortOrder", index);
            var insertedId = await questionCommand.ExecuteScalarAsync(cancellationToken);
            questionIds.Add(Convert.ToInt32(insertedId, CultureInfo.InvariantCulture));
        }

        foreach (var imagePath in imagePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            byte[] imageData;
            try
            {
                imageData = await File.ReadAllBytesAsync(imagePath, cancellationToken);
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            var name = Path.GetFileNameWithoutExtension(imagePath).Trim();
            if (string.IsNullOrWhiteSpace(name) || name.Length > MaximumGameNameLength ||
                imageData.Length == 0)
            {
                continue;
            }

            await using var gameCommand = connection.CreateCommand();
            gameCommand.Transaction = transaction;
            gameCommand.CommandText =
                """
                INSERT INTO MinigameCatalogGames (Name, ImageData, ImageContentType)
                VALUES ($name, $imageData, $contentType);
                SELECT last_insert_rowid();
                """;
            AddParameter(gameCommand, "$name", name);
            AddParameter(gameCommand, "$imageData", imageData);
            AddParameter(gameCommand, "$contentType", GetContentType(Path.GetExtension(imagePath)));
            var insertedGameId = await gameCommand.ExecuteScalarAsync(cancellationToken);
            var gameId = Convert.ToInt32(insertedGameId, CultureInfo.InvariantCulture);

            var answerPath = Path.Combine(rootPath, $"{name}.txt");
            var imported = MinigameAnswerImportParser.TryParseFile(
                answerPath,
                questions.Length,
                out var answers);
            if (!imported)
            {
                continue;
            }

            await InsertAnswersAsync(
                connection,
                transaction,
                gameId,
                questionIds.Select((questionId, index) => (questionId, answers[index])).ToArray(),
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task InsertAnswersAsync(
        DbConnection connection,
        DbTransaction transaction,
        int gameId,
        IReadOnlyList<(int QuestionId, bool AnswerYes)> answers,
        CancellationToken cancellationToken)
    {
        const int batchSize = 200;
        for (var offset = 0; offset < answers.Count; offset += batchSize)
        {
            var count = Math.Min(batchSize, answers.Count - offset);
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            var values = new string[count];
            for (var index = 0; index < count; index++)
            {
                var source = answers[offset + index];
                values[index] = $"($gameId{index}, $questionId{index}, $answer{index})";
                AddParameter(command, $"$gameId{index}", gameId);
                AddParameter(command, $"$questionId{index}", source.QuestionId);
                AddParameter(command, $"$answer{index}", source.AnswerYes ? 1 : 0);
            }
            command.CommandText =
                "INSERT INTO MinigameCatalogAnswers (GameId, QuestionId, AnswerYes) VALUES " +
                string.Join(", ", values) + ";";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task DeleteAnswersAsync(
        DbConnection connection,
        DbTransaction transaction,
        int gameId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM MinigameCatalogAnswers WHERE GameId = $gameId;";
        AddParameter(command, "$gameId", gameId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<MinigameCatalogQuestionItem>> ReadQuestionItemsAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        var questions = new List<MinigameCatalogQuestionItem>();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT Id, SortOrder, Text FROM MinigameCatalogQuestions ORDER BY SortOrder, Id;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            questions.Add(new MinigameCatalogQuestionItem(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetString(2)));
        }
        return questions;
    }

    private static async Task<int> ExecuteCountAsync(
        DbConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {tableName};";
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    private static async Task<bool> GameExistsAsync(
        DbConnection connection,
        int gameId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM MinigameCatalogGames WHERE Id = $id;";
        AddParameter(command, "$id", gameId);
        return Convert.ToInt32(
            await command.ExecuteScalarAsync(cancellationToken),
            CultureInfo.InvariantCulture) > 0;
    }

    private static async Task<bool> QuestionExistsAsync(
        DbConnection connection,
        int questionId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM MinigameCatalogQuestions WHERE Id = $id;";
        AddParameter(command, "$id", questionId);
        return Convert.ToInt32(
            await command.ExecuteScalarAsync(cancellationToken),
            CultureInfo.InvariantCulture) > 0;
    }

    private static async Task<bool> GameNameExistsAsync(
        DbConnection connection,
        string name,
        int? excludedId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = excludedId is null
            ? "SELECT COUNT(*) FROM MinigameCatalogGames WHERE Name = $name COLLATE NOCASE;"
            : "SELECT COUNT(*) FROM MinigameCatalogGames WHERE Name = $name COLLATE NOCASE AND Id <> $id;";
        AddParameter(command, "$name", name);
        if (excludedId is int id)
        {
            AddParameter(command, "$id", id);
        }
        return Convert.ToInt32(
            await command.ExecuteScalarAsync(cancellationToken),
            CultureInfo.InvariantCulture) > 0;
    }

    private static async Task<bool> QuestionTextExistsAsync(
        DbConnection connection,
        string text,
        int? excludedId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = excludedId is null
            ? "SELECT COUNT(*) FROM MinigameCatalogQuestions WHERE Text = $text;"
            : "SELECT COUNT(*) FROM MinigameCatalogQuestions WHERE Text = $text AND Id <> $id;";
        AddParameter(command, "$text", text);
        if (excludedId is int id)
        {
            AddParameter(command, "$id", id);
        }
        return Convert.ToInt32(
            await command.ExecuteScalarAsync(cancellationToken),
            CultureInfo.InvariantCulture) > 0;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static string? NormalizeName(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) || normalized.Length > MaximumGameNameLength
            ? null
            : normalized;
    }

    private static string? NormalizeQuestion(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) || normalized.Length > MaximumQuestionLength
            ? null
            : normalized;
    }

    private static bool IsSupportedContentType(string? contentType) =>
        contentType is "image/png" or "image/jpeg" or "image/webp" or "image/gif";

    private static string GetContentType(string extension) =>
        extension.ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "application/octet-stream"
        };

    private static string? ResolveLegacyRootPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Resources", "Minigames", "GameCards"),
            Path.Combine(Directory.GetCurrentDirectory(), "Resources", "Minigames", "GameCards"),
            Path.Combine(
                Directory.GetCurrentDirectory(),
                "src",
                "BadWolfQuiz.Web",
                "Resources",
                "Minigames",
                "GameCards")
        };
        return candidates.FirstOrDefault(Directory.Exists);
    }

    private static void Shuffle<T>(IList<T> items)
    {
        for (var index = items.Count - 1; index > 0; index--)
        {
            var swapIndex = Random.Shared.Next(index + 1);
            (items[index], items[swapIndex]) = (items[swapIndex], items[index]);
        }
    }
}

public static class MinigameAnswerImportParser
{
    public static MinigameAnswerImportResult Parse(string? content, int expectedCount)
    {
        if (content is null || expectedCount < 0)
        {
            return MinigameAnswerImportResult.Invalid(0, expectedCount);
        }

        var values = new List<bool>();
        using var reader = new StringReader(content);
        string? line;
        var lineNumber = 0;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            var value = line.Trim();
            if (value == "1")
            {
                values.Add(true);
            }
            else if (value == "0")
            {
                values.Add(false);
            }
            else
            {
                return MinigameAnswerImportResult.Invalid(lineNumber, expectedCount);
            }
        }

        return values.Count == expectedCount
            ? MinigameAnswerImportResult.Valid(values)
            : MinigameAnswerImportResult.Invalid(0, expectedCount, values.Count);
    }

    public static bool TryParseFile(
        string path,
        int expectedCount,
        out IReadOnlyList<bool> answers)
    {
        answers = [];
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            var result = Parse(File.ReadAllText(path), expectedCount);
            if (!result.Success)
            {
                return false;
            }
            answers = result.Answers;
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}

public enum MinigameCatalogMutationResult
{
    Success,
    Invalid,
    Duplicate,
    NotFound
}

public sealed record MinigameCatalogCounts(int GameCount, int QuestionCount);

public sealed record MinigameCatalogImage(byte[] Data, string ContentType);

public sealed record MinigameCatalogGameItem(
    int Id,
    string Name,
    string ImageContentType,
    long ImageBytes,
    int AssignedAnswerCount);

public sealed record MinigameCatalogQuestionItem(int Id, int SortOrder, string Text);

public sealed record MinigameCatalogAnswerItem(
    int QuestionId,
    int SortOrder,
    string QuestionText,
    bool? AnswerYes);

public sealed record MinigameAnswerImportResult(
    bool Success,
    IReadOnlyList<bool> Answers,
    int InvalidLineNumber,
    int ExpectedCount,
    int ActualCount)
{
    public static MinigameAnswerImportResult Valid(IReadOnlyList<bool> answers) =>
        new(true, answers, 0, answers.Count, answers.Count);

    public static MinigameAnswerImportResult Invalid(
        int lineNumber,
        int expectedCount,
        int actualCount = -1) =>
        new(false, [], lineNumber, expectedCount, actualCount);
}
