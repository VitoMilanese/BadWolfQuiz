using BadWolfQuiz.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BadWolfQuiz.Web.Migrations;

[DbContext(typeof(QuizDbContext))]
[Migration("20260911210000_AddHostCustomAchievements")]
public sealed class AddHostCustomAchievements : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "HostCustomAchievements",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                HostId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                Target = table.Column<int>(type: "INTEGER", nullable: false),
                ArtworkPng = table.Column<byte[]>(type: "BLOB", nullable: false),
                IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_HostCustomAchievements", x => x.Id);
                table.ForeignKey(
                    name: "FK_HostCustomAchievements_Hosts_HostId",
                    column: x => x.HostId,
                    principalTable: "Hosts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "HostCustomAchievementTags",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                HostCustomAchievementId = table.Column<Guid>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                NormalizedName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_HostCustomAchievementTags", x => x.Id);
                table.ForeignKey(
                    name: "FK_HostCustomAchievementTags_HostCustomAchievements_HostCustomAchievementId",
                    column: x => x.HostCustomAchievementId,
                    principalTable: "HostCustomAchievements",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_HostCustomAchievements_HostId_IsDeleted",
            table: "HostCustomAchievements",
            columns: new[] { "HostId", "IsDeleted" });
        migrationBuilder.CreateIndex(
            name: "IX_HostCustomAchievementTags_HostCustomAchievementId_NormalizedName",
            table: "HostCustomAchievementTags",
            columns: new[] { "HostCustomAchievementId", "NormalizedName" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "HostCustomAchievementTags");
        migrationBuilder.DropTable(name: "HostCustomAchievements");
    }
}
