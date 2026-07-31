using System;
using BadWolfQuiz.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BadWolfQuiz.Web.Migrations;

[DbContext(typeof(QuizDbContext))]
[Migration("20260731150000_AddHostAccounts")]
public sealed class AddHostAccounts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Hosts",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                Email = table.Column<string>(type: "TEXT", maxLength: 254, nullable: false),
                NormalizedEmail = table.Column<string>(type: "TEXT", maxLength: 254, nullable: false),
                PasswordHash = table.Column<string>(type: "TEXT", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Hosts", x => x.Id));

        migrationBuilder.AddColumn<string>(
            name: "HostId",
            table: "Quizzes",
            type: "TEXT",
            maxLength: 36,
            nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "HostId",
            table: "GameSessions",
            type: "TEXT",
            maxLength: 36,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Hosts_NormalizedEmail",
            table: "Hosts",
            column: "NormalizedEmail",
            unique: true);
        migrationBuilder.CreateIndex(name: "IX_Quizzes_HostId", table: "Quizzes", column: "HostId");
        migrationBuilder.CreateIndex(name: "IX_GameSessions_HostId", table: "GameSessions", column: "HostId");

        migrationBuilder.AddForeignKey(
            name: "FK_Quizzes_Hosts_HostId",
            table: "Quizzes",
            column: "HostId",
            principalTable: "Hosts",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(
            name: "FK_GameSessions_Hosts_HostId",
            table: "GameSessions",
            column: "HostId",
            principalTable: "Hosts",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey("FK_Quizzes_Hosts_HostId", "Quizzes");
        migrationBuilder.DropForeignKey("FK_GameSessions_Hosts_HostId", "GameSessions");
        migrationBuilder.DropIndex("IX_Quizzes_HostId", "Quizzes");
        migrationBuilder.DropIndex("IX_GameSessions_HostId", "GameSessions");
        migrationBuilder.DropColumn("HostId", "Quizzes");
        migrationBuilder.DropColumn("HostId", "GameSessions");
        migrationBuilder.DropTable("Hosts");
    }
}
