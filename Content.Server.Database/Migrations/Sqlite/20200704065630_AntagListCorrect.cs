﻿using Microsoft.EntityFrameworkCore.Migrations;

namespace Content.Server.Database.Migrations.Sqlite
{
    public partial class AntagListCorrect : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Antag",
                columns: table => new
                {
                    AntagId = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProfileHumanoidProfileId = table.Column<int>(nullable: false),
                    AntagName = table.Column<string>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Antag", x => x.AntagId);
                    table.ForeignKey(
                        name: "FK_Antag_HumanoidProfile_ProfileHumanoidProfileId",
                        column: x => x.ProfileHumanoidProfileId,
                        principalTable: "HumanoidProfile",
                        principalColumn: "HumanoidProfileId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Antag_ProfileHumanoidProfileId",
                table: "Antag",
                column: "ProfileHumanoidProfileId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Antag");
        }
    }
}
