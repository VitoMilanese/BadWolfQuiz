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

    private const string PlayerAchievementsMigrationId =
        "20260909225025_AddPlayerAchievements";

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
        await PreparePlayerAchievementsMigrationAsync(db, cancellationToken);
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

    private static async Task PreparePlayerAchievementsMigrationAsync(
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
                    PlayerAchievementsMigrationId,
                    cancellationToken))
            {
                return;
            }

            var hasPlayerAchievements = await TableExistsAsync(
                connection,
                "PlayerAchievements",
                cancellationToken);
            var hasPlayerGameAccountLinks = await TableExistsAsync(
                connection,
                "PlayerGameAccountLinks",
                cancellationToken);
            var hasUserQuestionAccountLinks = await TableExistsAsync(
                connection,
                "UserQuestionAccountLinks",
                cancellationToken);

            if (!hasPlayerAchievements &&
                !hasPlayerGameAccountLinks &&
                !hasUserQuestionAccountLinks)
            {
                return;
            }

            var hasAccountId = hasPlayerAchievements &&
                await ColumnExistsAsync(
                    connection,
                    "PlayerAchievements",
                    "AccountId",
                    cancellationToken);
            var playerAchievementsNeedsRebuild = hasPlayerAchievements &&
                (!hasAccountId ||
                 !await ColumnAllowsNullAsync(
                     connection,
                     "PlayerAchievements",
                     "HostId",
                     cancellationToken) ||
                 !await ColumnAllowsNullAsync(
                     connection,
                     "PlayerAchievements",
                     "PlayerKey",
                     cancellationToken));

            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            try
            {
                if (playerAchievementsNeedsRebuild)
                {
                    await using var rebuildCommand = connection.CreateCommand();
                    rebuildCommand.Transaction = transaction;
                    var accountIdProjection = hasAccountId ? "\"AccountId\"" : "NULL";
                    rebuildCommand.CommandText =
                        $"""
                        DROP INDEX IF EXISTS "IX_PlayerAchievements_AccountId_AchievementCode";
                        DROP INDEX IF EXISTS "IX_PlayerAchievements_HostId_PlayerKey_AchievementCode";
                        DROP INDEX IF EXISTS "IX_PlayerAchievements_SourceGameSessionId";

                        ALTER TABLE "PlayerAchievements"
                            RENAME TO "__PlayerAchievementsLegacy";

                        CREATE TABLE "PlayerAchievements" (
                            "Id" INTEGER NOT NULL CONSTRAINT "PK_PlayerAchievements" PRIMARY KEY AUTOINCREMENT,
                            "AccountId" TEXT NULL,
                            "HostId" TEXT NULL,
                            "PlayerKey" TEXT NULL,
                            "AchievementCode" TEXT NOT NULL,
                            "UnlockedAtUtc" TEXT NOT NULL,
                            "SourceGameSessionId" INTEGER NULL
                        );

                        INSERT INTO "PlayerAchievements" (
                            "Id",
                            "AccountId",
                            "HostId",
                            "PlayerKey",
                            "AchievementCode",
                            "UnlockedAtUtc",
                            "SourceGameSessionId")
                        SELECT
                            "Id",
                            {accountIdProjection},
                            "HostId",
                            "PlayerKey",
                            "AchievementCode",
                            "UnlockedAtUtc",
                            "SourceGameSessionId"
                        FROM "__PlayerAchievementsLegacy";

                        DROP TABLE "__PlayerAchievementsLegacy";
                        """;
                    await rebuildCommand.ExecuteNonQueryAsync(cancellationToken);
                }

                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText =
                    """
                    CREATE TABLE IF NOT EXISTS "PlayerAchievements" (
                        "Id" INTEGER NOT NULL CONSTRAINT "PK_PlayerAchievements" PRIMARY KEY AUTOINCREMENT,
                        "AccountId" TEXT NULL,
                        "HostId" TEXT NULL,
                        "PlayerKey" TEXT NULL,
                        "AchievementCode" TEXT NOT NULL,
                        "UnlockedAtUtc" TEXT NOT NULL,
                        "SourceGameSessionId" INTEGER NULL
                    );

                    CREATE TABLE IF NOT EXISTS "PlayerGameAccountLinks" (
                        "Id" INTEGER NOT NULL CONSTRAINT "PK_PlayerGameAccountLinks" PRIMARY KEY AUTOINCREMENT,
                        "GamePlayerId" INTEGER NOT NULL,
                        "AccountId" TEXT NOT NULL,
                        CONSTRAINT "FK_PlayerGameAccountLinks_GamePlayers_GamePlayerId"
                            FOREIGN KEY ("GamePlayerId") REFERENCES "GamePlayers" ("Id") ON DELETE CASCADE
                    );

                    CREATE TABLE IF NOT EXISTS "UserQuestionAccountLinks" (
                        "Id" INTEGER NOT NULL CONSTRAINT "PK_UserQuestionAccountLinks" PRIMARY KEY AUTOINCREMENT,
                        "UserQuestionId" INTEGER NOT NULL,
                        "AccountId" TEXT NOT NULL,
                        CONSTRAINT "FK_UserQuestionAccountLinks_UserQuestions_UserQuestionId"
                            FOREIGN KEY ("UserQuestionId") REFERENCES "UserQuestions" ("Id") ON DELETE CASCADE
                    );

                    CREATE UNIQUE INDEX IF NOT EXISTS "IX_PlayerAchievements_AccountId_AchievementCode"
                        ON "PlayerAchievements" ("AccountId", "AchievementCode");
                    CREATE UNIQUE INDEX IF NOT EXISTS "IX_PlayerAchievements_HostId_PlayerKey_AchievementCode"
                        ON "PlayerAchievements" ("HostId", "PlayerKey", "AchievementCode");
                    CREATE INDEX IF NOT EXISTS "IX_PlayerAchievements_SourceGameSessionId"
                        ON "PlayerAchievements" ("SourceGameSessionId");
                    CREATE INDEX IF NOT EXISTS "IX_PlayerGameAccountLinks_AccountId"
                        ON "PlayerGameAccountLinks" ("AccountId");
                    CREATE UNIQUE INDEX IF NOT EXISTS "IX_PlayerGameAccountLinks_GamePlayerId"
                        ON "PlayerGameAccountLinks" ("GamePlayerId");
                    CREATE INDEX IF NOT EXISTS "IX_UserQuestionAccountLinks_AccountId"
                        ON "UserQuestionAccountLinks" ("AccountId");
                    CREATE UNIQUE INDEX IF NOT EXISTS "IX_UserQuestionAccountLinks_UserQuestionId"
                        ON "UserQuestionAccountLinks" ("UserQuestionId");

                    INSERT OR IGNORE INTO "__EFMigrationsHistory"
                        ("MigrationId", "ProductVersion")
                    VALUES
                        ($migrationId, $productVersion);
                    """;

                var migrationId = command.CreateParameter();
                migrationId.ParameterName = "$migrationId";
                migrationId.Value = PlayerAchievementsMigrationId;
                command.Parameters.Add(migrationId);

                var productVersion = command.CreateParameter();
                productVersion.ParameterName = "$productVersion";
                productVersion.Value = EfProductVersion;
                command.Parameters.Add(productVersion);

                await command.ExecuteNonQueryAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
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

    private static async Task<bool> ColumnAllowsNullAsync(
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
                return reader.GetInt32(3) == 0;
            }
        }

        return false;
    }
}
