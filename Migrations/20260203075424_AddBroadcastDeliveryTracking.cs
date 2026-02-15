using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace News_Back_end.Migrations
{
    /// <inheritdoc />
    public partial class AddBroadcastDeliveryTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BroadcastDeliveries",
                columns: table => new
                {
                    BroadcastDeliveryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BroadcastMessageId = table.Column<int>(type: "int", nullable: false),
                    MemberId = table.Column<int>(type: "int", nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeliverySuccess = table.Column<bool>(type: "bit", nullable: false),
                    DeliveryError = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EmailOpened = table.Column<bool>(type: "bit", nullable: false),
                    OpenedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OpenCount = table.Column<int>(type: "int", nullable: false),
                    RecipientEmail = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UserAgent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BroadcastDeliveries", x => x.BroadcastDeliveryId);
                    table.ForeignKey(
                        name: "FK_BroadcastDeliveries_BroadcastMessages_BroadcastMessageId",
                        column: x => x.BroadcastMessageId,
                        principalTable: "BroadcastMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BroadcastDeliveries_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "MemberId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BroadcastDeliveries_BroadcastMessageId_MemberId",
                table: "BroadcastDeliveries",
                columns: new[] { "BroadcastMessageId", "MemberId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BroadcastDeliveries_MemberId",
                table: "BroadcastDeliveries",
                column: "MemberId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BroadcastDeliveries");
        }
    }
}
