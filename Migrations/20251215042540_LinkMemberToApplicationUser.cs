using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace News_Back_end.Migrations
{
    /// <inheritdoc />
    public partial class LinkMemberToApplicationUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IndustryTagMember_IndustryTags_IndustryTagsIndustryTagId",
                table: "IndustryTagMember");

            migrationBuilder.DropForeignKey(
                name: "FK_IndustryTagMember_Members_MembersMemberId",
                table: "IndustryTagMember");

            migrationBuilder.DropForeignKey(
                name: "FK_InterestTagMember_InterestTags_InterestsInterestTagId",
                table: "InterestTagMember");

            migrationBuilder.DropForeignKey(
                name: "FK_InterestTagMember_Members_MembersMemberId",
                table: "InterestTagMember");

            migrationBuilder.DropPrimaryKey(
                name: "PK_InterestTagMember",
                table: "InterestTagMember");

            migrationBuilder.DropPrimaryKey(
                name: "PK_IndustryTagMember",
                table: "IndustryTagMember");

            migrationBuilder.RenameTable(
                name: "InterestTagMember",
                newName: "MemberInterests");

            migrationBuilder.RenameTable(
                name: "IndustryTagMember",
                newName: "MemberIndustryTags");

            migrationBuilder.RenameIndex(
                name: "IX_InterestTagMember_MembersMemberId",
                table: "MemberInterests",
                newName: "IX_MemberInterests_MembersMemberId");

            migrationBuilder.RenameIndex(
                name: "IX_IndustryTagMember_MembersMemberId",
                table: "MemberIndustryTags",
                newName: "IX_MemberIndustryTags_MembersMemberId");

            migrationBuilder.AddColumn<string>(
                name: "ApplicationUserId",
                table: "Members",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_MemberInterests",
                table: "MemberInterests",
                columns: new[] { "InterestsInterestTagId", "MembersMemberId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_MemberIndustryTags",
                table: "MemberIndustryTags",
                columns: new[] { "IndustryTagsIndustryTagId", "MembersMemberId" });

            migrationBuilder.CreateIndex(
                name: "IX_Members_ApplicationUserId",
                table: "Members",
                column: "ApplicationUserId",
                unique: true,
                filter: "[ApplicationUserId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_MemberIndustryTags_IndustryTags_IndustryTagsIndustryTagId",
                table: "MemberIndustryTags",
                column: "IndustryTagsIndustryTagId",
                principalTable: "IndustryTags",
                principalColumn: "IndustryTagId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MemberIndustryTags_Members_MembersMemberId",
                table: "MemberIndustryTags",
                column: "MembersMemberId",
                principalTable: "Members",
                principalColumn: "MemberId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MemberInterests_InterestTags_InterestsInterestTagId",
                table: "MemberInterests",
                column: "InterestsInterestTagId",
                principalTable: "InterestTags",
                principalColumn: "InterestTagId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MemberInterests_Members_MembersMemberId",
                table: "MemberInterests",
                column: "MembersMemberId",
                principalTable: "Members",
                principalColumn: "MemberId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Members_AspNetUsers_ApplicationUserId",
                table: "Members",
                column: "ApplicationUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MemberIndustryTags_IndustryTags_IndustryTagsIndustryTagId",
                table: "MemberIndustryTags");

            migrationBuilder.DropForeignKey(
                name: "FK_MemberIndustryTags_Members_MembersMemberId",
                table: "MemberIndustryTags");

            migrationBuilder.DropForeignKey(
                name: "FK_MemberInterests_InterestTags_InterestsInterestTagId",
                table: "MemberInterests");

            migrationBuilder.DropForeignKey(
                name: "FK_MemberInterests_Members_MembersMemberId",
                table: "MemberInterests");

            migrationBuilder.DropForeignKey(
                name: "FK_Members_AspNetUsers_ApplicationUserId",
                table: "Members");

            migrationBuilder.DropIndex(
                name: "IX_Members_ApplicationUserId",
                table: "Members");

            migrationBuilder.DropPrimaryKey(
                name: "PK_MemberInterests",
                table: "MemberInterests");

            migrationBuilder.DropPrimaryKey(
                name: "PK_MemberIndustryTags",
                table: "MemberIndustryTags");

            migrationBuilder.DropColumn(
                name: "ApplicationUserId",
                table: "Members");

            migrationBuilder.RenameTable(
                name: "MemberInterests",
                newName: "InterestTagMember");

            migrationBuilder.RenameTable(
                name: "MemberIndustryTags",
                newName: "IndustryTagMember");

            migrationBuilder.RenameIndex(
                name: "IX_MemberInterests_MembersMemberId",
                table: "InterestTagMember",
                newName: "IX_InterestTagMember_MembersMemberId");

            migrationBuilder.RenameIndex(
                name: "IX_MemberIndustryTags_MembersMemberId",
                table: "IndustryTagMember",
                newName: "IX_IndustryTagMember_MembersMemberId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_InterestTagMember",
                table: "InterestTagMember",
                columns: new[] { "InterestsInterestTagId", "MembersMemberId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_IndustryTagMember",
                table: "IndustryTagMember",
                columns: new[] { "IndustryTagsIndustryTagId", "MembersMemberId" });

            migrationBuilder.AddForeignKey(
                name: "FK_IndustryTagMember_IndustryTags_IndustryTagsIndustryTagId",
                table: "IndustryTagMember",
                column: "IndustryTagsIndustryTagId",
                principalTable: "IndustryTags",
                principalColumn: "IndustryTagId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_IndustryTagMember_Members_MembersMemberId",
                table: "IndustryTagMember",
                column: "MembersMemberId",
                principalTable: "Members",
                principalColumn: "MemberId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_InterestTagMember_InterestTags_InterestsInterestTagId",
                table: "InterestTagMember",
                column: "InterestsInterestTagId",
                principalTable: "InterestTags",
                principalColumn: "InterestTagId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_InterestTagMember_Members_MembersMemberId",
                table: "InterestTagMember",
                column: "MembersMemberId",
                principalTable: "Members",
                principalColumn: "MemberId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
