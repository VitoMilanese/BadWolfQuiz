using BadWolfQuiz.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BadWolfQuiz.Web.Migrations;

[DbContext(typeof(QuizDbContext))]
[Migration("20260831090000_AddMinigameCatalogTables")]
public partial class AddMinigameCatalogTables : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
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
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP TABLE IF EXISTS MinigameCatalogAnswers;
            DROP TABLE IF EXISTS MinigameCatalogQuestions;
            DROP TABLE IF EXISTS MinigameCatalogGames;
            """);
    }
}
