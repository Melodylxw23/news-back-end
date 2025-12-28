using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace News_Back_end.Migrations
{
    /// <inheritdoc />
    public partial class MemberTags_Normalized : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IndustryTag",
                table: "Members");

            migrationBuilder.CreateTable(
                name: "IndustryTags",
                columns: table => new
                {
                    IndustryTagId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IndustryTags", x => x.IndustryTagId);
                });

            migrationBuilder.CreateTable(
                name: "InterestTags",
                columns: table => new
                {
                    InterestTagId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterestTags", x => x.InterestTagId);
                });

            migrationBuilder.CreateTable(
                name: "IndustryTagMember",
                columns: table => new
                {
                    IndustryTagsIndustryTagId = table.Column<int>(type: "int", nullable: false),
                    MembersMemberId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IndustryTagMember", x => new { x.IndustryTagsIndustryTagId, x.MembersMemberId });
                    table.ForeignKey(
                        name: "FK_IndustryTagMember_IndustryTags_IndustryTagsIndustryTagId",
                        column: x => x.IndustryTagsIndustryTagId,
                        principalTable: "IndustryTags",
                        principalColumn: "IndustryTagId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IndustryTagMember_Members_MembersMemberId",
                        column: x => x.MembersMemberId,
                        principalTable: "Members",
                        principalColumn: "MemberId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InterestTagMember",
                columns: table => new
                {
                    InterestsInterestTagId = table.Column<int>(type: "int", nullable: false),
                    MembersMemberId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterestTagMember", x => new { x.InterestsInterestTagId, x.MembersMemberId });
                    table.ForeignKey(
                        name: "FK_InterestTagMember_InterestTags_InterestsInterestTagId",
                        column: x => x.InterestsInterestTagId,
                        principalTable: "InterestTags",
                        principalColumn: "InterestTagId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InterestTagMember_Members_MembersMemberId",
                        column: x => x.MembersMemberId,
                        principalTable: "Members",
                        principalColumn: "MemberId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IndustryTagMember_MembersMemberId",
                table: "IndustryTagMember",
                column: "MembersMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_InterestTagMember_MembersMemberId",
                table: "InterestTagMember",
                column: "MembersMemberId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IndustryTagMember");

            migrationBuilder.DropTable(
                name: "InterestTagMember");

            migrationBuilder.DropTable(
                name: "IndustryTags");

            migrationBuilder.DropTable(
                name: "InterestTags");

            migrationBuilder.AddColumn<int>(
                name: "IndustryTag",
                table: "Members",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
