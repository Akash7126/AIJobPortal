using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobPlatform.ExternalIntegration.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "extint");

            migrationBuilder.EnsureSchema(
                name: "messaging");

            migrationBuilder.CreateTable(
                name: "ApiSchemaAccessLogs",
                schema: "extint",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApiVersion = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ViewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiSchemaAccessLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApiVersions",
                schema: "extint",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    DeprecatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SunsetAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AcceptedFormatsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiVersions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IdempotencyKeys",
                schema: "messaging",
                columns: table => new
                {
                    Scope = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Key = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Completed = table.Column<bool>(type: "bit", nullable: false),
                    IsSuccess = table.Column<bool>(type: "bit", nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyKeys", x => new { x.Scope, x.Key });
                });

            migrationBuilder.CreateTable(
                name: "InboxMessages",
                schema: "messaging",
                columns: table => new
                {
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConsumerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReceivedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    NextAttemptUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxMessages", x => new { x.MessageId, x.ConsumerName });
                });

            migrationBuilder.CreateTable(
                name: "Integrations",
                schema: "extint",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartnerAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourcePlatformId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourcePlatformName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BaseUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Recommendation = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    AdmissionStatus = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ApprovalBasis = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ApprovedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PullEnabled = table.Column<bool>(type: "bit", nullable: false),
                    PushEnabled = table.Column<bool>(type: "bit", nullable: false),
                    SyncMode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    SyncCron = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AttributionVisibility = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Sandbox = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SandboxProvisionedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    MappingId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Integrations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JobData",
                schema: "extint",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IntegrationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourcePlatformId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceJobId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PlatformJobId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    StandardizedJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RawPayload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SyncRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobData", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JobDataMappings",
                schema: "extint",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IntegrationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MappingVersion = table.Column<int>(type: "int", nullable: false),
                    StandardSchemaVersion = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    RulesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobDataMappings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JobPostAttributions",
                schema: "extint",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobDataId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlatformJobId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SourcePlatformName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Backlink = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SyncState = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    DeadlineUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobPostAttributions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KnownPartnerAccounts",
                schema: "extint",
                columns: table => new
                {
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActiveSinceUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnownPartnerAccounts", x => x.AccountId);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                schema: "messaging",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Exchange = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RoutingKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Headers = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AggregateId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false),
                    OccurredOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    NextAttemptUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PartnerCredentials",
                schema: "extint",
                columns: table => new
                {
                    ApiCredentialId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastEventVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartnerCredentials", x => x.ApiCredentialId);
                });

            migrationBuilder.CreateTable(
                name: "SoftwareInterfaces",
                schema: "extint",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Endpoint = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SoftwareInterfaces", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SyncRuns",
                schema: "extint",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Trigger = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MappingVersion = table.Column<int>(type: "int", nullable: false),
                    Received = table.Column<int>(type: "int", nullable: false),
                    Accepted = table.Column<int>(type: "int", nullable: false),
                    Rejected = table.Column<int>(type: "int", nullable: false),
                    ErrorCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ExternalJobSiteIntegrationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SyncRuns_Integrations_ExternalJobSiteIntegrationId",
                        column: x => x.ExternalJobSiteIntegrationId,
                        principalSchema: "extint",
                        principalTable: "Integrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApiSchemaAccessLogs_ViewedAt",
                schema: "extint",
                table: "ApiSchemaAccessLogs",
                column: "ViewedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyKeys_ExpiresAtUtc",
                schema: "messaging",
                table: "IdempotencyKeys",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_Status_NextAttemptUtc",
                schema: "messaging",
                table: "InboxMessages",
                columns: new[] { "Status", "NextAttemptUtc" });

            migrationBuilder.CreateIndex(
                name: "UQ_Integrations_PartnerAccountId",
                schema: "extint",
                table: "Integrations",
                column: "PartnerAccountId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Integrations_SourcePlatformId",
                schema: "extint",
                table: "Integrations",
                column: "SourcePlatformId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobData_PlatformJobId",
                schema: "extint",
                table: "JobData",
                column: "PlatformJobId");

            migrationBuilder.CreateIndex(
                name: "UQ_JobData_SourcePlatform_SourceJob",
                schema: "extint",
                table: "JobData",
                columns: new[] { "SourcePlatformId", "SourceJobId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_JobDataMappings_IntegrationId",
                schema: "extint",
                table: "JobDataMappings",
                column: "IntegrationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_JobPostAttributions_JobDataId",
                schema: "extint",
                table: "JobPostAttributions",
                column: "JobDataId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_JobPostAttributions_PlatformJobId",
                schema: "extint",
                table: "JobPostAttributions",
                column: "PlatformJobId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Status_NextAttemptUtc",
                schema: "messaging",
                table: "OutboxMessages",
                columns: new[] { "Status", "NextAttemptUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PartnerCredentials_AccountId",
                schema: "extint",
                table: "PartnerCredentials",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "UQ_SoftwareInterfaces_Category_Name",
                schema: "extint",
                table: "SoftwareInterfaces",
                columns: new[] { "Category", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SyncRuns_Integration_StartedAt",
                schema: "extint",
                table: "SyncRuns",
                columns: new[] { "ExternalJobSiteIntegrationId", "StartedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiSchemaAccessLogs",
                schema: "extint");

            migrationBuilder.DropTable(
                name: "ApiVersions",
                schema: "extint");

            migrationBuilder.DropTable(
                name: "IdempotencyKeys",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "InboxMessages",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "JobData",
                schema: "extint");

            migrationBuilder.DropTable(
                name: "JobDataMappings",
                schema: "extint");

            migrationBuilder.DropTable(
                name: "JobPostAttributions",
                schema: "extint");

            migrationBuilder.DropTable(
                name: "KnownPartnerAccounts",
                schema: "extint");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "PartnerCredentials",
                schema: "extint");

            migrationBuilder.DropTable(
                name: "SoftwareInterfaces",
                schema: "extint");

            migrationBuilder.DropTable(
                name: "SyncRuns",
                schema: "extint");

            migrationBuilder.DropTable(
                name: "Integrations",
                schema: "extint");
        }
    }
}
