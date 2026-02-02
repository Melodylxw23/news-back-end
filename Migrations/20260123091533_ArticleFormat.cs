using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace News_Back_end.Migrations
{
    /// <inheritdoc />
    public partial class ArticleFormat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Title",
                table: "NewsArticles",
                newName: "TitleZH");

            migrationBuilder.AddColumn<string>(
                name: "TitleEN",
                table: "NewsArticles",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TitleEN",
                table: "NewsArticles");

            migrationBuilder.RenameColumn(
                name: "TitleZH",
                table: "NewsArticles",
                newName: "Title");
        }
    }
}
