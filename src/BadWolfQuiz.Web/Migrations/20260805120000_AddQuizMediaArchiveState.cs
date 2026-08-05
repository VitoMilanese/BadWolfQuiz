using BadWolfQuiz.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BadWolfQuiz.Web.Migrations;

[DbContext(typeof(QuizDbContext))]
[Migration("20260805120000_AddQuizMediaArchiveState")]
public partial class AddQuizMediaArchiveState : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>("ArchivedMediaCount", "Quizzes", "INTEGER", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<long>("ArchivedMediaBytes", "Quizzes", "INTEGER", nullable: false, defaultValue: 0L);
        migrationBuilder.AddColumn<Guid>("CurrentArchiveOperationId", "Quizzes", "TEXT", nullable: true);
        migrationBuilder.AddColumn<DateTime>("LastPlayedAtUtc", "Quizzes", "TEXT", nullable: true);
        migrationBuilder.AddColumn<DateTime>("MediaArchivedAtUtc", "Quizzes", "TEXT", nullable: true);
        migrationBuilder.AddColumn<string>("MediaArchiveFailureReason", "Quizzes", "TEXT", maxLength: 1000, nullable: true);
        migrationBuilder.AddColumn<DateTime>("MediaRestoredAtUtc", "Quizzes", "TEXT", nullable: true);
        migrationBuilder.AddColumn<int>("MediaState", "Quizzes", "INTEGER", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<bool>("PreventAutomaticArchiving", "Quizzes", "INTEGER", nullable: false, defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("ArchivedMediaCount", "Quizzes");
        migrationBuilder.DropColumn("ArchivedMediaBytes", "Quizzes");
        migrationBuilder.DropColumn("CurrentArchiveOperationId", "Quizzes");
        migrationBuilder.DropColumn("LastPlayedAtUtc", "Quizzes");
        migrationBuilder.DropColumn("MediaArchivedAtUtc", "Quizzes");
        migrationBuilder.DropColumn("MediaArchiveFailureReason", "Quizzes");
        migrationBuilder.DropColumn("MediaRestoredAtUtc", "Quizzes");
        migrationBuilder.DropColumn("MediaState", "Quizzes");
        migrationBuilder.DropColumn("PreventAutomaticArchiving", "Quizzes");
    }
}
