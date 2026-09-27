using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobPlatform.JobSeekerProfile.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "messaging");

            migrationBuilder.EnsureSchema(
                name: "profile");

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
                name: "JobPreferences",
                schema: "profile",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobTypesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IndustriesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LocationsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SalaryMin = table.Column<decimal>(type: "decimal(12,2)", nullable: true),
                    SalaryMax = table.Column<decimal>(type: "decimal(12,2)", nullable: true),
                    SalaryCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    WorkArrangementsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobPreferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KnownAccounts",
                schema: "profile",
                columns: table => new
                {
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ActiveSinceUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Standing = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnownAccounts", x => x.AccountId);
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
                name: "PrivacySettings",
                schema: "profile",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Visibility = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    PublicSharingActive = table.Column<bool>(type: "bit", nullable: false),
                    DeletionState = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    DeactivationRequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrivacySettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProcessedParsedData",
                schema: "profile",
                columns: table => new
                {
                    ResumeParsedDataId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessedParsedData", x => x.ResumeParsedDataId);
                });

            migrationBuilder.CreateTable(
                name: "Profiles",
                schema: "profile",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    MobileNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Gender = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SalaryMin = table.Column<decimal>(type: "decimal(12,2)", nullable: true),
                    SalaryMax = table.Column<decimal>(type: "decimal(12,2)", nullable: true),
                    SalaryCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    Governorate = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Street = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    YearsOfExperience = table.Column<decimal>(type: "decimal(6,2)", nullable: true),
                    Statement = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Bio = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CompletionPercent = table.Column<int>(type: "int", nullable: false),
                    SectionStatusJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Profiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Resumes",
                schema: "profile",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Format = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false),
                    UploadedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Resumes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShareLinks",
                schema: "profile",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Token = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShareLinks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SupplementaryDocuments",
                schema: "profile",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UploadedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplementaryDocuments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProfileCertificates",
                schema: "profile",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Issuer = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IssuedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Source = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfileCertificates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProfileCertificates_Profiles_ProfileId",
                        column: x => x.ProfileId,
                        principalSchema: "profile",
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProfileEducation",
                schema: "profile",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Degree = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Institution = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    From = table.Column<DateTime>(type: "datetime2", nullable: true),
                    To = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Source = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfileEducation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProfileEducation_Profiles_ProfileId",
                        column: x => x.ProfileId,
                        principalSchema: "profile",
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProfileExperience",
                schema: "profile",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Company = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    From = table.Column<DateTime>(type: "datetime2", nullable: true),
                    To = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Source = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfileExperience", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProfileExperience_Profiles_ProfileId",
                        column: x => x.ProfileId,
                        principalSchema: "profile",
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProfileSkills",
                schema: "profile",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Class = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfileSkills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProfileSkills_Profiles_ProfileId",
                        column: x => x.ProfileId,
                        principalSchema: "profile",
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProfileSocialLinks",
                schema: "profile",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Network = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfileSocialLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProfileSocialLinks_Profiles_ProfileId",
                        column: x => x.ProfileId,
                        principalSchema: "profile",
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProfileTraining",
                schema: "profile",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CompletedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Source = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfileTraining", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProfileTraining_Profiles_ProfileId",
                        column: x => x.ProfileId,
                        principalSchema: "profile",
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                name: "IX_ProfileCertificates_ProfileId",
                schema: "profile",
                table: "ProfileCertificates",
                column: "ProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ProfileEducation_ProfileId",
                schema: "profile",
                table: "ProfileEducation",
                column: "ProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ProfileExperience_ProfileId",
                schema: "profile",
                table: "ProfileExperience",
                column: "ProfileId");

            migrationBuilder.CreateIndex(
                name: "UQ_Profiles_OwnerAccountId",
                schema: "profile",
                table: "Profiles",
                column: "OwnerAccountId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProfileSkills_ProfileId",
                schema: "profile",
                table: "ProfileSkills",
                column: "ProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ProfileSocialLinks_ProfileId",
                schema: "profile",
                table: "ProfileSocialLinks",
                column: "ProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ProfileTraining_ProfileId",
                schema: "profile",
                table: "ProfileTraining",
                column: "ProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Resumes_Profile_Current",
                schema: "profile",
                table: "Resumes",
                columns: new[] { "ProfileId", "IsCurrent" });

            migrationBuilder.CreateIndex(
                name: "IX_ShareLinks_Profile_Active",
                schema: "profile",
                table: "ShareLinks",
                columns: new[] { "ProfileId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "UQ_ShareLinks_Token",
                schema: "profile",
                table: "ShareLinks",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplementaryDocuments_Owner",
                schema: "profile",
                table: "SupplementaryDocuments",
                columns: new[] { "OwnerType", "OwnerId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IdempotencyKeys",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "InboxMessages",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "JobPreferences",
                schema: "profile");

            migrationBuilder.DropTable(
                name: "KnownAccounts",
                schema: "profile");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "PrivacySettings",
                schema: "profile");

            migrationBuilder.DropTable(
                name: "ProcessedParsedData",
                schema: "profile");

            migrationBuilder.DropTable(
                name: "ProfileCertificates",
                schema: "profile");

            migrationBuilder.DropTable(
                name: "ProfileEducation",
                schema: "profile");

            migrationBuilder.DropTable(
                name: "ProfileExperience",
                schema: "profile");

            migrationBuilder.DropTable(
                name: "ProfileSkills",
                schema: "profile");

            migrationBuilder.DropTable(
                name: "ProfileSocialLinks",
                schema: "profile");

            migrationBuilder.DropTable(
                name: "ProfileTraining",
                schema: "profile");

            migrationBuilder.DropTable(
                name: "Resumes",
                schema: "profile");

            migrationBuilder.DropTable(
                name: "ShareLinks",
                schema: "profile");

            migrationBuilder.DropTable(
                name: "SupplementaryDocuments",
                schema: "profile");

            migrationBuilder.DropTable(
                name: "Profiles",
                schema: "profile");
        }
    }
}
