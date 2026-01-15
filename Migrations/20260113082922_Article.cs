using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace News_Back_end.Migrations
{
    /// <inheritdoc />
    public partial class Article : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Summaries",
                columns: table => new
                {
                    SummaryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NewsId = table.Column<int>(type: "int", nullable: false),
                    SummaryText = table.Column<string>(type: "text", nullable: true),
                    SummaryGeneratedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    SummaryGeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SummaryEditedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SummaryEditedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PdfPath = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    PdfGeneratedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    PdfGeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PdfEditedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PdfEditedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PptPath = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    PptGeneratedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    PptGeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PptEditedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PptEditedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EditNotes = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Summaries", x => x.SummaryId);
                    table.ForeignKey(
                        name: "FK_Summaries_NewsArticles_NewsId",
                        column: x => x.NewsId,
                        principalTable: "NewsArticles",
                        principalColumn: "NewsArticleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ArticleLabels",
                columns: table => new
                {
                    LabelId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NewsId = table.Column<int>(type: "int", nullable: false),
                    SummaryId = table.Column<int>(type: "int", nullable: true),
                    WorkflowStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AddedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AddedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArticleLabels", x => x.LabelId);
                    table.ForeignKey(
                        name: "FK_ArticleLabels_NewsArticles_NewsId",
                        column: x => x.NewsId,
                        principalTable: "NewsArticles",
                        principalColumn: "NewsArticleId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ArticleLabels_Summaries_SummaryId",
                        column: x => x.SummaryId,
                        principalTable: "Summaries",
                        principalColumn: "SummaryId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArticleLabels_NewsId",
                table: "ArticleLabels",
                column: "NewsId");

            migrationBuilder.CreateIndex(
                name: "IX_ArticleLabels_SummaryId",
                table: "ArticleLabels",
                column: "SummaryId");

            migrationBuilder.CreateIndex(
                name: "IX_Summaries_NewsId",
                table: "Summaries",
                column: "NewsId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArticleLabels");

            migrationBuilder.DropTable(
                name: "Summaries");
        }
    }
}
