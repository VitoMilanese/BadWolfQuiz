using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BadWolfQuiz.Web.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Quizzes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Quizzes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GameSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    QuizId = table.Column<int>(type: "INTEGER", nullable: false),
                    PublicCode = table.Column<string>(type: "TEXT", maxLength: 12, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FinishedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameSessions_Quizzes_QuizId",
                        column: x => x.QuizId,
                        principalTable: "Quizzes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QuizRounds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    QuizId = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    DefaultTimeLimitSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    DefaultBuzzMode = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuizRounds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuizRounds_Quizzes_QuizId",
                        column: x => x.QuizId,
                        principalTable: "Quizzes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GamePlayers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameSessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    ReconnectToken = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    TotalScore = table.Column<int>(type: "INTEGER", nullable: false),
                    JoinedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSeenAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GamePlayers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GamePlayers_GameSessions_GameSessionId",
                        column: x => x.GameSessionId,
                        principalTable: "GameSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QuizCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    QuizRoundId = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuizCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuizCategories_QuizRounds_QuizRoundId",
                        column: x => x.QuizRoundId,
                        principalTable: "QuizRounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QuizRoundRows",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    QuizRoundId = table.Column<int>(type: "INTEGER", nullable: false),
                    RowIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    Points = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuizRoundRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuizRoundRows_QuizRounds_QuizRoundId",
                        column: x => x.QuizRoundId,
                        principalTable: "QuizRounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QuizQuestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    QuizCategoryId = table.Column<int>(type: "INTEGER", nullable: false),
                    RowIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    TimeLimitSecondsOverride = table.Column<int>(type: "INTEGER", nullable: true),
                    BuzzModeOverride = table.Column<int>(type: "INTEGER", nullable: false),
                    BuzzDelaySeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    IsSpecial = table.Column<bool>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuizQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuizQuestions_QuizCategories_QuizCategoryId",
                        column: x => x.QuizCategoryId,
                        principalTable: "QuizCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AnswerContentBlocks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    QuizQuestionId = table.Column<int>(type: "INTEGER", nullable: false),
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
                    table.PrimaryKey("PK_AnswerContentBlocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnswerContentBlocks_QuizQuestions_QuizQuestionId",
                        column: x => x.QuizQuestionId,
                        principalTable: "QuizQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GameQuestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameSessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    QuizQuestionId = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    BuzzOpenedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FinishedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FirstBuzzPlayerId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameQuestions_GameSessions_GameSessionId",
                        column: x => x.GameSessionId,
                        principalTable: "GameSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GameQuestions_QuizQuestions_QuizQuestionId",
                        column: x => x.QuizQuestionId,
                        principalTable: "QuizQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QuestionContentBlocks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    QuizQuestionId = table.Column<int>(type: "INTEGER", nullable: false),
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
                    table.PrimaryKey("PK_QuestionContentBlocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestionContentBlocks_QuizQuestions_QuizQuestionId",
                        column: x => x.QuizQuestionId,
                        principalTable: "QuizQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlayerBuzzes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameQuestionId = table.Column<int>(type: "INTEGER", nullable: false),
                    GamePlayerId = table.Column<int>(type: "INTEGER", nullable: false),
                    ServerReceivedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    BuzzOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    WasFirst = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerBuzzes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerBuzzes_GamePlayers_GamePlayerId",
                        column: x => x.GamePlayerId,
                        principalTable: "GamePlayers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlayerBuzzes_GameQuestions_GameQuestionId",
                        column: x => x.GameQuestionId,
                        principalTable: "GameQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlayerQuestionResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameQuestionId = table.Column<int>(type: "INTEGER", nullable: false),
                    GamePlayerId = table.Column<int>(type: "INTEGER", nullable: false),
                    IsCorrect = table.Column<bool>(type: "INTEGER", nullable: true),
                    PointsAwarded = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerQuestionResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerQuestionResults_GamePlayers_GamePlayerId",
                        column: x => x.GamePlayerId,
                        principalTable: "GamePlayers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlayerQuestionResults_GameQuestions_GameQuestionId",
                        column: x => x.GameQuestionId,
                        principalTable: "GameQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnswerContentBlocks_QuizQuestionId",
                table: "AnswerContentBlocks",
                column: "QuizQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_GamePlayers_GameSessionId_Name",
                table: "GamePlayers",
                columns: new[] { "GameSessionId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameQuestions_GameSessionId",
                table: "GameQuestions",
                column: "GameSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_GameQuestions_QuizQuestionId",
                table: "GameQuestions",
                column: "QuizQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_GameSessions_PublicCode",
                table: "GameSessions",
                column: "PublicCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameSessions_QuizId",
                table: "GameSessions",
                column: "QuizId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerBuzzes_GamePlayerId",
                table: "PlayerBuzzes",
                column: "GamePlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerBuzzes_GameQuestionId_GamePlayerId",
                table: "PlayerBuzzes",
                columns: new[] { "GameQuestionId", "GamePlayerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerQuestionResults_GamePlayerId",
                table: "PlayerQuestionResults",
                column: "GamePlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerQuestionResults_GameQuestionId_GamePlayerId",
                table: "PlayerQuestionResults",
                columns: new[] { "GameQuestionId", "GamePlayerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuestionContentBlocks_QuizQuestionId",
                table: "QuestionContentBlocks",
                column: "QuizQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_QuizCategories_QuizRoundId_SortOrder",
                table: "QuizCategories",
                columns: new[] { "QuizRoundId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuizQuestions_QuizCategoryId_RowIndex",
                table: "QuizQuestions",
                columns: new[] { "QuizCategoryId", "RowIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuizRoundRows_QuizRoundId_RowIndex",
                table: "QuizRoundRows",
                columns: new[] { "QuizRoundId", "RowIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuizRounds_QuizId_SortOrder",
                table: "QuizRounds",
                columns: new[] { "QuizId", "SortOrder" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnswerContentBlocks");

            migrationBuilder.DropTable(
                name: "PlayerBuzzes");

            migrationBuilder.DropTable(
                name: "PlayerQuestionResults");

            migrationBuilder.DropTable(
                name: "QuestionContentBlocks");

            migrationBuilder.DropTable(
                name: "QuizRoundRows");

            migrationBuilder.DropTable(
                name: "GamePlayers");

            migrationBuilder.DropTable(
                name: "GameQuestions");

            migrationBuilder.DropTable(
                name: "GameSessions");

            migrationBuilder.DropTable(
                name: "QuizQuestions");

            migrationBuilder.DropTable(
                name: "QuizCategories");

            migrationBuilder.DropTable(
                name: "QuizRounds");

            migrationBuilder.DropTable(
                name: "Quizzes");
        }
    }
}
