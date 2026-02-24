using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace News_Back_end.Migrations
{
    /// <inheritdoc />
    public partial class AddFetchedArticleURLs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FetchedArticleUrls",
                columns: table => new
                {
                    FetchedArticleUrlId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApplicationUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SourceURL = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    FetchedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FetchedArticleUrls", x => x.FetchedArticleUrlId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FetchedArticleUrls_ApplicationUserId_SourceURL",
                table: "FetchedArticleUrls",
                columns: new[] { "ApplicationUserId", "SourceURL" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FetchedArticleUrls");
        }
    }
}
