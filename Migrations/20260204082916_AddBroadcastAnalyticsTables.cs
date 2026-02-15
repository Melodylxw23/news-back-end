using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace News_Back_end.Migrations
{
    /// <inheritdoc />
    public partial class AddBroadcastAnalyticsTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "OpenedAt",
                table: "BroadcastDeliveries",
                newName: "UnsubscribedAt");

            migrationBuilder.AddColumn<string>(
                name: "BounceReason",
                table: "BroadcastDeliveries",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BounceType",
                table: "BroadcastDeliveries",
                type: "nvarchar(50)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ClickCount",
                table: "BroadcastDeliveries",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DeviceType",
                table: "BroadcastDeliveries",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailClient",
                table: "BroadcastDeliveries",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EstimatedReadTimeSeconds",
                table: "BroadcastDeliveries",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FirstClickedAt",
                table: "BroadcastDeliveries",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FirstOpenedAt",
                table: "BroadcastDeliveries",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasClicked",
                table: "BroadcastDeliveries",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastClickedAt",
                table: "BroadcastDeliveries",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastOpenedAt",
                table: "BroadcastDeliveries",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OperatingSystem",
                table: "BroadcastDeliveries",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "BroadcastDeliveries",
                type: "nvarchar(50)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "Unsubscribed",
                table: "BroadcastDeliveries",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "BroadcastAnalyticsSummaries",
                columns: table => new
                {
                    BroadcastAnalyticsSummaryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BroadcastMessageId = table.Column<int>(type: "int", nullable: false),
                    TotalSent = table.Column<int>(type: "int", nullable: false),
                    TotalDelivered = table.Column<int>(type: "int", nullable: false),
                    TotalBounced = table.Column<int>(type: "int", nullable: false),
                    DeliveryRate = table.Column<double>(type: "float", nullable: false),
                    UniqueOpens = table.Column<int>(type: "int", nullable: false),
                    TotalOpens = table.Column<int>(type: "int", nullable: false),
                    OpenRate = table.Column<double>(type: "float", nullable: false),
                    UniqueClicks = table.Column<int>(type: "int", nullable: false),
                    TotalClicks = table.Column<int>(type: "int", nullable: false),
                    ClickRate = table.Column<double>(type: "float", nullable: false),
                    ClickToOpenRate = table.Column<double>(type: "float", nullable: false),
                    FirstOpenAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastOpenAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PeakEngagementHour = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EngagementByCountryJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EngagementByLanguageJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EngagementByIndustryJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EngagementByInterestJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DesktopOpens = table.Column<int>(type: "int", nullable: false),
                    MobileOpens = table.Column<int>(type: "int", nullable: false),
                    TabletOpens = table.Column<int>(type: "int", nullable: false),
                    ComputedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BroadcastAnalyticsSummaries", x => x.BroadcastAnalyticsSummaryId);
                    table.ForeignKey(
                        name: "FK_BroadcastAnalyticsSummaries_BroadcastMessages_BroadcastMessageId",
                        column: x => x.BroadcastMessageId,
                        principalTable: "BroadcastMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BroadcastLinkClicks",
                columns: table => new
                {
                    BroadcastLinkClickId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BroadcastDeliveryId = table.Column<int>(type: "int", nullable: false),
                    PublicationDraftId = table.Column<int>(type: "int", nullable: true),
                    OriginalUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    LinkIdentifier = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ClickedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserAgent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeviceType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    EmailClient = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BroadcastLinkClicks", x => x.BroadcastLinkClickId);
                    table.ForeignKey(
                        name: "FK_BroadcastLinkClicks_BroadcastDeliveries_BroadcastDeliveryId",
                        column: x => x.BroadcastDeliveryId,
                        principalTable: "BroadcastDeliveries",
                        principalColumn: "BroadcastDeliveryId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BroadcastLinkClicks_PublicationDrafts_PublicationDraftId",
                        column: x => x.PublicationDraftId,
                        principalTable: "PublicationDrafts",
                        principalColumn: "PublicationDraftId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "DailyBroadcastMetrics",
                columns: table => new
                {
                    DailyBroadcastMetricId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MetricDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BroadcastsSent = table.Column<int>(type: "int", nullable: false),
                    TotalEmailsSent = table.Column<int>(type: "int", nullable: false),
                    TotalEmailsDelivered = table.Column<int>(type: "int", nullable: false),
                    TotalOpens = table.Column<int>(type: "int", nullable: false),
                    TotalClicks = table.Column<int>(type: "int", nullable: false),
                    AverageOpenRate = table.Column<double>(type: "float", nullable: false),
                    AverageClickRate = table.Column<double>(type: "float", nullable: false),
                    UniqueRecipientsReached = table.Column<int>(type: "int", nullable: false),
                    NewSubscribersEngaged = table.Column<int>(type: "int", nullable: false),
                    ComputedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyBroadcastMetrics", x => x.DailyBroadcastMetricId);
                });

            migrationBuilder.CreateTable(
                name: "MemberEngagementProfiles",
                columns: table => new
                {
                    MemberEngagementProfileId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MemberId = table.Column<int>(type: "int", nullable: false),
                    TotalEmailsReceived = table.Column<int>(type: "int", nullable: false),
                    TotalEmailsOpened = table.Column<int>(type: "int", nullable: false),
                    TotalLinksClicked = table.Column<int>(type: "int", nullable: false),
                    LifetimeOpenRate = table.Column<double>(type: "float", nullable: false),
                    LifetimeClickRate = table.Column<double>(type: "float", nullable: false),
                    EngagementLevel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PreferredDayOfWeek = table.Column<int>(type: "int", nullable: true),
                    PreferredHourOfDay = table.Column<int>(type: "int", nullable: true),
                    TopEngagedTopicsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastEmailReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastEmailOpenedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastLinkClickedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RecencyScore = table.Column<double>(type: "float", nullable: false),
                    FrequencyScore = table.Column<double>(type: "float", nullable: false),
                    OverallEngagementScore = table.Column<double>(type: "float", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberEngagementProfiles", x => x.MemberEngagementProfileId);
                    table.ForeignKey(
                        name: "FK_MemberEngagementProfiles_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "MemberId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TopicPerformanceMetrics",
                columns: table => new
                {
                    TopicPerformanceMetricId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InterestTagId = table.Column<int>(type: "int", nullable: true),
                    IndustryTagId = table.Column<int>(type: "int", nullable: true),
                    MetricDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BroadcastCount = table.Column<int>(type: "int", nullable: false),
                    TotalSent = table.Column<int>(type: "int", nullable: false),
                    TotalOpens = table.Column<int>(type: "int", nullable: false),
                    TotalClicks = table.Column<int>(type: "int", nullable: false),
                    AverageOpenRate = table.Column<double>(type: "float", nullable: false),
                    AverageClickRate = table.Column<double>(type: "float", nullable: false),
                    EngagementScore = table.Column<double>(type: "float", nullable: false),
                    ComputedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TopicPerformanceMetrics", x => x.TopicPerformanceMetricId);
                    table.ForeignKey(
                        name: "FK_TopicPerformanceMetrics_IndustryTags_IndustryTagId",
                        column: x => x.IndustryTagId,
                        principalTable: "IndustryTags",
                        principalColumn: "IndustryTagId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TopicPerformanceMetrics_InterestTags_InterestTagId",
                        column: x => x.InterestTagId,
                        principalTable: "InterestTags",
                        principalColumn: "InterestTagId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BroadcastAnalyticsSummaries_BroadcastMessageId",
                table: "BroadcastAnalyticsSummaries",
                column: "BroadcastMessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BroadcastLinkClicks_BroadcastDeliveryId",
                table: "BroadcastLinkClicks",
                column: "BroadcastDeliveryId");

            migrationBuilder.CreateIndex(
                name: "IX_BroadcastLinkClicks_PublicationDraftId",
                table: "BroadcastLinkClicks",
                column: "PublicationDraftId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyBroadcastMetrics_MetricDate",
                table: "DailyBroadcastMetrics",
                column: "MetricDate",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MemberEngagementProfiles_MemberId",
                table: "MemberEngagementProfiles",
                column: "MemberId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TopicPerformanceMetrics_IndustryTagId",
                table: "TopicPerformanceMetrics",
                column: "IndustryTagId");

            migrationBuilder.CreateIndex(
                name: "IX_TopicPerformanceMetrics_InterestTagId",
                table: "TopicPerformanceMetrics",
                column: "InterestTagId");

            migrationBuilder.CreateIndex(
                name: "IX_TopicPerformanceMetrics_MetricDate_InterestTagId_IndustryTagId",
                table: "TopicPerformanceMetrics",
                columns: new[] { "MetricDate", "InterestTagId", "IndustryTagId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BroadcastAnalyticsSummaries");

            migrationBuilder.DropTable(
                name: "BroadcastLinkClicks");

            migrationBuilder.DropTable(
                name: "DailyBroadcastMetrics");

            migrationBuilder.DropTable(
                name: "MemberEngagementProfiles");

            migrationBuilder.DropTable(
                name: "TopicPerformanceMetrics");

            migrationBuilder.DropColumn(
                name: "BounceReason",
                table: "BroadcastDeliveries");

            migrationBuilder.DropColumn(
                name: "BounceType",
                table: "BroadcastDeliveries");

            migrationBuilder.DropColumn(
                name: "ClickCount",
                table: "BroadcastDeliveries");

            migrationBuilder.DropColumn(
                name: "DeviceType",
                table: "BroadcastDeliveries");

            migrationBuilder.DropColumn(
                name: "EmailClient",
                table: "BroadcastDeliveries");

            migrationBuilder.DropColumn(
                name: "EstimatedReadTimeSeconds",
                table: "BroadcastDeliveries");

            migrationBuilder.DropColumn(
                name: "FirstClickedAt",
                table: "BroadcastDeliveries");

            migrationBuilder.DropColumn(
                name: "FirstOpenedAt",
                table: "BroadcastDeliveries");

            migrationBuilder.DropColumn(
                name: "HasClicked",
                table: "BroadcastDeliveries");

            migrationBuilder.DropColumn(
                name: "LastClickedAt",
                table: "BroadcastDeliveries");

            migrationBuilder.DropColumn(
                name: "LastOpenedAt",
                table: "BroadcastDeliveries");

            migrationBuilder.DropColumn(
                name: "OperatingSystem",
                table: "BroadcastDeliveries");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "BroadcastDeliveries");

            migrationBuilder.DropColumn(
                name: "Unsubscribed",
                table: "BroadcastDeliveries");

            migrationBuilder.RenameColumn(
                name: "UnsubscribedAt",
                table: "BroadcastDeliveries",
                newName: "OpenedAt");
        }
    }
}
