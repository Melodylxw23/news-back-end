using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace News_Back_end.Migrations
{
 /// <inheritdoc />
    public partial class AddConsultantInsightsHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
          migrationBuilder.CreateTable(
       name: "ConsultantInsightsHistories",
 columns: table => new
 {
        ConsultantInsightsHistoryId = table.Column<int>(type: "int", nullable: false)
 .Annotation("SqlServer:Identity", "1, 1"),
  ConsultantUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
            Period = table.Column<string>(type: "nvarchar(20)", nullable: false),
   PeriodDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
     KeyDevelopmentsJson = table.Column<string>(type: "nvarchar(5000)", nullable: false),
       OpportunitiesJson = table.Column<string>(type: "nvarchar(5000)", nullable: false),
           WatchoutsJson = table.Column<string>(type: "nvarchar(5000)", nullable: false),
          RecommendedActionsJson = table.Column<string>(type: "nvarchar(5000)", nullable: false),
       ExecutiveSummary = table.Column<string>(type: "nvarchar(1000)", nullable: false),
           GeneratedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
  },
       constraints: table =>
      {
   table.PrimaryKey("PK_ConsultantInsightsHistories", x => x.ConsultantInsightsHistoryId);
 table.ForeignKey(
               name: "FK_ConsultantInsightsHistories_AspNetUsers_ConsultantUserId",
           column: x => x.ConsultantUserId,
              principalTable: "AspNetUsers",
        principalColumn: "Id",
       onDelete: ReferentialAction.Cascade);
       });

          migrationBuilder.CreateIndex(
   name: "IX_ConsultantInsightsHistories_ConsultantUserId_Period_PeriodDateUtc",
     table: "ConsultantInsightsHistories",
            columns: new[] { "ConsultantUserId", "Period", "PeriodDateUtc" },
                unique: true);
        }

        /// <inheritdoc />
 protected override void Down(MigrationBuilder migrationBuilder)
        {
         migrationBuilder.DropTable(
     name: "ConsultantInsightsHistories");
        }
 }
}
