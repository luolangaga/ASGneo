﻿using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ASG.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDisplayTeamIdToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE \"Users\" DROP CONSTRAINT IF EXISTS \"FK_Users_Teams_TeamId\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Users_TeamId\";");
            migrationBuilder.Sql("ALTER TABLE \"Users\" ADD COLUMN IF NOT EXISTS \"DisplayTeamId\" uuid NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_OwnerId",
                table: "Teams",
                column: "OwnerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Teams_Users_OwnerId",
                table: "Teams",
                column: "OwnerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Teams_Users_OwnerId",
                table: "Teams");

            migrationBuilder.DropIndex(
                name: "IX_Teams_OwnerId",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "DisplayTeamId",
                table: "Users");

            migrationBuilder.CreateIndex(
                name: "IX_Users_TeamId",
                table: "Users",
                column: "TeamId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Teams_TeamId",
                table: "Users",
                column: "TeamId",
                principalTable: "Teams",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
