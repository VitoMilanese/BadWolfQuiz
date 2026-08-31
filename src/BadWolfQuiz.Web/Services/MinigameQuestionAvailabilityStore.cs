using System.Data.Common;
using BadWolfQuiz.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Services;

public sealed class MinigameQuestionAvailabilityStore(
    IDbContextFactory<QuizDbContext> dbFactory)
{
    private readonly IDbContextFactory<QuizDbContext> _dbFactory =
        dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));

    public async Task<IReadOnlySet<int>> GetDisabledQuestionIdsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var db = await OpenDatabaseAsync(cancellationToken);
        var connection = db.Database.GetDbConnection();
        var ids = new HashSet<int>();

        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT QuestionId FROM MinigameDisabledQuestions ORDER BY QuestionId;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            ids.Add(reader.GetInt32(0));
        }

        return ids;
    }

    public async Task<int> GetEnabledQuestionCountAsync(
        CancellationToken cancellationToken = default)
    {
        await using var db = await OpenDatabaseAsync(cancellationToken);
        var connection = db.Database.GetDbConnection();

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT COUNT(*)
            FROM MinigameCatalogQuestions q
            LEFT JOIN MinigameDisabledQuestions d ON d.QuestionId = q.Id
            WHERE d.QuestionId IS NULL;
            """;
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    public async Task<IReadOnlyList<string>> GetEnabledQuestionsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var db = await OpenDatabaseAsync(cancellationToken);
        var connection = db.Database.GetDbConnection();
        var questions = new List<string>();

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT q.Text
            FROM MinigameCatalogQuestions q
            LEFT JOIN MinigameDisabledQuestions d ON d.QuestionId = q.Id
            WHERE d.QuestionId IS NULL
            ORDER BY q.SortOrder, q.Id;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            questions.Add(reader.GetString(0));
        }

        return questions;
    }

    public async Task<bool> SetEnabledAsync(
        int questionId,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        if (questionId <= 0)
        {
            return false;
        }

        await using var db = await OpenDatabaseAsync(cancellationToken);
        var connection = db.Database.GetDbConnection();
        if (!await QuestionExistsAsync(connection, questionId, cancellationToken))
        {
            return false;
        }

        await using var command = connection.CreateCommand();
        if (enabled)
        {
            command.CommandText =
                "DELETE FROM MinigameDisabledQuestions WHERE QuestionId = $questionId;";
        }
        else
        {
            command.CommandText =
                """
                INSERT OR IGNORE INTO MinigameDisabledQuestions (QuestionId)
                VALUES ($questionId);
                """;
        }
        AddParameter(command, "$questionId", questionId);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return true;
    }

    private async Task<QuizDbContext> OpenDatabaseAsync(
        CancellationToken cancellationToken)
    {
        var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        try
        {
            await db.Database.OpenConnectionAsync(cancellationToken);
            await EnsureSchemaAsync(db.Database.GetDbConnection(), cancellationToken);
            return db;
        }
        catch
        {
            await db.DisposeAsync();
            throw;
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

            CREATE TABLE IF NOT EXISTS MinigameDisabledQuestions (
                QuestionId INTEGER NOT NULL CONSTRAINT PK_MinigameDisabledQuestions PRIMARY KEY,
                CONSTRAINT FK_MinigameDisabledQuestions_Questions_QuestionId
                    FOREIGN KEY (QuestionId) REFERENCES MinigameCatalogQuestions (Id) ON DELETE CASCADE
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<bool> QuestionExistsAsync(
        DbConnection connection,
        int questionId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM MinigameCatalogQuestions WHERE Id = $questionId;";
        AddParameter(command, "$questionId", questionId);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
