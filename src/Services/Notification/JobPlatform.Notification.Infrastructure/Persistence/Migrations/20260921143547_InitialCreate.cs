using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobPlatform.Notification.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "notify");

            migrationBuilder.EnsureSchema(
                name: "messaging");

            migrationBuilder.CreateTable(
                name: "EmailTemplates",
                schema: "notify",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Locale = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PlaceholdersJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EditedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailTemplates", x => x.Id);
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
                name: "InAppNotifications",
                schema: "notify",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipientAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TypeCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    TitleAr = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    TitleEn = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    BodyAr = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    BodyEn = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ActionUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReadAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeliveredRealtime = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InAppNotifications", x => x.Id);
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
                name: "JobConfirmations",
                schema: "notify",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourcePlatformId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceJobId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PlatformJobId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PartnerAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConfirmedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobConfirmations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationPreferences",
                schema: "notify",
                columns: table => new
                {
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmailMode = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    SmsOptedIn = table.Column<bool>(type: "bit", nullable: false),
                    Mobile = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Suspended = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EmailJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InAppJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UnsubscribedJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationPreferences", x => x.AccountId);
                });

            migrationBuilder.CreateTable(
                name: "NotificationTypes",
                schema: "notify",
                columns: table => new
                {
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Icon = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Colour = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    TextAlternative = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsMandatory = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationTypes", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "OutboundMessages",
                schema: "notify",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    RecipientAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    DedupeKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TemplateCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    TemplateVersion = table.Column<int>(type: "int", nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Locale = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    SuppressionReason = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    ErrorCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    DeliveryStatus = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ProviderMessageId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SenderIdentity = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DigestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDigestCandidate = table.Column<bool>(type: "bit", nullable: false),
                    ContainsSecret = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NextAttemptUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboundMessages", x => x.Id);
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
                name: "SmsPolicy",
                schema: "notify",
                columns: table => new
                {
                    Version = table.Column<int>(type: "int", nullable: false),
                    EssentialCategories = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ChangedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmsPolicy", x => x.Version);
                });

            migrationBuilder.CreateTable(
                name: "WeeklyCycle",
                schema: "notify",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsoWeek = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeeklyCycle", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UQ_EmailTemplates_Code_Version_Locale",
                schema: "notify",
                table: "EmailTemplates",
                columns: new[] { "Code", "Version", "Locale" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyKeys_ExpiresAtUtc",
                schema: "messaging",
                table: "IdempotencyKeys",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_InApp_Recipient_Status_Created",
                schema: "notify",
                table: "InAppNotifications",
                columns: new[] { "RecipientAccountId", "Status", "CreatedAtUtc" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_Status_NextAttemptUtc",
                schema: "messaging",
                table: "InboxMessages",
                columns: new[] { "Status", "NextAttemptUtc" });

            migrationBuilder.CreateIndex(
                name: "UQ_JobConfirmations_Source",
                schema: "notify",
                table: "JobConfirmations",
                columns: new[] { "SourcePlatformId", "SourceJobId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboundMessages_ProviderMessageId",
                schema: "notify",
                table: "OutboundMessages",
                column: "ProviderMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_OutboundMessages_Status_NextAttempt",
                schema: "notify",
                table: "OutboundMessages",
                columns: new[] { "Status", "NextAttemptUtc" });

            migrationBuilder.CreateIndex(
                name: "UQ_OutboundMessages_DedupeKey",
                schema: "notify",
                table: "OutboundMessages",
                column: "DedupeKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Status_NextAttemptUtc",
                schema: "messaging",
                table: "OutboxMessages",
                columns: new[] { "Status", "NextAttemptUtc" });

            migrationBuilder.CreateIndex(
                name: "UQ_WeeklyCycle_Account_Week",
                schema: "notify",
                table: "WeeklyCycle",
                columns: new[] { "AccountId", "IsoWeek" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailTemplates",
                schema: "notify");

            migrationBuilder.DropTable(
                name: "IdempotencyKeys",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "InAppNotifications",
                schema: "notify");

            migrationBuilder.DropTable(
                name: "InboxMessages",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "JobConfirmations",
                schema: "notify");

            migrationBuilder.DropTable(
                name: "NotificationPreferences",
                schema: "notify");

            migrationBuilder.DropTable(
                name: "NotificationTypes",
                schema: "notify");

            migrationBuilder.DropTable(
                name: "OutboundMessages",
                schema: "notify");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "SmsPolicy",
                schema: "notify");

            migrationBuilder.DropTable(
                name: "WeeklyCycle",
                schema: "notify");
        }
    }
}
