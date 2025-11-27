using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ASG.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRulesAndRegistrationFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"CREATE TABLE IF NOT EXISTS ""EventRegistrationAnswers"" (
                ""Id"" uuid NOT NULL,
                ""EventId"" uuid NOT NULL,
                ""TeamId"" uuid NOT NULL,
                ""AnswersJson"" text NOT NULL,
                ""SubmittedByUserId"" text NULL,
                ""SubmittedAt"" timestamp with time zone NOT NULL,
                ""UpdatedAt"" timestamp with time zone NULL,
                CONSTRAINT ""PK_EventRegistrationAnswers"" PRIMARY KEY (""Id""),
                CONSTRAINT ""FK_EventRegistrationAnswers_Events_EventId"" FOREIGN KEY (""EventId"") REFERENCES ""Events"" (""Id"") ON DELETE CASCADE,
                CONSTRAINT ""FK_EventRegistrationAnswers_Teams_TeamId"" FOREIGN KEY (""TeamId"") REFERENCES ""Teams"" (""Id"") ON DELETE CASCADE
            );");

            migrationBuilder.Sql(@"CREATE TABLE IF NOT EXISTS ""EventRuleRevisions"" (
                ""Id"" uuid NOT NULL,
                ""EventId"" uuid NOT NULL,
                ""Version"" integer NOT NULL,
                ""ContentMarkdown"" text NOT NULL,
                ""ChangeNotes"" character varying(500) NULL,
                ""CreatedByUserId"" text NULL,
                ""CreatedAt"" timestamp with time zone NOT NULL,
                ""IsPublished"" boolean NOT NULL,
                ""PublishedAt"" timestamp with time zone NULL,
                CONSTRAINT ""PK_EventRuleRevisions"" PRIMARY KEY (""Id""),
                CONSTRAINT ""FK_EventRuleRevisions_Events_EventId"" FOREIGN KEY (""EventId"") REFERENCES ""Events"" (""Id"") ON DELETE CASCADE
            );");

            migrationBuilder.Sql(@"CREATE TABLE IF NOT EXISTS ""OperationLogs"" (
                ""Id"" uuid NOT NULL,
                ""Timestamp"" timestamp with time zone NOT NULL,
                ""Action"" character varying(64) NOT NULL,
                ""UserId"" text NULL,
                ""EntityType"" character varying(64) NULL,
                ""EntityId"" uuid NULL,
                ""Details"" text NULL,
                CONSTRAINT ""PK_OperationLogs"" PRIMARY KEY (""Id"")
            );");

            migrationBuilder.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_EventRegistrationAnswers_EventId_TeamId"" ON ""EventRegistrationAnswers"" (""EventId"", ""TeamId"");");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_EventRegistrationAnswers_TeamId"" ON ""EventRegistrationAnswers"" (""TeamId"");");
            migrationBuilder.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_EventRuleRevisions_EventId_Version"" ON ""EventRuleRevisions"" (""EventId"", ""Version"");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventRegistrationAnswers");

            migrationBuilder.DropTable(
                name: "EventRuleRevisions");

            migrationBuilder.DropTable(
                name: "OperationLogs");
        }
    }
}
