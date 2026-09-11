from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def read(path):
    return (ROOT / path).read_text(encoding="utf-8")


def write(path, text):
    (ROOT / path).write_text(text, encoding="utf-8")


def replace_once(path, old, new):
    text = read(path)
    count = text.count(old)
    if count != 1:
        raise RuntimeError(f"{path}: expected exactly one match, found {count}\n--- needle ---\n{old[:500]}")
    write(path, text.replace(old, new, 1))


migration_service = "src/BadWolfQuiz.Web/Data/DatabaseMigrationService.cs"

replace_once(
    migration_service,
    """    private const string QuizCategoryColorsMigrationSuffix =\n        \"_AddQuizCategoryColors\";\n\n    private static readonly string[] ContentBlockAutoplayTables =\n""",
    """    private const string QuizCategoryColorsMigrationSuffix =\n        \"_AddQuizCategoryColors\";\n\n    private const string AchievementHistoryEligibilityMigrationSuffix =\n        \"_AddAchievementHistoryEligibility\";\n\n    private static readonly string[] ContentBlockAutoplayTables =\n""",
)

replace_once(
    migration_service,
    """        await PreparePlayerAchievementsMigrationAsync(db, cancellationToken);\n        await PrepareQuizCategoryColorsMigrationAsync(db, cancellationToken);\n        await db.Database.MigrateAsync(cancellationToken);\n""",
    """        await PreparePlayerAchievementsMigrationAsync(db, cancellationToken);\n        await PrepareQuizCategoryColorsMigrationAsync(db, cancellationToken);\n        await PrepareAchievementHistoryEligibilityMigrationAsync(db, cancellationToken);\n        await db.Database.MigrateAsync(cancellationToken);\n""",
)

marker = """    private static async Task EnsureContentBlockAutoplayColumnsAsync(\n"""
method = """    private static async Task PrepareAchievementHistoryEligibilityMigrationAsync(\n        QuizDbContext db,\n        CancellationToken cancellationToken)\n    {\n        var migrationId = db.Database.GetMigrations()\n            .SingleOrDefault(id => id.EndsWith(\n                AchievementHistoryEligibilityMigrationSuffix,\n                StringComparison.Ordinal));\n        if (migrationId is null)\n        {\n            return;\n        }\n\n        var connection = db.Database.GetDbConnection();\n        var shouldClose = connection.State == ConnectionState.Closed;\n        if (shouldClose)\n        {\n            await connection.OpenAsync(cancellationToken);\n        }\n\n        try\n        {\n            if (!await TableExistsAsync(connection, \"__EFMigrationsHistory\", cancellationToken) ||\n                await MigrationAppliedAsync(connection, migrationId, cancellationToken))\n            {\n                return;\n            }\n\n            var hasGamePlayers = await TableExistsAsync(\n                connection,\n                \"GamePlayers\",\n                cancellationToken);\n            if (hasGamePlayers &&\n                !await ColumnExistsAsync(\n                    connection,\n                    \"GamePlayers\",\n                    \"CountsForAchievementHistory\",\n                    cancellationToken))\n            {\n                return;\n            }\n\n            // Legacy/bootstrap databases can intentionally lack GamePlayers, while development\n            // databases may already contain the physical column without the migration-history row.\n            // In both cases, executing ALTER TABLE would fail or duplicate the column, so mark the\n            // migration as physically satisfied.\n            await using var command = connection.CreateCommand();\n            command.CommandText =\n                \"\"\"\n                INSERT OR IGNORE INTO \"__EFMigrationsHistory\"\n                    (\"MigrationId\", \"ProductVersion\")\n                VALUES\n                    ($migrationId, $productVersion);\n                \"\"\";\n\n            var migrationParameter = command.CreateParameter();\n            migrationParameter.ParameterName = \"$migrationId\";\n            migrationParameter.Value = migrationId;\n            command.Parameters.Add(migrationParameter);\n\n            var productVersion = command.CreateParameter();\n            productVersion.ParameterName = \"$productVersion\";\n            productVersion.Value = EfProductVersion;\n            command.Parameters.Add(productVersion);\n\n            await command.ExecuteNonQueryAsync(cancellationToken);\n        }\n        finally\n        {\n            if (shouldClose)\n            {\n                await connection.CloseAsync();\n            }\n        }\n    }\n\n"""
replace_once(migration_service, marker, method + marker)

# Existing service test used the old eager player-view adoption behavior. Under the hardened rule,
# explicitly confirm the account/nickname relationship from the already-finished eligible game.
service_tests = "tests/BadWolfQuiz.Web.Tests/PlayerAchievementServiceTests.cs"
replace_once(
    service_tests,
    """        var accountProgress = await service.LoadForPlayerAsync(\n            host.Id,\n            \"Rose\",\n            session.PublicCode,\n            host.Id);\n""",
    """        await service.AdoptHostNicknameHistoryAsync(\n            host.Id,\n            host.Id,\n            \"Rose\",\n            session.Id);\n\n        var accountProgress = await service.LoadForPlayerAsync(\n            host.Id,\n            \"Rose\",\n            session.PublicCode,\n            host.Id);\n""",
)

print("Issue 622 follow-up compatibility fixes patched successfully.")
