using BadWolfQuiz.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BadWolfQuiz.Web.Migrations;

[DbContext(typeof(QuizDbContext))]
[Migration("20260823184000_AddFinalQuestionDescriptionBlocks")]
public partial class AddFinalQuestionDescriptionBlocks : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "FinalDescriptionContentBlocks",
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
                Autoplay = table.Column<bool>(type: "INTEGER", nullable: false),
                FileData = table.Column<byte[]>(type: "BLOB", nullable: true),
                FileContentType = table.Column<string>(type: "TEXT", nullable: true),
                FileName = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FinalDescriptionContentBlocks", x => x.Id);
                table.ForeignKey(
                    name: "FK_FinalDescriptionContentBlocks_Quizzes_QuizId",
                    column: x => x.QuizId,
                    principalTable: "Quizzes",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_FinalDescriptionContentBlocks_QuizId",
            table: "FinalDescriptionContentBlocks",
            column: "QuizId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "FinalDescriptionContentBlocks");
    }
}
