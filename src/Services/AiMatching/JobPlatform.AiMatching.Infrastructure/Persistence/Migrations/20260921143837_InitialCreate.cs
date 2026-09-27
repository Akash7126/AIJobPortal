using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobPlatform.AiMatching.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "matching");

            migrationBuilder.EnsureSchema(
                name: "messaging");

            migrationBuilder.CreateTable(
                name: "CandidateShortlists",
                schema: "matching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobPostingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployerAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedSize = table.Column<int>(type: "int", nullable: false),
                    ConfigVersion = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ItemsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FailureReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ComputedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateShortlists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Embeddings",
                schema: "matching",
                columns: table => new
                {
                    EntityType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModelVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Vector = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Embeddings", x => new { x.EntityType, x.EntityId });
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
                name: "JobRecommendations",
                schema: "matching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Strategy = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ComputedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobRecommendations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JobSemantics",
                schema: "matching",
                columns: table => new
                {
                    JobPostingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SkillsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LevelsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CategoriesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Language = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    Confidence = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    LowConfidence = table.Column<bool>(type: "bit", nullable: false),
                    ModelVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SemanticsVersion = table.Column<int>(type: "int", nullable: false),
                    PostingVersion = table.Column<long>(type: "bigint", nullable: false),
                    AnalyzedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobSemantics", x => x.JobPostingId);
                });

            migrationBuilder.CreateTable(
                name: "KnownPostings",
                schema: "matching",
                columns: table => new
                {
                    JobPostingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployerAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Suspended = table.Column<bool>(type: "bit", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    LastEventVersion = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnownPostings", x => x.JobPostingId);
                });

            migrationBuilder.CreateTable(
                name: "KnownProfiles",
                schema: "matching",
                columns: table => new
                {
                    ProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Standing = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    LastEventVersion = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnownProfiles", x => x.ProfileId);
                });

            migrationBuilder.CreateTable(
                name: "MatchingConfigurations",
                schema: "matching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConfigVersion = table.Column<int>(type: "int", nullable: false),
                    MatchThresholdPercent = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    WeightSkillOverlap = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    WeightEducation = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    WeightTraining = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    WeightLocation = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    WeightExperience = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    WeightSalary = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    ShortlistSize = table.Column<int>(type: "int", nullable: false),
                    LowConfidenceThresholdPercent = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchingConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MatchScores",
                schema: "matching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobPostingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Score = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    BreakdownJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConfigVersion = table.Column<int>(type: "int", nullable: false),
                    ProfileVersion = table.Column<long>(type: "bigint", nullable: false),
                    PostingVersion = table.Column<long>(type: "bigint", nullable: false),
                    ModelVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ComputedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchScores", x => x.Id);
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
                name: "ParsedProfileData",
                schema: "matching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResumeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParsedProfileData", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ResumeParsedData",
                schema: "matching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResumeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Language = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    FailureCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    LanguageFlagged = table.Column<bool>(type: "bit", nullable: false),
                    ModelVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SupersededBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SkillsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResumeParsedData", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SkillStandardizations",
                schema: "matching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResumeParsedDataId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaxonomyVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SkillStandardizations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkItems",
                schema: "matching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    NextAttemptUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConfigurationHistory",
                schema: "matching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConfigVersion = table.Column<int>(type: "int", nullable: false),
                    MatchThresholdPercent = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    WeightSkillOverlap = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    WeightEducation = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    WeightTraining = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    WeightLocation = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    WeightExperience = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    WeightSalary = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    ShortlistSize = table.Column<int>(type: "int", nullable: false),
                    LowConfidenceThresholdPercent = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    ChangedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConfigurationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfigurationHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConfigurationHistory_MatchingConfigurations_ConfigurationId",
                        column: x => x.ConfigurationId,
                        principalSchema: "matching",
                        principalTable: "MatchingConfigurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProfileFields",
                schema: "matching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Confidence = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    NeedsReview = table.Column<bool>(type: "bit", nullable: false),
                    ParsedProfileDataId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfileFields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProfileFields_ParsedProfileData_ParsedProfileDataId",
                        column: x => x.ParsedProfileDataId,
                        principalSchema: "matching",
                        principalTable: "ParsedProfileData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ParsedFields",
                schema: "matching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Confidence = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    NeedsReview = table.Column<bool>(type: "bit", nullable: false),
                    ResumeParsedDataId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParsedFields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParsedFields_ResumeParsedData_ResumeParsedDataId",
                        column: x => x.ResumeParsedDataId,
                        principalSchema: "matching",
                        principalTable: "ResumeParsedData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SkillMappings",
                schema: "matching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExtractedTerm = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CanonicalCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Confidence = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    IsFreeText = table.Column<bool>(type: "bit", nullable: false),
                    NeedsReview = table.Column<bool>(type: "bit", nullable: false),
                    SkillStandardizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SkillMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SkillMappings_SkillStandardizations_SkillStandardizationId",
                        column: x => x.SkillStandardizationId,
                        principalSchema: "matching",
                        principalTable: "SkillStandardizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CandidateShortlists_Posting",
                schema: "matching",
                table: "CandidateShortlists",
                column: "JobPostingId");

            migrationBuilder.CreateIndex(
                name: "IX_ConfigurationHistory_ConfigurationId",
                schema: "matching",
                table: "ConfigurationHistory",
                column: "ConfigurationId");

            migrationBuilder.CreateIndex(
                name: "IX_ConfigurationHistory_Version",
                schema: "matching",
                table: "ConfigurationHistory",
                column: "ConfigVersion");

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
                name: "IX_JobRecommendations_Profile_ComputedAt",
                schema: "matching",
                table: "JobRecommendations",
                columns: new[] { "ProfileId", "ComputedAtUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_KnownPostings_Employer_Status",
                schema: "matching",
                table: "KnownPostings",
                columns: new[] { "EmployerAccountId", "Status" });

            migrationBuilder.CreateIndex(
                name: "UQ_KnownProfiles_Owner",
                schema: "matching",
                table: "KnownProfiles",
                column: "OwnerAccountId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MatchScores_Posting_Score",
                schema: "matching",
                table: "MatchScores",
                columns: new[] { "JobPostingId", "Score" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_MatchScores_Profile_Score",
                schema: "matching",
                table: "MatchScores",
                columns: new[] { "ProfileId", "Score" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "UQ_MatchScores_Profile_Posting",
                schema: "matching",
                table: "MatchScores",
                columns: new[] { "ProfileId", "JobPostingId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Status_NextAttemptUtc",
                schema: "messaging",
                table: "OutboxMessages",
                columns: new[] { "Status", "NextAttemptUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ParsedFields_ResumeParsedDataId",
                schema: "matching",
                table: "ParsedFields",
                column: "ResumeParsedDataId");

            migrationBuilder.CreateIndex(
                name: "IX_ParsedProfileData_Owner",
                schema: "matching",
                table: "ParsedProfileData",
                column: "OwnerAccountId");

            migrationBuilder.CreateIndex(
                name: "UQ_ParsedProfileData_Profile",
                schema: "matching",
                table: "ParsedProfileData",
                column: "ProfileId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProfileFields_ParsedProfileDataId",
                schema: "matching",
                table: "ProfileFields",
                column: "ParsedProfileDataId");

            migrationBuilder.CreateIndex(
                name: "IX_ResumeParsedData_Profile",
                schema: "matching",
                table: "ResumeParsedData",
                column: "ProfileId");

            migrationBuilder.CreateIndex(
                name: "UQ_ResumeParsedData_Resume_Sha256",
                schema: "matching",
                table: "ResumeParsedData",
                columns: new[] { "ResumeId", "Sha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SkillMappings_SkillStandardizationId",
                schema: "matching",
                table: "SkillMappings",
                column: "SkillStandardizationId");

            migrationBuilder.CreateIndex(
                name: "IX_SkillStandardizations_ResumeParsedData",
                schema: "matching",
                table: "SkillStandardizations",
                column: "ResumeParsedDataId");

            migrationBuilder.CreateIndex(
                name: "IX_SkillStandardizations_TaxonomyVersion",
                schema: "matching",
                table: "SkillStandardizations",
                column: "TaxonomyVersion");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_Status_NextAttempt",
                schema: "matching",
                table: "WorkItems",
                columns: new[] { "Status", "NextAttemptUtc" });

            migrationBuilder.CreateIndex(
                name: "UQ_WorkItems_Pending",
                schema: "matching",
                table: "WorkItems",
                columns: new[] { "Kind", "EntityId" },
                unique: true,
                filter: "[Status] = 'Pending'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CandidateShortlists",
                schema: "matching");

            migrationBuilder.DropTable(
                name: "ConfigurationHistory",
                schema: "matching");

            migrationBuilder.DropTable(
                name: "Embeddings",
                schema: "matching");

            migrationBuilder.DropTable(
                name: "IdempotencyKeys",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "InboxMessages",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "JobRecommendations",
                schema: "matching");

            migrationBuilder.DropTable(
                name: "JobSemantics",
                schema: "matching");

            migrationBuilder.DropTable(
                name: "KnownPostings",
                schema: "matching");

            migrationBuilder.DropTable(
                name: "KnownProfiles",
                schema: "matching");

            migrationBuilder.DropTable(
                name: "MatchScores",
                schema: "matching");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "ParsedFields",
                schema: "matching");

            migrationBuilder.DropTable(
                name: "ProfileFields",
                schema: "matching");

            migrationBuilder.DropTable(
                name: "SkillMappings",
                schema: "matching");

            migrationBuilder.DropTable(
                name: "WorkItems",
                schema: "matching");

            migrationBuilder.DropTable(
                name: "MatchingConfigurations",
                schema: "matching");

            migrationBuilder.DropTable(
                name: "ResumeParsedData",
                schema: "matching");

            migrationBuilder.DropTable(
                name: "ParsedProfileData",
                schema: "matching");

            migrationBuilder.DropTable(
                name: "SkillStandardizations",
                schema: "matching");
        }
    }
}
