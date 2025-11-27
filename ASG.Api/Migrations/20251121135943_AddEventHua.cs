using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ASG.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddEventHua : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomData",
                table: "Events",
                type: "text",
                nullable: false,
                defaultValue: "{}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomData",
                table: "Events");
        }
    }
}
