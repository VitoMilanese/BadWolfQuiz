using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BadWolfQuiz.Web.Migrations
{
    /// <inheritdoc />
    public partial class MoveDiscordMessageIdToUserQuestionMessage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<ulong>(
                name: "DiscordMessageId",
                table: "UserQuestionMessages",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.Sql(
                """
        UPDATE UserQuestionMessages
        SET DiscordMessageId = (
            SELECT UserQuestions.DiscordMessageId
            FROM UserQuestions
            WHERE UserQuestions.Id = UserQuestionMessages.UserQuestionId
        )
        WHERE Id = (
            SELECT FirstMessage.Id
            FROM UserQuestionMessages AS FirstMessage
            WHERE FirstMessage.UserQuestionId = UserQuestionMessages.UserQuestionId
            ORDER BY FirstMessage.CreatedAtUtc, FirstMessage.Id
            LIMIT 1
        )
        AND EXISTS (
            SELECT 1
            FROM UserQuestions
            WHERE UserQuestions.Id = UserQuestionMessages.UserQuestionId
              AND UserQuestions.DiscordMessageId IS NOT NULL
        );
        """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscordMessageId",
                table: "UserQuestionMessages");
        }
    }
}
