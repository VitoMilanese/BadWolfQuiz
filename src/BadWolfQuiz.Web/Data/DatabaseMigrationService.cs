using System.Data;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Data;

public static class DatabaseMigrationService
{
    private const string InitialMigrationId =
        "20260729023333_InitialCreate";

    private const string EfProductVersion = "8.0.29";

    private const string ContentBlockAutoplayMigrationId =
        "20260816103000_AddContentBlockAutoplay";

    private static readonly string[] ContentBlockAutoplayTables =
    [
        "QuestionContentBlocks",
        "AnswerContentBlocks",
        "FinalQuestionContentBlocks",
        "FinalAnswerContentBlocks",
        "RoundDescriptionContentBlocks",
        "CategoryDescriptionContentBlocks"
    ];

    public static async Task MigrateAsync(
        QuizDbContext db,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);

        await BootstrapMigrationHistoryAsync(db, cancellationToken);
        await UpgradeLegacyQuizRatingsAsync(db, cancellationToken);
        await PrepareContentBlockAutoplayMigrationAsync(db, cancellationToken);
        await db.Database.MigrateAsync(cancellationToken);
        await EnsureContentBlockAutoplayColumnsAsync(db, cancellationToken);
    }

    private static async Task PrepareContentBlockAutoplayMigrationAsync(
        QuizDbContext db,
        CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State == ConnectionState.Closed;

        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            if (!await TableExistsAsync(connection, "__EFMigrationsHistory", cancellationToken) ||
                await MigrationAppliedAsync(
                    connection,
                    ContentBlockAutoplayMigrationId,
                    cancellationToken))
            {
                return;
            }

            var hasMissingContentBlockTable = false;
            foreach (var tableName in ContentBlockAutoplayTables)
            {
                if (!await TableExistsAsync(connection, tableName, cancellationToken))
                {
                    hasMissingContentBlockTable = true;
                    break;
                }
            }

            if (!hasMissingContentBlockTable)
            {
                return;
            }

            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                INSERT OR IGNORE INTO "__EFMigrationsHistory"
                    ("MigrationId", "ProductVersion")
                VALUES
                    ($migrationId, $productVersion);
                """;
            var migrationId = command.CreateParameter();
            migrationId.ParameterName = "$migrationId";
            migrationId.Value = ContentBlockAutoplayMigrationId;
            command.Parameters.Add(migrationId);
            var productVersion = command.CreateParameter();
            productVersion.ParameterName = "$productVersion";
            productVersion.Value = EfProductVersion;
            command.Parameters.Add(productVersion);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task EnsureContentBlockAutoplayColumnsAsync(
        QuizDbContext db,
        CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State == ConnectionState.Closed;

        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            foreach (var tableName in ContentBlockAutoplayTables)
            {
                if (!await TableExistsAsync(connection, tableName, cancellationToken) ||
                    await ColumnExistsAsync(connection, tableName, "Autoplay", cancellationToken))
                {
                    continue;
                }

                await using var command = connection.CreateCommand();
                command.CommandText =
                    $"ALTER TABLE \"{tableName}\" ADD COLUMN \"Autoplay\" INTEGER NOT NULL DEFAULT 0;";
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<bool> MigrationAppliedAsync(
        System.Data.Common.DbConnection connection,
        string migrationId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT COUNT(*)
            FROM "__EFMigrationsHistory"
            WHERE "MigrationId" = $migrationId;
            """;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "$migrationId";
        parameter.Value = migrationId;
        command.Parameters.Add(parameter);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(result) > 0;
    }

    private static async Task UpgradeLegacyQuizRatingsAsync(
        QuizDbContext db,
        CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State == ConnectionState.Closed;

        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            if (!await TableExistsAsync(connection, "QuizRatings", cancellationToken) ||
                await ColumnExistsAsync(connection, "QuizRatings", "RaterKey", cancellationToken) ||
                !await ColumnExistsAsync(connection, "QuizRatings", "PlayerName", cancellationToken))
            {
                return;
            }

            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                DROP INDEX IF EXISTS "IX_QuizRatings_GameSessionId_PlayerName";
                ALTER TABLE "QuizRatings" RENAME COLUMN "PlayerName" TO "RaterKey";
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_QuizRatings_GameSessionId_RaterKey"
                    ON "QuizRatings" ("GameSessionId", "RaterKey");
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task BootstrapMigrationHistoryAsync(
        QuizDbContext db,
        CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State == ConnectionState.Closed;

        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            var hasLegacySchema = await TableExistsAsync(
                connection,
                "QuizRounds",
                cancellationToken);

            var hasMigrationHistory = await TableExistsAsync(
                connection,
                "__EFMigrationsHistory",
                cancellationToken);

            if (!hasLegacySchema || hasMigrationHistory)
            {
                return;
            }

            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                    "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                    "ProductVersion" TEXT NOT NULL
                );

                INSERT OR IGNORE INTO "__EFMigrationsHistory"
                    ("MigrationId", "ProductVersion")
                VALUES
                    ($migrationId, $productVersion);
                """;

            var migrationId = command.CreateParameter();
            migrationId.ParameterName = "$migrationId";
            migrationId.Value = InitialMigrationId;
            command.Parameters.Add(migrationId);

            var productVersion = command.CreateParameter();
            productVersion.ParameterName = "$productVersion";
            productVersion.Value = EfProductVersion;
            command.Parameters.Add(productVersion);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<bool> TableExistsAsync(
        System.Data.Common.DbConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE type = 'table' AND name = $tableName;
            """;

        var parameter = command.CreateParameter();
        parameter.ParameterName = "$tableName";
        parameter.Value = tableName;
        command.Parameters.Add(parameter);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(result) > 0;
    }

    private static async Task<bool> ColumnExistsAsync(
        System.Data.Common.DbConnection connection,
        string tableName,
        string columnName,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{tableName}\");";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (string.Equals(reader.GetString(1), columnName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
