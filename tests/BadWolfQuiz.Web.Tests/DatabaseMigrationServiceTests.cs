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
}
