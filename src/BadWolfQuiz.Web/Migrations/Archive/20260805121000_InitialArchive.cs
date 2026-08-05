using BadWolfQuiz.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BadWolfQuiz.Web.Migrations.Archive;

[DbContext(typeof(ArchiveDbContext))]
[Migration("20260805121000_InitialArchive")]
public partial class InitialArchive : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ArchivedQuizMedia",
            columns: table => new
            {
                Id = table.Column<long>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                OperationId = table.Column<Guid>(type: "TEXT", nullable: false),
                QuizId = table.Column<int>(type: "INTEGER", nullable: false),
                HostId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                EntityId = table.Column<int>(type: "INTEGER", nullable: false),
                Role = table.Column<int>(type: "INTEGER", nullable: false),
                ContentType = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                OriginalFileName = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                Data = table.Column<byte[]>(type: "BLOB", nullable: false),
                Length = table.Column<long>(type: "INTEGER", nullable: false),
                Sha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                ArchivedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
            }, constraints: table => table.PrimaryKey("PK_ArchivedQuizMedia", x => x.Id));
        migrationBuilder.CreateTable(
            name: "ArchiveOperations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false), QuizId = table.Column<int>(type: "INTEGER", nullable: false),
                HostId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false), State = table.Column<int>(type: "INTEGER", nullable: false),
                MediaCount = table.Column<int>(type: "INTEGER", nullable: false), MediaBytes = table.Column<long>(type: "INTEGER", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false), CompletedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                RestoredAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true), OrphanedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                FailureReason = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
            }, constraints: table => table.PrimaryKey("PK_ArchiveOperations", x => x.Id));
        migrationBuilder.CreateIndex("IX_ArchivedQuizMedia_HostId", "ArchivedQuizMedia", "HostId");
        migrationBuilder.CreateIndex("IX_ArchivedQuizMedia_OperationId", "ArchivedQuizMedia", "OperationId");
        migrationBuilder.CreateIndex("IX_ArchivedQuizMedia_QuizId", "ArchivedQuizMedia", "QuizId");
        migrationBuilder.CreateIndex("IX_ArchivedQuizMedia_QuizId_HostId", "ArchivedQuizMedia", new[] { "QuizId", "HostId" });
        migrationBuilder.CreateIndex("IX_ArchivedQuizMedia_OperationId_EntityId_Role", "ArchivedQuizMedia", new[] { "OperationId", "EntityId", "Role" }, unique: true);
        migrationBuilder.CreateIndex("IX_ArchiveOperations_QuizId_HostId", "ArchiveOperations", new[] { "QuizId", "HostId" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("ArchivedQuizMedia");
        migrationBuilder.DropTable("ArchiveOperations");
    }
}
