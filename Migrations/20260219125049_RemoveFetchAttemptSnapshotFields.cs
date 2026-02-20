using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace News_Back_end.Migrations
{
    /// <inheritdoc />
    public partial class RemoveFetchAttemptSnapshotFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IncludeChineseSummary",
                table: "FetchAttempts");

            migrationBuilder.DropColumn(
                name: "IncludeEnglishSummary",
                table: "FetchAttempts");

            migrationBuilder.DropColumn(
                name: "SummaryFormat",
                table: "FetchAttempts");

            migrationBuilder.DropColumn(
                name: "SummaryLanguage",
                table: "FetchAttempts");

            migrationBuilder.DropColumn(
                name: "SummaryWordCount",
                table: "FetchAttempts");

            migrationBuilder.DropColumn(
                name: "TranslateOnFetch",
                table: "FetchAttempts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IncludeChineseSummary",
                table: "FetchAttempts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IncludeEnglishSummary",
                table: "FetchAttempts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SummaryFormat",
                table: "FetchAttempts",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SummaryLanguage",
                table: "FetchAttempts",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "SummaryWordCount",
                table: "FetchAttempts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "TranslateOnFetch",
                table: "FetchAttempts",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
