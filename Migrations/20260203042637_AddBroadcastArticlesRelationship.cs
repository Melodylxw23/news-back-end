using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace News_Back_end.Migrations
{
    /// <inheritdoc />
    public partial class AddBroadcastArticlesRelationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BroadcastMessageArticles",
                columns: table => new
                {
                    BroadcastMessagesId = table.Column<int>(type: "int", nullable: false),
                    SelectedArticlesPublicationDraftId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BroadcastMessageArticles", x => new { x.BroadcastMessagesId, x.SelectedArticlesPublicationDraftId });
                    table.ForeignKey(
                        name: "FK_BroadcastMessageArticles_BroadcastMessages_BroadcastMessagesId",
                        column: x => x.BroadcastMessagesId,
                        principalTable: "BroadcastMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BroadcastMessageArticles_PublicationDrafts_SelectedArticlesPublicationDraftId",
                        column: x => x.SelectedArticlesPublicationDraftId,
                        principalTable: "PublicationDrafts",
                        principalColumn: "PublicationDraftId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BroadcastMessageArticles_SelectedArticlesPublicationDraftId",
                table: "BroadcastMessageArticles",
                column: "SelectedArticlesPublicationDraftId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BroadcastMessageArticles");
        }
    }
}
