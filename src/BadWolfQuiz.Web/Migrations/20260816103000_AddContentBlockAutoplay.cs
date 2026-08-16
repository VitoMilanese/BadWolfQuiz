using BadWolfQuiz.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BadWolfQuiz.Web.Migrations;

[DbContext(typeof(QuizDbContext))]
[Migration("20260816103000_AddContentBlockAutoplay")]
public partial class AddContentBlockAutoplay : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "Autoplay",
            table: "QuestionContentBlocks",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "Autoplay",
            table: "AnswerContentBlocks",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "Autoplay",
            table: "FinalQuestionContentBlocks",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "Autoplay",
            table: "FinalAnswerContentBlocks",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "Autoplay",
            table: "RoundDescriptionContentBlocks",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "Autoplay",
            table: "CategoryDescriptionContentBlocks",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Autoplay",
            table: "CategoryDescriptionContentBlocks");
        migrationBuilder.DropColumn(
            name: "Autoplay",
            table: "RoundDescriptionContentBlocks");
        migrationBuilder.DropColumn(
            name: "Autoplay",
            table: "FinalAnswerContentBlocks");
        migrationBuilder.DropColumn(
            name: "Autoplay",
            table: "FinalQuestionContentBlocks");
        migrationBuilder.DropColumn(
            name: "Autoplay",
            table: "AnswerContentBlocks");
        migrationBuilder.DropColumn(
            name: "Autoplay",
            table: "QuestionContentBlocks");
    }
}
