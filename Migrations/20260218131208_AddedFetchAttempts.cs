using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace News_Back_end.Migrations
{
    /// <inheritdoc />
    public partial class AddedFetchAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FetchedAt",
                table: "NewsArticles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FetchAttempts",
                columns: table => new
                {
                    FetchAttemptId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApplicationUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FetchedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SummaryFormat = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SummaryWordCount = table.Column<int>(type: "int", nullable: false),
                    TranslateOnFetch = table.Column<bool>(type: "bit", nullable: false),
                    IncludeEnglishSummary = table.Column<bool>(type: "bit", nullable: false),
                    IncludeChineseSummary = table.Column<bool>(type: "bit", nullable: false),
                    SummaryLanguage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MaxArticlesPerFetch = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FetchAttempts", x => x.FetchAttemptId);
                    table.ForeignKey(
                        name: "FK_FetchAttempts_AspNetUsers_ApplicationUserId",
                        column: x => x.ApplicationUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FetchAttemptArticles",
                columns: table => new
                {
                    FetchAttemptArticleId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FetchAttemptId = table.Column<int>(type: "int", nullable: false),
                    NewsArticleId = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FetchAttemptArticles", x => x.FetchAttemptArticleId);
                    table.ForeignKey(
                        name: "FK_FetchAttemptArticles_FetchAttempts_FetchAttemptId",
                        column: x => x.FetchAttemptId,
                        principalTable: "FetchAttempts",
                        principalColumn: "FetchAttemptId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FetchAttemptArticles_NewsArticles_NewsArticleId",
                        column: x => x.NewsArticleId,
                        principalTable: "NewsArticles",
                        principalColumn: "NewsArticleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FetchAttemptArticles_FetchAttemptId_NewsArticleId",
                table: "FetchAttemptArticles",
                columns: new[] { "FetchAttemptId", "NewsArticleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FetchAttemptArticles_NewsArticleId",
                table: "FetchAttemptArticles",
                column: "NewsArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_FetchAttempts_ApplicationUserId",
                table: "FetchAttempts",
                column: "ApplicationUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FetchAttemptArticles");

            migrationBuilder.DropTable(
                name: "FetchAttempts");

            migrationBuilder.DropColumn(
                name: "FetchedAt",
                table: "NewsArticles");
        }
    }
}
