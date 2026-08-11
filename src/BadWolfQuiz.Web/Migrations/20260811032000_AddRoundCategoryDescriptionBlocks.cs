using BadWolfQuiz.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BadWolfQuiz.Web.Migrations;

[DbContext(typeof(QuizDbContext))]
[Migration("20260811032000_AddRoundCategoryDescriptionBlocks")]
public partial class AddRoundCategoryDescriptionBlocks : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "CategoryDescriptionContentBlocks",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                QuizCategoryId = table.Column<int>(type: "INTEGER", nullable: false),
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
                table.PrimaryKey("PK_CategoryDescriptionContentBlocks", x => x.Id);
                table.ForeignKey(
                    name: "FK_CategoryDescriptionContentBlocks_QuizCategories_QuizCategoryId",
                    column: x => x.QuizCategoryId,
                    principalTable: "QuizCategories",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "RoundDescriptionContentBlocks",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                QuizRoundId = table.Column<int>(type: "INTEGER", nullable: false),
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
                table.PrimaryKey("PK_RoundDescriptionContentBlocks", x => x.Id);
                table.ForeignKey(
                    name: "FK_RoundDescriptionContentBlocks_QuizRounds_QuizRoundId",
                    column: x => x.QuizRoundId,
                    principalTable: "QuizRounds",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_CategoryDescriptionContentBlocks_QuizCategoryId",
            table: "CategoryDescriptionContentBlocks",
            column: "QuizCategoryId");

        migrationBuilder.CreateIndex(
            name: "IX_RoundDescriptionContentBlocks_QuizRoundId",
            table: "RoundDescriptionContentBlocks",
            column: "QuizRoundId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "CategoryDescriptionContentBlocks");
        migrationBuilder.DropTable(name: "RoundDescriptionContentBlocks");
    }
}
