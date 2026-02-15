using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace News_Back_end.Migrations
{
    /// <inheritdoc />
    public partial class consultant_pref : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConsultantPreferences",
                columns: table => new
                {
                    ConsultantPreferenceId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConsultantUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TerritoriesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IndustriesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Frequency = table.Column<string>(type: "nvarchar(20)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    PreferredTimeMinutesUtc = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsultantPreferences", x => x.ConsultantPreferenceId);
                    table.ForeignKey(
                        name: "FK_ConsultantPreferences_AspNetUsers_ConsultantUserId",
                        column: x => x.ConsultantUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConsultantPreferences_ConsultantUserId",
                table: "ConsultantPreferences",
                column: "ConsultantUserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConsultantPreferences");
        }
    }
}
