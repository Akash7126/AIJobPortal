using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobPlatform.AuditLogging.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "audit");

            migrationBuilder.EnsureSchema(
                name: "messaging");

            migrationBuilder.CreateTable(
                name: "AuditEntries",
                schema: "audit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceBc = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SourceMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    SubjectType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SubjectId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    OwnerType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    DetailsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RetainUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    ArchivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CandidateInsights",
                schema: "audit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobPostingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CandidateProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Availability = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ExpectedSalary = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    FitScore = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: true),
                    WithheldFields = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ComputedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateInsights", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmployerDashboards",
                schema: "audit",
                columns: table => new
                {
                    EmployerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RegistrationApproved = table.Column<bool>(type: "bit", nullable: false),
                    ShortlistCount = table.Column<int>(type: "int", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployerDashboards", x => x.EmployerId);
                });

            migrationBuilder.CreateTable(
                name: "ExportJobs",
                schema: "audit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ParametersJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ParametersHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Format = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ResultRef = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExportJobs", x => x.Id);
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
                name: "IntegrationUsageDaily",
                schema: "audit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    Submitted = table.Column<int>(type: "int", nullable: false),
                    Matched = table.Column<int>(type: "int", nullable: false),
                    Viewed = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationUsageDaily", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JobStatusHistory",
                schema: "audit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobPostingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    ToStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobStatusHistory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationLog",
                schema: "audit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipientId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Channel = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    MaskedRecipient = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationLog", x => x.Id);
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
                name: "SyncJobStatuses",
                schema: "audit",
                columns: table => new
                {
                    PlatformJobId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SourcePlatformId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastEventVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncJobStatuses", x => x.PlatformJobId);
                });

            migrationBuilder.CreateTable(
                name: "EmployerDashboardPostings",
                schema: "audit",
                columns: table => new
                {
                    JobPostingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployerDashboardPostings", x => new { x.EmployerId, x.JobPostingId });
                    table.ForeignKey(
                        name: "FK_EmployerDashboardPostings_EmployerDashboards_EmployerId",
                        column: x => x.EmployerId,
                        principalSchema: "audit",
                        principalTable: "EmployerDashboards",
                        principalColumn: "EmployerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_Category_Owner_OccurredAt",
                schema: "audit",
                table: "AuditEntries",
                columns: new[] { "Category", "OwnerId", "OccurredAtUtc" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_Retention",
                schema: "audit",
                table: "AuditEntries",
                columns: new[] { "IsArchived", "RetainUntilUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_Subject",
                schema: "audit",
                table: "AuditEntries",
                columns: new[] { "SubjectType", "SubjectId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UQ_AuditEntries_Source_Category",
                schema: "audit",
                table: "AuditEntries",
                columns: new[] { "SourceBc", "SourceMessageId", "Category" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CandidateInsights_Job_Candidate",
                schema: "audit",
                table: "CandidateInsights",
                columns: new[] { "JobPostingId", "CandidateProfileId" });

            migrationBuilder.CreateIndex(
                name: "UQ_ExportJobs_InProgress",
                schema: "audit",
                table: "ExportJobs",
                columns: new[] { "RequestedBy", "ReportType", "ParametersHash" },
                unique: true,
                filter: "[Status] IN ('Queued','Generating')");

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
                name: "UQ_IntegrationUsageDaily_Partner_Day",
                schema: "audit",
                table: "IntegrationUsageDaily",
                columns: new[] { "PartnerId", "Day" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobStatusHistory_Job_At",
                schema: "audit",
                table: "JobStatusHistory",
                columns: new[] { "JobPostingId", "ChangedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UQ_JobStatusHistory_SourceMessage",
                schema: "audit",
                table: "JobStatusHistory",
                column: "SourceMessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationLog_Channel_SentAt",
                schema: "audit",
                table: "NotificationLog",
                columns: new[] { "Channel", "SentAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationLog_Recipient_SentAt",
                schema: "audit",
                table: "NotificationLog",
                columns: new[] { "RecipientId", "SentAtUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Status_NextAttemptUtc",
                schema: "messaging",
                table: "OutboxMessages",
                columns: new[] { "Status", "NextAttemptUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SyncJobStatuses_Owner_Status",
                schema: "audit",
                table: "SyncJobStatuses",
                columns: new[] { "OwnerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SyncJobStatuses_Platform_Status",
                schema: "audit",
                table: "SyncJobStatuses",
                columns: new[] { "SourcePlatformId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditEntries",
                schema: "audit");

            migrationBuilder.DropTable(
                name: "CandidateInsights",
                schema: "audit");

            migrationBuilder.DropTable(
                name: "EmployerDashboardPostings",
                schema: "audit");

            migrationBuilder.DropTable(
                name: "ExportJobs",
                schema: "audit");

            migrationBuilder.DropTable(
                name: "IdempotencyKeys",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "InboxMessages",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "IntegrationUsageDaily",
                schema: "audit");

            migrationBuilder.DropTable(
                name: "JobStatusHistory",
                schema: "audit");

            migrationBuilder.DropTable(
                name: "NotificationLog",
                schema: "audit");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "SyncJobStatuses",
                schema: "audit");

            migrationBuilder.DropTable(
                name: "EmployerDashboards",
                schema: "audit");
        }
    }
}
