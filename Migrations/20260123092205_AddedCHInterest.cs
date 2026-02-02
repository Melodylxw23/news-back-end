using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace News_Back_end.Migrations
{
    /// <inheritdoc />
    public partial class AddedCHInterest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Name",
                table: "InterestTags");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "IndustryTags");

            migrationBuilder.AddColumn<string>(
                name: "DescriptionEN",
                table: "InterestTags",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DescriptionZH",
                table: "InterestTags",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameEN",
                table: "InterestTags",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NameZH",
                table: "InterestTags",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DescriptionEN",
                table: "IndustryTags",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DescriptionZH",
                table: "IndustryTags",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameEN",
                table: "IndustryTags",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NameZH",
                table: "IndustryTags",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DescriptionEN",
                table: "InterestTags");

            migrationBuilder.DropColumn(
                name: "DescriptionZH",
                table: "InterestTags");

            migrationBuilder.DropColumn(
                name: "NameEN",
                table: "InterestTags");

            migrationBuilder.DropColumn(
                name: "NameZH",
                table: "InterestTags");

            migrationBuilder.DropColumn(
                name: "DescriptionEN",
                table: "IndustryTags");

            migrationBuilder.DropColumn(
                name: "DescriptionZH",
                table: "IndustryTags");

            migrationBuilder.DropColumn(
                name: "NameEN",
                table: "IndustryTags");

            migrationBuilder.DropColumn(
                name: "NameZH",
                table: "IndustryTags");

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "InterestTags",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "IndustryTags",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
