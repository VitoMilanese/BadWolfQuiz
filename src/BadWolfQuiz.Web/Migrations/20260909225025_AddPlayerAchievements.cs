using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BadWolfQuiz.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerAchievements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlayerAchievements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: true),
                    HostId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: true),
                    PlayerKey = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    AchievementCode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    UnlockedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SourceGameSessionId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerAchievements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlayerGameAccountLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GamePlayerId = table.Column<int>(type: "INTEGER", nullable: false),
                    AccountId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerGameAccountLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerGameAccountLinks_GamePlayers_GamePlayerId",
                        column: x => x.GamePlayerId,
                        principalTable: "GamePlayers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserQuestionAccountLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserQuestionId = table.Column<int>(type: "INTEGER", nullable: false),
                    AccountId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserQuestionAccountLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserQuestionAccountLinks_UserQuestions_UserQuestionId",
                        column: x => x.UserQuestionId,
                        principalTable: "UserQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerAchievements_AccountId_AchievementCode",
                table: "PlayerAchievements",
                columns: new[] { "AccountId", "AchievementCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerAchievements_HostId_PlayerKey_AchievementCode",
                table: "PlayerAchievements",
                columns: new[] { "HostId", "PlayerKey", "AchievementCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerAchievements_SourceGameSessionId",
                table: "PlayerAchievements",
                column: "SourceGameSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerGameAccountLinks_AccountId",
                table: "PlayerGameAccountLinks",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerGameAccountLinks_GamePlayerId",
                table: "PlayerGameAccountLinks",
                column: "GamePlayerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserQuestionAccountLinks_AccountId",
                table: "UserQuestionAccountLinks",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_UserQuestionAccountLinks_UserQuestionId",
                table: "UserQuestionAccountLinks",
                column: "UserQuestionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerAchievements");

            migrationBuilder.DropTable(
                name: "PlayerGameAccountLinks");

            migrationBuilder.DropTable(
                name: "UserQuestionAccountLinks");
        }
    }
}
