using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobPlatform.JobPosting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "jobs");

            migrationBuilder.EnsureSchema(
                name: "messaging");

            migrationBuilder.CreateTable(
                name: "FavoriteJobLists",
                schema: "jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobPostingIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FavoriteJobLists", x => x.Id);
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
                name: "InterestedListEntries",
                schema: "jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferenceJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterestedListEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JobPostings",
                schema: "jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployerAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SourcePlatformId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourcePlatformJobId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SourceName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SourceBacklinkUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SourceAttributionPublic = table.Column<bool>(type: "bit", nullable: false),
                    TitleAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TitleEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SummaryAr = table.Column<string>(type: "nvarchar(max)", maxLength: 5000, nullable: false),
                    SummaryEn = table.Column<string>(type: "nvarchar(max)", maxLength: 5000, nullable: false),
                    SkillsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CategoryCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ContractType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EducationLevel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    RequiredTrainingJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WorkFormat = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Governorate = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SalaryMin = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    SalaryMax = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    SalaryCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    MinExperienceYears = table.Column<int>(type: "int", nullable: true),
                    MaxExperienceYears = table.Column<int>(type: "int", nullable: true),
                    RequiredLanguagesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeadlineDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AutoClose = table.Column<bool>(type: "bit", nullable: false),
                    JobLink = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    OtherFieldsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VisibilityScope = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    VisibilityTargetJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AdminSuspended = table.Column<bool>(type: "bit", nullable: false),
                    SuspendReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TaxonomyVersion = table.Column<int>(type: "int", nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobPostings", x => x.Id);
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
                name: "SavedSearches",
                schema: "jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriteriaJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CriteriaHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    NotifyOnMatch = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastEvaluatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedSearches", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UQ_FavoriteJobLists_Owner",
                schema: "jobs",
                table: "FavoriteJobLists",
                column: "OwnerAccountId",
                unique: true);

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
                name: "IX_InterestedListEntries_Owner",
                schema: "jobs",
                table: "InterestedListEntries",
                column: "OwnerAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_JobPostings_Category",
                schema: "jobs",
                table: "JobPostings",
                column: "CategoryCode");

            migrationBuilder.CreateIndex(
                name: "IX_JobPostings_Employer_ContentHash",
                schema: "jobs",
                table: "JobPostings",
                columns: new[] { "EmployerAccountId", "ContentHash" });

            migrationBuilder.CreateIndex(
                name: "IX_JobPostings_Source_PlatformJobId",
                schema: "jobs",
                table: "JobPostings",
                column: "SourcePlatformJobId");

            migrationBuilder.CreateIndex(
                name: "IX_JobPostings_Status",
                schema: "jobs",
                table: "JobPostings",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Status_NextAttemptUtc",
                schema: "messaging",
                table: "OutboxMessages",
                columns: new[] { "Status", "NextAttemptUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SavedSearches_NotifyOnMatch",
                schema: "jobs",
                table: "SavedSearches",
                column: "NotifyOnMatch");

            migrationBuilder.CreateIndex(
                name: "UQ_SavedSearches_Owner_Hash",
                schema: "jobs",
                table: "SavedSearches",
                columns: new[] { "OwnerAccountId", "CriteriaHash" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FavoriteJobLists",
                schema: "jobs");

            migrationBuilder.DropTable(
                name: "IdempotencyKeys",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "InboxMessages",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "InterestedListEntries",
                schema: "jobs");

            migrationBuilder.DropTable(
                name: "JobPostings",
                schema: "jobs");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "SavedSearches",
                schema: "jobs");
        }
    }
}
