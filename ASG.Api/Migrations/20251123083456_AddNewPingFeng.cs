using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ASG.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddNewPingFeng : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE \"Teams\" ADD COLUMN IF NOT EXISTS \"CommunityPostId\" uuid NULL;");
            migrationBuilder.Sql("ALTER TABLE \"Teams\" ADD COLUMN IF NOT EXISTS \"DisputeDetail\" text NULL;");
            migrationBuilder.Sql("ALTER TABLE \"TeamReviews\" ADD COLUMN IF NOT EXISTS \"CommunityPostId\" uuid NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CommunityPostId",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "DisputeDetail",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "CommunityPostId",
                table: "TeamReviews");
        }
    }
}
