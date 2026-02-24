using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace News_Back_end.Migrations
{
    /// <inheritdoc />
    public partial class add_consultant_language_preference : Migration
    {
 /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
    name: "Language",
           table: "ConsultantPreferences",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
         defaultValue: "English");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
    migrationBuilder.DropColumn(
                name: "Language",
  table: "ConsultantPreferences");
        }
    }
}
