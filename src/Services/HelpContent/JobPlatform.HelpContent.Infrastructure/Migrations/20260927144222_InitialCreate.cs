using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobPlatform.HelpContent.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "content");

            migrationBuilder.EnsureSchema(
                name: "messaging");

            migrationBuilder.CreateTable(
                name: "CompanyProfilePages",
                schema: "content",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployerAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BackgroundAr = table.Column<string>(type: "nvarchar(max)", maxLength: 50000, nullable: true),
                    BackgroundEn = table.Column<string>(type: "nvarchar(max)", maxLength: 50000, nullable: true),
                    HighlightsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyProfilePages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContentCategories",
                schema: "content",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(max)", maxLength: 50000, nullable: true),
                    NameEn = table.Column<string>(type: "nvarchar(max)", maxLength: 50000, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContentCategorizations",
                schema: "content",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ArticleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CategoryIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TagsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentCategorizations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContextHelpMappings",
                schema: "content",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PageKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    HelpContentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContextHelpMappings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HelpContentOrganizations",
                schema: "content",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HelpContentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TopicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RolesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HelpContentOrganizations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HelpContents",
                schema: "content",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CurrentVersion = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HelpContents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HelpFeedback",
                schema: "content",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HelpContentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Rating = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HelpFeedback", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HelpTopics",
                schema: "content",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(max)", maxLength: 50000, nullable: true),
                    NameEn = table.Column<string>(type: "nvarchar(max)", maxLength: 50000, nullable: true),
                    IsRemoved = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HelpTopics", x => x.Id);
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
                name: "NewsArticles",
                schema: "content",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TitleAr = table.Column<string>(type: "nvarchar(max)", maxLength: 50000, nullable: true),
                    TitleEn = table.Column<string>(type: "nvarchar(max)", maxLength: 50000, nullable: true),
                    BodyAr = table.Column<string>(type: "nvarchar(max)", maxLength: 50000, nullable: true),
                    BodyEn = table.Column<string>(type: "nvarchar(max)", maxLength: 50000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ArchivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsArticles", x => x.Id);
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
                name: "TutorialProgress",
                schema: "content",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TutorialId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TutorialProgress", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HelpContentVersions",
                schema: "content",
                columns: table => new
                {
                    VersionNo = table.Column<int>(type: "int", nullable: false),
                    HelpContentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TitleAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TitleEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BodyAr = table.Column<string>(type: "nvarchar(max)", maxLength: 50000, nullable: true),
                    BodyEn = table.Column<string>(type: "nvarchar(max)", maxLength: 50000, nullable: true),
                    EditedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EditedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HelpContentVersions", x => new { x.HelpContentId, x.VersionNo });
                    table.ForeignKey(
                        name: "FK_HelpContentVersions_HelpContents_HelpContentId",
                        column: x => x.HelpContentId,
                        principalSchema: "content",
                        principalTable: "HelpContents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HelpMedia",
                schema: "content",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CaptionsRef = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TextAlternative = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    HelpContentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HelpMedia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HelpMedia_HelpContents_HelpContentId",
                        column: x => x.HelpContentId,
                        principalSchema: "content",
                        principalTable: "HelpContents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NewsMedia",
                schema: "content",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AltText = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    NewsArticleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsMedia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NewsMedia_NewsArticles_NewsArticleId",
                        column: x => x.NewsArticleId,
                        principalSchema: "content",
                        principalTable: "NewsArticles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UQ_CompanyProfilePages_EmployerAccountId",
                schema: "content",
                table: "CompanyProfilePages",
                column: "EmployerAccountId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_ContentCategorizations_ArticleId",
                schema: "content",
                table: "ContentCategorizations",
                column: "ArticleId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_ContextHelpMappings_PageKey",
                schema: "content",
                table: "ContextHelpMappings",
                column: "PageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_HelpContentOrganizations_HelpContentId",
                schema: "content",
                table: "HelpContentOrganizations",
                column: "HelpContentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_HelpFeedback_Content_User",
                schema: "content",
                table: "HelpFeedback",
                columns: new[] { "HelpContentId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HelpMedia_HelpContentId",
                schema: "content",
                table: "HelpMedia",
                column: "HelpContentId");

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
                name: "IX_NewsArticles_ContentHash",
                schema: "content",
                table: "NewsArticles",
                column: "ContentHash");

            migrationBuilder.CreateIndex(
                name: "IX_NewsArticles_Status_PublishedAt",
                schema: "content",
                table: "NewsArticles",
                columns: new[] { "Status", "PublishedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_NewsMedia_NewsArticleId",
                schema: "content",
                table: "NewsMedia",
                column: "NewsArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Status_NextAttemptUtc",
                schema: "messaging",
                table: "OutboxMessages",
                columns: new[] { "Status", "NextAttemptUtc" });

            migrationBuilder.CreateIndex(
                name: "UQ_TutorialProgress_User_Tutorial",
                schema: "content",
                table: "TutorialProgress",
                columns: new[] { "UserId", "TutorialId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompanyProfilePages",
                schema: "content");

            migrationBuilder.DropTable(
                name: "ContentCategories",
                schema: "content");

            migrationBuilder.DropTable(
                name: "ContentCategorizations",
                schema: "content");

            migrationBuilder.DropTable(
                name: "ContextHelpMappings",
                schema: "content");

            migrationBuilder.DropTable(
                name: "HelpContentOrganizations",
                schema: "content");

            migrationBuilder.DropTable(
                name: "HelpContentVersions",
                schema: "content");

            migrationBuilder.DropTable(
                name: "HelpFeedback",
                schema: "content");

            migrationBuilder.DropTable(
                name: "HelpMedia",
                schema: "content");

            migrationBuilder.DropTable(
                name: "HelpTopics",
                schema: "content");

            migrationBuilder.DropTable(
                name: "IdempotencyKeys",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "InboxMessages",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "NewsMedia",
                schema: "content");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "TutorialProgress",
                schema: "content");

            migrationBuilder.DropTable(
                name: "HelpContents",
                schema: "content");

            migrationBuilder.DropTable(
                name: "NewsArticles",
                schema: "content");
        }
    }
}
