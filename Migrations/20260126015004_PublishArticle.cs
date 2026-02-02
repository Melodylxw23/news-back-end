using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace News_Back_end.Migrations
{
    /// <inheritdoc />
    public partial class PublishArticle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PublicationDrafts",
                columns: table => new
                {
                    PublicationDraftId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NewsArticleId = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HeroImageUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    HeroImageAlt = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    HeroImageSource = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SeoDescription = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Slug = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FullContentEN = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FullContentZH = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    PublishedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ScheduledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IndustryTagId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicationDrafts", x => x.PublicationDraftId);
                    table.ForeignKey(
                        name: "FK_PublicationDrafts_IndustryTags_IndustryTagId",
                        column: x => x.IndustryTagId,
                        principalTable: "IndustryTags",
                        principalColumn: "IndustryTagId");
                    table.ForeignKey(
                        name: "FK_PublicationDrafts_NewsArticles_NewsArticleId",
                        column: x => x.NewsArticleId,
                        principalTable: "NewsArticles",
                        principalColumn: "NewsArticleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PublicationDraftInterestTags",
                columns: table => new
                {
                    InterestTagsInterestTagId = table.Column<int>(type: "int", nullable: false),
                    PublicationDraftsPublicationDraftId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicationDraftInterestTags", x => new { x.InterestTagsInterestTagId, x.PublicationDraftsPublicationDraftId });
                    table.ForeignKey(
                        name: "FK_PublicationDraftInterestTags_InterestTags_InterestTagsInterestTagId",
                        column: x => x.InterestTagsInterestTagId,
                        principalTable: "InterestTags",
                        principalColumn: "InterestTagId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PublicationDraftInterestTags_PublicationDrafts_PublicationDraftsPublicationDraftId",
                        column: x => x.PublicationDraftsPublicationDraftId,
                        principalTable: "PublicationDrafts",
                        principalColumn: "PublicationDraftId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PublicationDraftInterestTags_PublicationDraftsPublicationDraftId",
                table: "PublicationDraftInterestTags",
                column: "PublicationDraftsPublicationDraftId");

            migrationBuilder.CreateIndex(
                name: "IX_PublicationDrafts_IndustryTagId",
                table: "PublicationDrafts",
                column: "IndustryTagId");

            migrationBuilder.CreateIndex(
                name: "IX_PublicationDrafts_NewsArticleId",
                table: "PublicationDrafts",
                column: "NewsArticleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PublicationDraftInterestTags");

            migrationBuilder.DropTable(
                name: "PublicationDrafts");
        }
    }
}
