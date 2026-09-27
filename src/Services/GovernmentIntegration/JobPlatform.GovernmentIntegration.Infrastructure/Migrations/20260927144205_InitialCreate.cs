using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobPlatform.GovernmentIntegration.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "govint");

            migrationBuilder.EnsureSchema(
                name: "messaging");

            migrationBuilder.CreateTable(
                name: "DataQuality",
                schema: "govint",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MigrationRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SnapshotVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    IssuesResolved = table.Column<int>(type: "int", nullable: false),
                    DuplicatesRemoved = table.Column<int>(type: "int", nullable: false),
                    FormatsStandardized = table.Column<int>(type: "int", nullable: false),
                    RecordsChecked = table.Column<int>(type: "int", nullable: false),
                    RecordsRejected = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataQuality", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EducationalCredentialVerifications",
                schema: "govint",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Institution = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CredentialName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    RetentionExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EducationalCredentialVerifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmployerVerifications",
                schema: "govint",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployerAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmissionJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    State = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Method = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    DecidedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployerVerifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GovernmentDataAccessLog",
                schema: "govint",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Component = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SubjectRef = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Decision = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ErrorCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GovernmentDataAccessLog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GovernmentSourceConnections",
                schema: "govint",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Endpoint = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    AuthMethod = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CredentialRef = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    LastSuccessfulSyncAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastKnownGoodSnapshotRef = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LastKnownGoodTakenAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Health = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ConsecutiveFailures = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GovernmentSourceConnections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GovernmentVerificationData",
                schema: "govint",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubjectType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SubjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    ImportedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RetentionExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VerifiedFieldsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GovernmentVerificationData", x => x.Id);
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
                name: "IdentityVerifications",
                schema: "govint",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubjectType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SubjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NationalIdReference = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    UnverifiedReason = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    RetentionExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdentityVerifications", x => x.Id);
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
                name: "KnownAccounts",
                schema: "govint",
                columns: table => new
                {
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastEventVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnownAccounts", x => x.AccountId);
                });

            migrationBuilder.CreateTable(
                name: "LegacyData",
                schema: "govint",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MigrationRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MigrationBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceSystem = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SourceRecordId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RecordType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SourcePayload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MappedPayload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Stage = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ValidationErrorsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegacyData", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MigrationRuns",
                schema: "govint",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InitiatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CurrentPhaseIndex = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MigrationRuns", x => x.Id);
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
                name: "EmployerVerificationAttempts",
                schema: "govint",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptNo = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ErrorCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EmployerVerificationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployerVerificationAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployerVerificationAttempts_EmployerVerifications_EmployerVerificationId",
                        column: x => x.EmployerVerificationId,
                        principalSchema: "govint",
                        principalTable: "EmployerVerifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MigrationLog",
                schema: "govint",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Phase = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    AtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MigrationRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MigrationLog", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MigrationLog_MigrationRuns_MigrationRunId",
                        column: x => x.MigrationRunId,
                        principalSchema: "govint",
                        principalTable: "MigrationRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MigrationPhases",
                schema: "govint",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    TestOutcome = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MigrationRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MigrationPhases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MigrationPhases_MigrationRuns_MigrationRunId",
                        column: x => x.MigrationRunId,
                        principalSchema: "govint",
                        principalTable: "MigrationRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DataQuality_BatchId",
                schema: "govint",
                table: "DataQuality",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_EducationalCredentialVerifications_SubjectId",
                schema: "govint",
                table: "EducationalCredentialVerifications",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "UQ_EmployerVerificationAttempts_Verification_AttemptNo",
                schema: "govint",
                table: "EmployerVerificationAttempts",
                columns: new[] { "EmployerVerificationId", "AttemptNo" },
                unique: true,
                filter: "[EmployerVerificationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EmployerVerifications_EmployerAccountId_State",
                schema: "govint",
                table: "EmployerVerifications",
                columns: new[] { "EmployerAccountId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_GovernmentDataAccessLog_OccurredAtUtc",
                schema: "govint",
                table: "GovernmentDataAccessLog",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "UQ_GovernmentSourceConnections_Source",
                schema: "govint",
                table: "GovernmentSourceConnections",
                column: "Source",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GovernmentVerificationData_RetentionExpiresAtUtc",
                schema: "govint",
                table: "GovernmentVerificationData",
                column: "RetentionExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_GovernmentVerificationData_Subject",
                schema: "govint",
                table: "GovernmentVerificationData",
                columns: new[] { "SubjectType", "SubjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyKeys_ExpiresAtUtc",
                schema: "messaging",
                table: "IdempotencyKeys",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_IdentityVerifications_SubjectId",
                schema: "govint",
                table: "IdentityVerifications",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_Status_NextAttemptUtc",
                schema: "messaging",
                table: "InboxMessages",
                columns: new[] { "Status", "NextAttemptUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LegacyData_MigrationBatchId_Stage",
                schema: "govint",
                table: "LegacyData",
                columns: new[] { "MigrationBatchId", "Stage" });

            migrationBuilder.CreateIndex(
                name: "UQ_LegacyData_SourceSystem_SourceRecordId",
                schema: "govint",
                table: "LegacyData",
                columns: new[] { "SourceSystem", "SourceRecordId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MigrationLog_MigrationRunId",
                schema: "govint",
                table: "MigrationLog",
                column: "MigrationRunId");

            migrationBuilder.CreateIndex(
                name: "IX_MigrationPhases_MigrationRunId",
                schema: "govint",
                table: "MigrationPhases",
                column: "MigrationRunId");

            migrationBuilder.CreateIndex(
                name: "IX_MigrationRuns_Status",
                schema: "govint",
                table: "MigrationRuns",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Status_NextAttemptUtc",
                schema: "messaging",
                table: "OutboxMessages",
                columns: new[] { "Status", "NextAttemptUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DataQuality",
                schema: "govint");

            migrationBuilder.DropTable(
                name: "EducationalCredentialVerifications",
                schema: "govint");

            migrationBuilder.DropTable(
                name: "EmployerVerificationAttempts",
                schema: "govint");

            migrationBuilder.DropTable(
                name: "GovernmentDataAccessLog",
                schema: "govint");

            migrationBuilder.DropTable(
                name: "GovernmentSourceConnections",
                schema: "govint");

            migrationBuilder.DropTable(
                name: "GovernmentVerificationData",
                schema: "govint");

            migrationBuilder.DropTable(
                name: "IdempotencyKeys",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "IdentityVerifications",
                schema: "govint");

            migrationBuilder.DropTable(
                name: "InboxMessages",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "KnownAccounts",
                schema: "govint");

            migrationBuilder.DropTable(
                name: "LegacyData",
                schema: "govint");

            migrationBuilder.DropTable(
                name: "MigrationLog",
                schema: "govint");

            migrationBuilder.DropTable(
                name: "MigrationPhases",
                schema: "govint");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "EmployerVerifications",
                schema: "govint");

            migrationBuilder.DropTable(
                name: "MigrationRuns",
                schema: "govint");
        }
    }
}
