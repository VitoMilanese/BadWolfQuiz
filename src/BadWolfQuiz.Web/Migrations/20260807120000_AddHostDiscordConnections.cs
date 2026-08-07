using BadWolfQuiz.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BadWolfQuiz.Web.Migrations;

[DbContext(typeof(QuizDbContext))]
[Migration("20260807120000_AddHostDiscordConnections")]
public partial class AddHostDiscordConnections : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "HostDiscordConnections",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                HostId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                DiscordUserId = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                DiscordUserName = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                GuildId = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                GuildName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                VoiceChannelId = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                VoiceChannelName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                AutoMuteDuringMedia = table.Column<bool>(type: "INTEGER", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_HostDiscordConnections", x => x.Id);
                table.ForeignKey(
                    name: "FK_HostDiscordConnections_Hosts_HostId",
                    column: x => x.HostId,
                    principalTable: "Hosts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_HostDiscordConnections_HostId",
            table: "HostDiscordConnections",
            column: "HostId",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "HostDiscordConnections");
}
