using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobPlatform.Reporting.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "reporting");

            migrationBuilder.EnsureSchema(
                name: "messaging");

            migrationBuilder.EnsureSchema(
                name: "config");

            migrationBuilder.CreateTable(
                name: "AggDaily",
                schema: "reporting",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    Metric = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Count = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AggDaily", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FactEvent",
                schema: "reporting",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceBc = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ActivityType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActorType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ActorKey = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SubjectId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FactEvent", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FactJobPosting",
                schema: "reporting",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Location = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SalaryMin = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    SalaryMax = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Source = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    HasDetails = table.Column<bool>(type: "bit", nullable: false),
                    FirstSeenAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StatusChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FactJobPosting", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FactMatch",
                schema: "reporting",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    JobPostingId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Score = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: true),
                    ConfigVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ItemCount = table.Column<int>(type: "int", nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FactMatch", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FactNotification",
                schema: "reporting",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FactNotification", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FactOutcome",
                schema: "reporting",
                columns: table => new
                {
                    JobPostingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Shortlisted = table.Column<int>(type: "int", nullable: false),
                    FollowUps = table.Column<int>(type: "int", nullable: false),
                    FitSum = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    FitCount = table.Column<int>(type: "int", nullable: false),
                    FirstShortlistedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FactOutcome", x => x.JobPostingId);
                });

            migrationBuilder.CreateTable(
                name: "FactRegistration",
                schema: "reporting",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Milestone = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ActorType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Governorate = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FactRegistration", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FactSkillDemand",
                schema: "reporting",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Skill = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Side = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    SubjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FactSkillDemand", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FactSystemMetric",
                schema: "reporting",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Metric = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    SampledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FactSystemMetric", x => x.Id);
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
                name: "LaborMarketReports",
                schema: "config",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Period = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    ContentJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GeneratedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    GeneratedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RetainUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LaborMarketReports", x => x.Id);
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
                name: "PerformanceAlertRules",
                schema: "config",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Metric = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Comparator = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Threshold = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    WindowMinutes = table.Column<int>(type: "int", nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerformanceAlertRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PerformanceAlerts",
                schema: "config",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Metric = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Threshold = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    RaisedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RetainUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerformanceAlerts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReportAccessDecisions",
                schema: "config",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Allowed = table.Column<bool>(type: "bit", nullable: false),
                    Request = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportAccessDecisions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReportAccessRules",
                schema: "config",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CategoriesCsv = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportAccessRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReportExportFiles",
                schema: "config",
                columns: table => new
                {
                    ExportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Content = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportExportFiles", x => x.ExportId);
                });

            migrationBuilder.CreateTable(
                name: "ReportExports",
                schema: "config",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RefKind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    RefId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ParametersJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ParametersHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Format = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ResultRef = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportExports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReportSchedules",
                schema: "config",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SavedReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Interval = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CronText = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RecipientsCsv = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Format = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    NextRunAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastRunAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportSchedules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReportTemplates",
                schema: "config",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DataSource = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ParametersJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RetentionPolicy",
                schema: "config",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RetentionMonths = table.Column<int>(type: "int", nullable: false),
                    LegalMinimumMonths = table.Column<int>(type: "int", nullable: false),
                    ChangedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetentionPolicy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SavedReports",
                schema: "config",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DefinitionJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RetainUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    ArchivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedReports", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UQ_AggDaily_Day_Metric",
                schema: "reporting",
                table: "AggDaily",
                columns: new[] { "Day", "Metric" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FactEvent_OccurredAt_Activity",
                schema: "reporting",
                table: "FactEvent",
                columns: new[] { "OccurredAtUtc", "ActivityType" });

            migrationBuilder.CreateIndex(
                name: "UQ_FactEvent_MessageId",
                schema: "reporting",
                table: "FactEvent",
                column: "MessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FactJobPosting_Category_Location",
                schema: "reporting",
                table: "FactJobPosting",
                columns: new[] { "Category", "Location" });

            migrationBuilder.CreateIndex(
                name: "IX_FactJobPosting_FirstSeen",
                schema: "reporting",
                table: "FactJobPosting",
                column: "FirstSeenAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_FactMatch_OccurredAt",
                schema: "reporting",
                table: "FactMatch",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_FactNotification_OccurredAt",
                schema: "reporting",
                table: "FactNotification",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_FactOutcome_FirstShortlisted",
                schema: "reporting",
                table: "FactOutcome",
                column: "FirstShortlistedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_FactRegistration_OccurredAt_Milestone",
                schema: "reporting",
                table: "FactRegistration",
                columns: new[] { "OccurredAtUtc", "Milestone" });

            migrationBuilder.CreateIndex(
                name: "UQ_FactRegistration_MessageId",
                schema: "reporting",
                table: "FactRegistration",
                column: "MessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FactSkillDemand_OccurredAt",
                schema: "reporting",
                table: "FactSkillDemand",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "UQ_FactSkillDemand_Skill_Side_Subject",
                schema: "reporting",
                table: "FactSkillDemand",
                columns: new[] { "Skill", "Side", "SubjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FactSystemMetric_Metric_SampledAt",
                schema: "reporting",
                table: "FactSystemMetric",
                columns: new[] { "Metric", "SampledAtUtc" });

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
                name: "UQ_LaborMarketReports_Period",
                schema: "config",
                table: "LaborMarketReports",
                column: "Period",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Status_NextAttemptUtc",
                schema: "messaging",
                table: "OutboxMessages",
                columns: new[] { "Status", "NextAttemptUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceAlerts_Retention",
                schema: "config",
                table: "PerformanceAlerts",
                column: "RetainUntilUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceAlerts_Rule_RaisedAt",
                schema: "config",
                table: "PerformanceAlerts",
                columns: new[] { "RuleId", "RaisedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ReportAccessDecisions_DecidedAt",
                schema: "config",
                table: "ReportAccessDecisions",
                column: "DecidedAtUtc");

            migrationBuilder.CreateIndex(
                name: "UQ_ReportAccessRules_Role",
                schema: "config",
                table: "ReportAccessRules",
                column: "Role",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_ReportExports_Running",
                schema: "config",
                table: "ReportExports",
                columns: new[] { "RequestedBy", "ParametersHash" },
                unique: true,
                filter: "[Status] IN ('Queued','Generating')");

            migrationBuilder.CreateIndex(
                name: "IX_ReportSchedules_Active_NextRun",
                schema: "config",
                table: "ReportSchedules",
                columns: new[] { "IsActive", "NextRunAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SavedReports_Owner_Created",
                schema: "config",
                table: "SavedReports",
                columns: new[] { "OwnerId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SavedReports_Retention",
                schema: "config",
                table: "SavedReports",
                columns: new[] { "IsArchived", "RetainUntilUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AggDaily",
                schema: "reporting");

            migrationBuilder.DropTable(
                name: "FactEvent",
                schema: "reporting");

            migrationBuilder.DropTable(
                name: "FactJobPosting",
                schema: "reporting");

            migrationBuilder.DropTable(
                name: "FactMatch",
                schema: "reporting");

            migrationBuilder.DropTable(
                name: "FactNotification",
                schema: "reporting");

            migrationBuilder.DropTable(
                name: "FactOutcome",
                schema: "reporting");

            migrationBuilder.DropTable(
                name: "FactRegistration",
                schema: "reporting");

            migrationBuilder.DropTable(
                name: "FactSkillDemand",
                schema: "reporting");

            migrationBuilder.DropTable(
                name: "FactSystemMetric",
                schema: "reporting");

            migrationBuilder.DropTable(
                name: "IdempotencyKeys",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "InboxMessages",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "LaborMarketReports",
                schema: "config");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "PerformanceAlertRules",
                schema: "config");

            migrationBuilder.DropTable(
                name: "PerformanceAlerts",
                schema: "config");

            migrationBuilder.DropTable(
                name: "ReportAccessDecisions",
                schema: "config");

            migrationBuilder.DropTable(
                name: "ReportAccessRules",
                schema: "config");

            migrationBuilder.DropTable(
                name: "ReportExportFiles",
                schema: "config");

            migrationBuilder.DropTable(
                name: "ReportExports",
                schema: "config");

            migrationBuilder.DropTable(
                name: "ReportSchedules",
                schema: "config");

            migrationBuilder.DropTable(
                name: "ReportTemplates",
                schema: "config");

            migrationBuilder.DropTable(
                name: "RetentionPolicy",
                schema: "config");

            migrationBuilder.DropTable(
                name: "SavedReports",
                schema: "config");
        }
    }
}
