using BadWolfQuiz.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BadWolfQuiz.Web.Migrations;

[DbContext(typeof(QuizDbContext))]
[Migration("20260731010000_AddFinalQuestionContent")]
public partial class AddFinalQuestionContent : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        CreateContentTable(
            migrationBuilder,
            "FinalAnswerContentBlocks");

        CreateContentTable(
            migrationBuilder,
            "FinalQuestionContentBlocks");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "FinalAnswerContentBlocks");
        migrationBuilder.DropTable(name: "FinalQuestionContentBlocks");
    }

    private static void CreateContentTable(
        MigrationBuilder migrationBuilder,
        string tableName)
    {
        migrationBuilder.CreateTable(
            name: tableName,
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                QuizId = table.Column<int>(type: "INTEGER", nullable: false),
                BlockType = table.Column<int>(type: "INTEGER", nullable: false),
                TextContent = table.Column<string>(type: "TEXT", nullable: true),
                TopCaption = table.Column<string>(type: "TEXT", nullable: true),
                BottomCaption = table.Column<string>(type: "TEXT", nullable: true),
                MediaPath = table.Column<string>(type: "TEXT", nullable: true),
                ExternalUrl = table.Column<string>(type: "TEXT", nullable: true),
                SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                AudioOnly = table.Column<bool>(type: "INTEGER", nullable: false),
                FileData = table.Column<byte[]>(type: "BLOB", nullable: true),
                FileContentType = table.Column<string>(type: "TEXT", nullable: true),
                FileName = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey($"PK_{tableName}", x => x.Id);
                table.ForeignKey(
                    name: $"FK_{tableName}_Quizzes_QuizId",
                    column: x => x.QuizId,
                    principalTable: "Quizzes",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: $"IX_{tableName}_QuizId",
            table: tableName,
            column: "QuizId");
    }
}
