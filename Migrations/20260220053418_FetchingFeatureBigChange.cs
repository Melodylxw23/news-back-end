using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace News_Back_end.Migrations
{
    /// <inheritdoc />
    public partial class FetchingFeatureBigChange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AttemptNumber",
                table: "FetchAttempts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SourceIdsSnapshot",
                table: "FetchAttempts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SummaryFormat",
                table: "FetchAttempts",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SummaryLength",
                table: "FetchAttempts",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SummaryWordCount",
                table: "FetchAttempts",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AttemptNumber",
                table: "FetchAttempts");

            migrationBuilder.DropColumn(
                name: "SourceIdsSnapshot",
                table: "FetchAttempts");

            migrationBuilder.DropColumn(
                name: "SummaryFormat",
                table: "FetchAttempts");

            migrationBuilder.DropColumn(
                name: "SummaryLength",
                table: "FetchAttempts");

            migrationBuilder.DropColumn(
                name: "SummaryWordCount",
                table: "FetchAttempts");
        }
    }
}
