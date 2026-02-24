using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace News_Back_end.Migrations
{
    /// <inheritdoc />
    public partial class tags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SelectedInterestTagIdsJson",
                table: "BroadcastMessages",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "SelectedIndustryTagIdsJson",
                table: "BroadcastMessages",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SelectedInterestTagIdsJson",
                table: "BroadcastMessages");

            migrationBuilder.DropColumn(
                name: "SelectedIndustryTagIdsJson",
                table: "BroadcastMessages");
        }
    }
}
