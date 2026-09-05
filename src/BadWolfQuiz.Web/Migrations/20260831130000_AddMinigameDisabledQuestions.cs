using BadWolfQuiz.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BadWolfQuiz.Web.Migrations;

[DbContext(typeof(QuizDbContext))]
[Migration("20260831130000_AddMinigameDisabledQuestions")]
public partial class AddMinigameDisabledQuestions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE IF NOT EXISTS MinigameDisabledQuestions (
                QuestionId INTEGER NOT NULL CONSTRAINT PK_MinigameDisabledQuestions PRIMARY KEY,
                CONSTRAINT FK_MinigameDisabledQuestions_Questions_QuestionId
                    FOREIGN KEY (QuestionId) REFERENCES MinigameCatalogQuestions (Id) ON DELETE CASCADE
            );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS MinigameDisabledQuestions;");
    }
}
