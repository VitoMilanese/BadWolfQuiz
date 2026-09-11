using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BadWolfQuiz.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddAchievementHistoryEligibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CountsForAchievementHistory",
                table: "GamePlayers",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CountsForAchievementHistory",
                table: "GamePlayers");
        }
    }
}
