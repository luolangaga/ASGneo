using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ASG.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPingFeng : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE \"Users\" DROP CONSTRAINT IF EXISTS \"FK_Users_Teams_TeamId\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Users_TeamId\";");

            migrationBuilder.Sql("ALTER TABLE \"Teams\" ADD COLUMN IF NOT EXISTS \"HasDispute\" boolean NOT NULL DEFAULT false;");

            migrationBuilder.Sql(@"CREATE TABLE IF NOT EXISTS ""TeamReviews"" (
                ""Id"" uuid NOT NULL,
                ""TeamId"" uuid NOT NULL,
                ""EventId"" uuid NULL,
                ""Rating"" integer NOT NULL DEFAULT 0,
                ""CommentMarkdown"" text NULL,
                ""CreatedByUserId"" text NULL,
                ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                ""UpdatedAt"" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                ""IsDeleted"" boolean NOT NULL DEFAULT false,
                CONSTRAINT ""PK_TeamReviews"" PRIMARY KEY (""Id""),
                CONSTRAINT ""FK_TeamReviews_Events_EventId"" FOREIGN KEY (""EventId"") REFERENCES ""Events"" (""Id"") ON DELETE SET NULL,
                CONSTRAINT ""FK_TeamReviews_Teams_TeamId"" FOREIGN KEY (""TeamId"") REFERENCES ""Teams"" (""Id"") ON DELETE CASCADE
            );");

            migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Teams_OwnerId\" ON \"Teams\" (\"OwnerId\");");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_TeamReviews_EventId\" ON \"TeamReviews\" (\"EventId\");");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_TeamReviews_TeamId\" ON \"TeamReviews\" (\"TeamId\");");

            migrationBuilder.Sql(@"DO $$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM pg_constraint WHERE conname = 'FK_Teams_Users_OwnerId'
                ) THEN
                    ALTER TABLE ""Teams"" ADD CONSTRAINT ""FK_Teams_Users_OwnerId"" FOREIGN KEY (""OwnerId"") REFERENCES ""Users"" (""Id"") ON DELETE SET NULL;
                END IF;
            END $$;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Teams_Users_OwnerId",
                table: "Teams");

            migrationBuilder.DropTable(
                name: "TeamReviews");

            migrationBuilder.DropIndex(
                name: "IX_Teams_OwnerId",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "HasDispute",
                table: "Teams");

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
