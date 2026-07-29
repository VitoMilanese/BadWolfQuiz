using BadWolfQuiz.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BadWolfQuiz.Web.Migrations;

[DbContext(typeof(QuizDbContext))]
[Migration("20260729214500_AddRandomWagerQuestions")]
public partial class AddRandomWagerQuestions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "ExcludeFromRandomWagerSelection",
            table: "QuizQuestions",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<int>(
            name: "RandomWagerQuestionCount",
            table: "QuizRounds",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<bool>(
            name: "UseRandomWagerQuestions",
            table: "QuizRounds",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ExcludeFromRandomWagerSelection",
            table: "QuizQuestions");

        migrationBuilder.DropColumn(
            name: "RandomWagerQuestionCount",
            table: "QuizRounds");

        migrationBuilder.DropColumn(
            name: "UseRandomWagerQuestions",
            table: "QuizRounds");
    }
}
