using BadWolfQuiz.Web.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Tests;

public sealed class DatabaseMigrationServiceTests
{
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
            "QuizQuestions",
            "ExcludeFromRandomWagerSelection"));

        await using var historyCommand = connection.CreateCommand();
        historyCommand.CommandText =
            "SELECT COUNT(*) FROM \"__EFMigrationsHistory\";";

        Assert.Equal(
            2L,
            Convert.ToInt64(await historyCommand.ExecuteScalarAsync()));
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
