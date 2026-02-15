using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace News_Back_end.Migrations
{
    /// <inheritdoc />
    public partial class cnultat_snd_onc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConsultantInsightsSendLogs",
                columns: table => new
                {
                    ConsultantInsightsSendLogId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConsultantUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Period = table.Column<string>(type: "nvarchar(20)", nullable: false),
                    PeriodDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SentAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    Success = table.Column<bool>(type: "bit", nullable: false),
                    Error = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsultantInsightsSendLogs", x => x.ConsultantInsightsSendLogId);
                    table.ForeignKey(
                        name: "FK_ConsultantInsightsSendLogs_AspNetUsers_ConsultantUserId",
                        column: x => x.ConsultantUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConsultantInsightsSendLogs_ConsultantUserId_Period_PeriodDateUtc",
                table: "ConsultantInsightsSendLogs",
                columns: new[] { "ConsultantUserId", "Period", "PeriodDateUtc" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConsultantInsightsSendLogs");
        }
    }
}
