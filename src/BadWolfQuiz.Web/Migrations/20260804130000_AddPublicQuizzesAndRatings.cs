using BadWolfQuiz.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BadWolfQuiz.Web.Migrations;

[DbContext(typeof(QuizDbContext))]
[Migration("20260804130000_AddPublicQuizzesAndRatings")]
public partial class AddPublicQuizzesAndRatings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsPublic",
            table: "Quizzes",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<DateTime>(
            name: "PublishedAtUtc",
            table: "Quizzes",
            type: "TEXT",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "QuizRatings",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                QuizId = table.Column<int>(type: "INTEGER", nullable: false),
                GameSessionId = table.Column<int>(type: "INTEGER", nullable: false),
                RaterKey = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                Score = table.Column<int>(type: "INTEGER", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_QuizRatings", x => x.Id);
                table.ForeignKey(
                    name: "FK_QuizRatings_GameSessions_GameSessionId",
                    column: x => x.GameSessionId,
                    principalTable: "GameSessions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_QuizRatings_Quizzes_QuizId",
                    column: x => x.QuizId,
                    principalTable: "Quizzes",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_QuizRatings_GameSessionId_RaterKey",
            table: "QuizRatings",
            columns: new[] { "GameSessionId", "RaterKey" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_QuizRatings_QuizId",
            table: "QuizRatings",
            column: "QuizId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "QuizRatings");
        migrationBuilder.DropColumn(name: "IsPublic", table: "Quizzes");
        migrationBuilder.DropColumn(name: "PublishedAtUtc", table: "Quizzes");
    }
}
