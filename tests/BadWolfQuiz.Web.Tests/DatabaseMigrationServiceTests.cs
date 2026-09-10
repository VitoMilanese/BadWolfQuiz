using BadWolfQuiz.Web.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Tests;

public sealed class DatabaseMigrationServiceTests
{
    [Fact]
    public async Task MigrateAsync_renames_legacy_quiz_rating_identity_column()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<QuizDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new QuizDbContext(options);
        await db.Database.MigrateAsync();

        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                """
                DROP INDEX "IX_QuizRatings_GameSessionId_RaterKey";
                ALTER TABLE "QuizRatings" RENAME COLUMN "RaterKey" TO "PlayerName";
                CREATE UNIQUE INDEX "IX_QuizRatings_GameSessionId_PlayerName"
                    ON "QuizRatings" ("GameSessionId", "PlayerName");
                """;
            await command.ExecuteNonQueryAsync();
        }

        await DatabaseMigrationService.MigrateAsync(db);

        Assert.True(await ColumnExistsAsync(connection, "QuizRatings", "RaterKey"));
        Assert.False(await ColumnExistsAsync(connection, "QuizRatings", "PlayerName"));
    }

    [Fact]
    public async Task MigrateAsync_upgrades_database_created_without_migration_history()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                """
                CREATE TABLE "QuizRounds" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_QuizRounds" PRIMARY KEY AUTOINCREMENT
                );

                CREATE TABLE "QuizQuestions" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_QuizQuestions" PRIMARY KEY AUTOINCREMENT
                );

                CREATE TABLE "Quizzes" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_Quizzes" PRIMARY KEY AUTOINCREMENT
                );

                CREATE TABLE "GameSessions" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_GameSessions" PRIMARY KEY AUTOINCREMENT
                );
                """;

            await command.ExecuteNonQueryAsync();
        }

        var options = new DbContextOptionsBuilder<QuizDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new QuizDbContext(options);

        await DatabaseMigrationService.MigrateAsync(db);

        Assert.True(await ColumnExistsAsync(
            connection,
            "QuizRounds",
            "UseRandomWagerQuestions"));
        Assert.True(await ColumnExistsAsync(
            connection,
            "QuizRounds",
            "RandomWagerQuestionCount"));
        Assert.True(await ColumnExistsAsync(
            connection,
            "QuizRounds",
            "UseRandomAnonymousSharedWagerQuestions"));
        Assert.True(await ColumnExistsAsync(
            connection,
            "QuizRounds",
            "RandomAnonymousSharedWagerQuestionCount"));
        Assert.True(await ColumnExistsAsync(
            connection,
            "QuizQuestions",
            "ExcludeFromRandomWagerSelection"));

        Assert.True(await TableExistsAsync(
            connection,
            "FinalQuestionContentBlocks"));
        Assert.True(await TableExistsAsync(
            connection,
            "FinalAnswerContentBlocks"));
        Assert.True(await TableExistsAsync(
            connection,
            "Hosts"));
        Assert.True(await ColumnExistsAsync(
            connection,
            "Quizzes",
            "HostId"));
        Assert.True(await ColumnExistsAsync(
            connection,
            "GameSessions",
            "HostId"));
        Assert.True(await ColumnExistsAsync(
            connection,
            "Hosts",
            "PasswordResetTokenHash"));
        Assert.True(await ColumnExistsAsync(
            connection,
            "Hosts",
            "PasswordResetTokenExpiresAtUtc"));

        await using var historyCommand = connection.CreateCommand();
        historyCommand.CommandText =
            "SELECT COUNT(*) FROM \"__EFMigrationsHistory\";";

        var expectedMigrationCount =
            db.Database.GetMigrations().LongCount();

        Assert.Equal(
            expectedMigrationCount,
            Convert.ToInt64(await historyCommand.ExecuteScalarAsync()));
    }

    [Fact]
    public async Task MigrateAsync_upgrades_legacy_player_achievement_table_without_recreating_it()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<QuizDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new QuizDbContext(options);
        await db.Database.MigrateAsync();

        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                """
                DROP TABLE "PlayerGameAccountLinks";
                DROP TABLE "UserQuestionAccountLinks";
                DROP TABLE "PlayerAchievements";
                DELETE FROM "__EFMigrationsHistory"
                WHERE "MigrationId" = '20260909225025_AddPlayerAchievements';

                CREATE TABLE "PlayerAchievements" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_PlayerAchievements" PRIMARY KEY AUTOINCREMENT,
                    "HostId" TEXT NOT NULL,
                    "PlayerKey" TEXT NOT NULL,
                    "AchievementCode" TEXT NOT NULL,
                    "UnlockedAtUtc" TEXT NOT NULL,
                    "SourceGameSessionId" INTEGER NULL
                );
                CREATE UNIQUE INDEX "IX_PlayerAchievements_HostId_PlayerKey_AchievementCode"
                    ON "PlayerAchievements" ("HostId", "PlayerKey", "AchievementCode");
                CREATE INDEX "IX_PlayerAchievements_SourceGameSessionId"
                    ON "PlayerAchievements" ("SourceGameSessionId");

                INSERT INTO "PlayerAchievements"
                    ("HostId", "PlayerKey", "AchievementCode", "UnlockedAtUtc")
                VALUES
                    ('host-1', 'player-1', 'first-game', '2026-09-10T10:00:00Z');

                INSERT OR IGNORE INTO "__EFMigrationsHistory"
                    ("MigrationId", "ProductVersion")
                VALUES
                    ('20260909211151_AddPlayerAchievements', '8.0.29');
                """;
            await command.ExecuteNonQueryAsync();
        }

        await DatabaseMigrationService.MigrateAsync(db);

        Assert.True(await TableExistsAsync(connection, "PlayerAchievements"));
        Assert.True(await TableExistsAsync(connection, "PlayerGameAccountLinks"));
        Assert.True(await TableExistsAsync(connection, "UserQuestionAccountLinks"));
        Assert.True(await ColumnExistsAsync(connection, "PlayerAchievements", "AccountId"));
        Assert.True(await ColumnAllowsNullAsync(connection, "PlayerAchievements", "HostId"));
        Assert.True(await ColumnAllowsNullAsync(connection, "PlayerAchievements", "PlayerKey"));

        await using (var legacyDataCommand = connection.CreateCommand())
        {
            legacyDataCommand.CommandText =
                """
                SELECT COUNT(*)
                FROM "PlayerAchievements"
                WHERE "HostId" = 'host-1'
                  AND "PlayerKey" = 'player-1'
                  AND "AchievementCode" = 'first-game';
                """;
            Assert.Equal(
                1L,
                Convert.ToInt64(await legacyDataCommand.ExecuteScalarAsync()));
        }

        await using (var accountAchievementCommand = connection.CreateCommand())
        {
            accountAchievementCommand.CommandText =
                """
                INSERT INTO "PlayerAchievements"
                    ("AccountId", "AchievementCode", "UnlockedAtUtc")
                VALUES
                    ('account-1', 'account-only', '2026-09-10T10:01:00Z');
                """;
            await accountAchievementCommand.ExecuteNonQueryAsync();
        }

        await using var historyCommand = connection.CreateCommand();
        historyCommand.CommandText =
            """
            SELECT COUNT(*)
            FROM "__EFMigrationsHistory"
            WHERE "MigrationId" = '20260909225025_AddPlayerAchievements';
            """;
        Assert.Equal(1L, Convert.ToInt64(await historyCommand.ExecuteScalarAsync()));
    }

    private static async Task<bool> TableExistsAsync(
        SqliteConnection connection,
        string tableName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE type = 'table' AND name = $tableName;
            """;
        command.Parameters.AddWithValue("$tableName", tableName);

        return Convert.ToInt64(await command.ExecuteScalarAsync()) == 1L;
    }

    private static async Task<bool> ColumnExistsAsync(
        SqliteConnection connection,
        string tableName,
        string columnName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{tableName}\");";

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            if (string.Equals(
                    reader.GetString(1),
                    columnName,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static async Task<bool> ColumnAllowsNullAsync(
        SqliteConnection connection,
        string tableName,
        string columnName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{tableName}\");";

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            if (string.Equals(
                    reader.GetString(1),
                    columnName,
                    StringComparison.Ordinal))
            {
                return reader.GetInt32(3) == 0;
            }
        }

        return false;
    }
}
