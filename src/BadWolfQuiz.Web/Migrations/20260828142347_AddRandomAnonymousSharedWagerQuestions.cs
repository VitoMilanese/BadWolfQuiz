using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BadWolfQuiz.Web.Migrations;

public partial class AddRandomAnonymousSharedWagerQuestions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "RandomAnonymousSharedWagerQuestionCount",
            table: "QuizRounds",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<bool>(
            name: "UseRandomAnonymousSharedWagerQuestions",
            table: "QuizRounds",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "RandomAnonymousSharedWagerQuestionCount",
            table: "QuizRounds");

        migrationBuilder.DropColumn(
            name: "UseRandomAnonymousSharedWagerQuestions",
            table: "QuizRounds");
    }
}
