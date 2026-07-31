using System;
using BadWolfQuiz.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BadWolfQuiz.Web.Migrations;

[DbContext(typeof(QuizDbContext))]
[Migration("20260731170000_AddPasswordResetTokens")]
public sealed class AddPasswordResetTokens : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "PasswordResetTokenExpiresAtUtc",
            table: "Hosts",
            type: "TEXT",
            nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "PasswordResetTokenHash",
            table: "Hosts",
            type: "TEXT",
            maxLength: 64,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("PasswordResetTokenExpiresAtUtc", "Hosts");
        migrationBuilder.DropColumn("PasswordResetTokenHash", "Hosts");
    }
}
