using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BadWolfQuiz.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddUserQuestionConversations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAtUtc",
                table: "UserQuestions",
                type: "TEXT",
                nullable: false,
                defaultValue: DateTimeOffset.MinValue);

            migrationBuilder.CreateTable(
                name: "UserQuestionMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserQuestionId = table.Column<int>(type: "INTEGER", nullable: false),
                    AuthorType = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserQuestionMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserQuestionMessages_UserQuestions_UserQuestionId",
                        column: x => x.UserQuestionId,
                        principalTable: "UserQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO "UserQuestionMessages"
                    ("UserQuestionId", "AuthorType", "Text", "CreatedAtUtc")
                SELECT
                    "Id",
                    0,
                    "Question",
                    "CreatedAtUtc"
                FROM "UserQuestions";
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO "UserQuestionMessages"
                    ("UserQuestionId", "AuthorType", "Text", "CreatedAtUtc")
                SELECT
                    "Id",
                    1,
                    "Answer",
                    "AnsweredAtUtc"
                FROM "UserQuestions"
                WHERE "Answer" IS NOT NULL
                  AND "AnsweredAtUtc" IS NOT NULL;
                """);

            migrationBuilder.Sql(
                """
                UPDATE "UserQuestions"
                SET "UpdatedAtUtc" = COALESCE("AnsweredAtUtc", "CreatedAtUtc");
                """);

            migrationBuilder.DropColumn(
                name: "Answer",
                table: "UserQuestions");

            migrationBuilder.DropColumn(
                name: "AnsweredAtUtc",
                table: "UserQuestions");

            migrationBuilder.DropColumn(
                name: "Question",
                table: "UserQuestions");

            migrationBuilder.CreateIndex(
                name: "IX_UserQuestionMessages_UserQuestionId",
                table: "UserQuestionMessages",
                column: "UserQuestionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Question",
                table: "UserQuestions",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Answer",
                table: "UserQuestions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AnsweredAtUtc",
                table: "UserQuestions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "UserQuestions"
                SET "Question" = COALESCE(
                    (
                        SELECT "Text"
                        FROM "UserQuestionMessages"
                        WHERE "UserQuestionId" = "UserQuestions"."Id"
                          AND "AuthorType" = 0
                        ORDER BY "CreatedAtUtc", "Id"
                        LIMIT 1
                    ),
                    ''
                );
                """);

            migrationBuilder.Sql(
                """
                UPDATE "UserQuestions"
                SET
                    "Answer" = (
                        SELECT "Text"
                        FROM "UserQuestionMessages"
                        WHERE "UserQuestionId" = "UserQuestions"."Id"
                          AND "AuthorType" = 1
                        ORDER BY "CreatedAtUtc", "Id"
                        LIMIT 1
                    ),
                    "AnsweredAtUtc" = (
                        SELECT "CreatedAtUtc"
                        FROM "UserQuestionMessages"
                        WHERE "UserQuestionId" = "UserQuestions"."Id"
                          AND "AuthorType" = 1
                        ORDER BY "CreatedAtUtc", "Id"
                        LIMIT 1
                    );
                """);

            migrationBuilder.DropTable(
                name: "UserQuestionMessages");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "UserQuestions");
        }
    }
}
