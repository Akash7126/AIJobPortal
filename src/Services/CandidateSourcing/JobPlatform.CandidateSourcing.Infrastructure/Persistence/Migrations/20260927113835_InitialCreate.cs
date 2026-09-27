using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobPlatform.CandidateSourcing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "sourcing");

            migrationBuilder.EnsureSchema(
                name: "messaging");

            migrationBuilder.CreateTable(
                name: "CandidateInsights",
                schema: "sourcing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployerAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobPostingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CandidateProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Availability = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    ExpectedSalaryMin = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ExpectedSalaryMax = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    FitJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WithheldFieldsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ComputedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateInsights", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CandidateProjections",
                schema: "sourcing",
                columns: table => new
                {
                    ProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Visibility = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    EmployerVisibilityOptIn = table.Column<bool>(type: "bit", nullable: false),
                    Deactivated = table.Column<bool>(type: "bit", nullable: false),
                    SkillsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EducationLevel = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    YearsOfExperience = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    LocationCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    SalaryMin = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    SalaryMax = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Availability = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    LastEventVersion = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateProjections", x => x.ProfileId);
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
                name: "QualificationThresholds",
                schema: "sourcing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployerAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobPostingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Percent = table.Column<int>(type: "int", nullable: false),
                    ThresholdVersion = table.Column<int>(type: "int", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QualificationThresholds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TalentPoolEntries",
                schema: "sourcing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployerAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CandidateProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobPostingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AddedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Removed = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TalentPoolEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VerifiedEmployers",
                schema: "sourcing",
                columns: table => new
                {
                    EmployerAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VerifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VerifiedEmployers", x => x.EmployerAccountId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CandidateInsights_Posting_Candidate",
                schema: "sourcing",
                table: "CandidateInsights",
                columns: new[] { "JobPostingId", "CandidateProfileId" });

            migrationBuilder.CreateIndex(
                name: "IX_CandidateProjections_Visibility_Deactivated",
                schema: "sourcing",
                table: "CandidateProjections",
                columns: new[] { "Visibility", "Deactivated" });

            migrationBuilder.CreateIndex(
                name: "UQ_CandidateProjections_OwnerAccount",
                schema: "sourcing",
                table: "CandidateProjections",
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
                name: "IX_OutboxMessages_Status_NextAttemptUtc",
                schema: "messaging",
                table: "OutboxMessages",
                columns: new[] { "Status", "NextAttemptUtc" });

            migrationBuilder.CreateIndex(
                name: "UQ_QualificationThresholds_Employer_Posting",
                schema: "sourcing",
                table: "QualificationThresholds",
                columns: new[] { "EmployerAccountId", "JobPostingId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TalentPoolEntries_Employer",
                schema: "sourcing",
                table: "TalentPoolEntries",
                column: "EmployerAccountId");

            migrationBuilder.CreateIndex(
                name: "UQ_TalentPoolEntries_Employer_Candidate_Posting_Removed",
                schema: "sourcing",
                table: "TalentPoolEntries",
                columns: new[] { "EmployerAccountId", "CandidateProfileId", "JobPostingId", "Removed" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CandidateInsights",
                schema: "sourcing");

            migrationBuilder.DropTable(
                name: "CandidateProjections",
                schema: "sourcing");

            migrationBuilder.DropTable(
                name: "IdempotencyKeys",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "InboxMessages",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "QualificationThresholds",
                schema: "sourcing");

            migrationBuilder.DropTable(
                name: "TalentPoolEntries",
                schema: "sourcing");

            migrationBuilder.DropTable(
                name: "VerifiedEmployers",
                schema: "sourcing");
        }
    }
}
