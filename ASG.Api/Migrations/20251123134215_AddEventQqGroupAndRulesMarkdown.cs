using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ASG.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddEventQqGroupAndRulesMarkdown : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE \"Events\" ADD COLUMN IF NOT EXISTS \"QqGroup\" character varying(50) NULL;");
            migrationBuilder.Sql("ALTER TABLE \"Events\" ADD COLUMN IF NOT EXISTS \"RulesMarkdown\" text NULL;");

            migrationBuilder.Sql(@"CREATE TABLE IF NOT EXISTS ""DeviceTokens"" (
                ""Id"" uuid NOT NULL,
                ""UserId"" text NOT NULL,
                ""Token"" text NOT NULL,
                ""Platform"" character varying(20) NOT NULL DEFAULT '',
                ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                ""LastSeenAt"" timestamp with time zone NULL,
                ""IsActive"" boolean NOT NULL DEFAULT true,
                CONSTRAINT ""PK_DeviceTokens"" PRIMARY KEY (""Id"")
            );");

            migrationBuilder.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_DeviceTokens_UserId_Token"" ON ""DeviceTokens"" (""UserId"", ""Token"");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceTokens");

            migrationBuilder.DropColumn(
                name: "QqGroup",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "RulesMarkdown",
                table: "Events");
        }
    }
}
