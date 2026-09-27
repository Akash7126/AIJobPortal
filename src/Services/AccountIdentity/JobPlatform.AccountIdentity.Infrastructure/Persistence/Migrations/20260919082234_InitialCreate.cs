using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobPlatform.AccountIdentity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "identity");

            migrationBuilder.EnsureSchema(
                name: "messaging");

            migrationBuilder.CreateTable(
                name: "AccessLog",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Action = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Resource = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Decision = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessLog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Accounts",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: true),
                    Mobile = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ExternalIdentityKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    RegistrationNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    PasswordHash = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Standing = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RegistrationLevel = table.Column<int>(type: "int", nullable: false),
                    MfaSecret = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    MfaEnabled = table.Column<bool>(type: "bit", nullable: false),
                    MfaEnrolledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailedAttempts = table.Column<int>(type: "int", nullable: false),
                    FirstFailedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LockedUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActivatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MustChangePassword = table.Column<bool>(type: "bit", nullable: false),
                    PasswordChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PasswordPolicyVersion = table.Column<int>(type: "int", nullable: false),
                    EmailVerifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EmailVerificationTokenHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailVerificationExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApiCredentials",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartnerAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    KeyId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SecretHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IpWhitelistJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LimitsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IssuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailedAuthAttempts = table.Column<int>(type: "int", nullable: false),
                    FirstFailedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiCredentials", x => x.Id);
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
                name: "PasswordPolicy",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MinLength = table.Column<int>(type: "int", nullable: false),
                    RequireUpper = table.Column<bool>(type: "bit", nullable: false),
                    RequireLower = table.Column<bool>(type: "bit", nullable: false),
                    RequireDigit = table.Column<bool>(type: "bit", nullable: false),
                    PolicyVersion = table.Column<int>(type: "int", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasswordPolicy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PrivacyConsents",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GuestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicyVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ChoicesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Locale = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrivacyConsents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SessionSettings",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdleTimeoutMinutes = table.Column<int>(type: "int", nullable: false),
                    SettingVersion = table.Column<int>(type: "int", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SigningKeys",
                schema: "identity",
                columns: table => new
                {
                    Kid = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PublicKey = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EncryptedPrivateKey = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NotBeforeUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NotAfterUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SigningKeys", x => x.Kid);
                });

            migrationBuilder.CreateTable(
                name: "AccountRoles",
                schema: "identity",
                columns: table => new
                {
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountRoles", x => new { x.AccountId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AccountRoles_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "identity",
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AccountStatusHistory",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    From = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    To = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    By = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountStatusHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountStatusHistory_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "identity",
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ActivationChallenges",
                schema: "identity",
                columns: table => new
                {
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CodeHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    IssuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivationChallenges", x => x.AccountId);
                    table.ForeignKey(
                        name: "FK_ActivationChallenges_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "identity",
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                schema: "identity",
                columns: table => new
                {
                    Permission = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => new { x.RoleId, x.Permission });
                    table.ForeignKey(
                        name: "FK_RolePermissions_Roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "identity",
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccessLog_AccountId_At",
                schema: "identity",
                table: "AccessLog",
                columns: new[] { "AccountId", "AtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AccessLog_At",
                schema: "identity",
                table: "AccessLog",
                column: "AtUtc");

            migrationBuilder.CreateIndex(
                name: "UQ_Accounts_ActorType_Email",
                schema: "identity",
                table: "Accounts",
                columns: new[] { "ActorType", "Email" },
                unique: true,
                filter: "[Email] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UQ_Accounts_ActorType_ExternalIdentityKey",
                schema: "identity",
                table: "Accounts",
                columns: new[] { "ActorType", "ExternalIdentityKey" },
                unique: true,
                filter: "[ExternalIdentityKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UQ_Accounts_ActorType_Mobile",
                schema: "identity",
                table: "Accounts",
                columns: new[] { "ActorType", "Mobile" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountStatusHistory_AccountId_AtUtc",
                schema: "identity",
                table: "AccountStatusHistory",
                columns: new[] { "AccountId", "AtUtc" });

            migrationBuilder.CreateIndex(
                name: "UQ_ApiCredentials_KeyId",
                schema: "identity",
                table: "ApiCredentials",
                column: "KeyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_ApiCredentials_OneActivePerPartner",
                schema: "identity",
                table: "ApiCredentials",
                column: "PartnerAccountId",
                unique: true,
                filter: "[Status] = 'Active'");

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
                name: "UQ_PrivacyConsents_GuestId_PolicyVersion",
                schema: "identity",
                table: "PrivacyConsents",
                columns: new[] { "GuestId", "PolicyVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Roles_Name",
                schema: "identity",
                table: "Roles",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccessLog",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "AccountRoles",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "AccountStatusHistory",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "ActivationChallenges",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "ApiCredentials",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "InboxMessages",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "PasswordPolicy",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "PrivacyConsents",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "RolePermissions",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "SessionSettings",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "SigningKeys",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "Accounts",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "Roles",
                schema: "identity");
        }
    }
}
