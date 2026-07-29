using System.Data;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Data;

public static class DatabaseMigrationService
{
    private const string InitialMigrationId =
        "20260729023333_InitialCreate";

    private const string EfProductVersion = "8.0.29";

    public static async Task MigrateAsync(
        QuizDbContext db,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);

        await BootstrapMigrationHistoryAsync(db, cancellationToken);
        await db.Database.MigrateAsync(cancellationToken);
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
}
