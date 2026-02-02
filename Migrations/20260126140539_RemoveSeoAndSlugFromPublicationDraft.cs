using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace News_Back_end.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSeoAndSlugFromPublicationDraft : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SeoDescription",
                table: "PublicationDrafts");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "PublicationDrafts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SeoDescription",
                table: "PublicationDrafts",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "PublicationDrafts",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }
    }
}
