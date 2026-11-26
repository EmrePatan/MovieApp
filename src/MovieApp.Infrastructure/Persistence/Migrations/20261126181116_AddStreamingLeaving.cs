using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MovieApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStreamingLeaving : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "streaming_api_usage",
                columns: table => new
                {
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    Region = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    RequestCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_streaming_api_usage", x => new { x.PeriodStart, x.Region });
                });

            migrationBuilder.CreateTable(
                name: "streaming_leaving_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Region = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    ServiceId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ServiceName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    MediaType = table.Column<int>(type: "integer", nullable: false),
                    TmdbId = table.Column<int>(type: "integer", nullable: false),
                    ContentId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeavesAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    WatchLink = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    FetchedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_streaming_leaving_items", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "streaming_leaving_regions",
                columns: table => new
                {
                    Region = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    LastAttemptAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSuccessAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastFetchRequestCount = table.Column<int>(type: "integer", nullable: false),
                    IsComplete = table.Column<bool>(type: "boolean", nullable: false),
                    ServicesJson = table.Column<string>(type: "text", nullable: true),
                    ServicesFetchedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_streaming_leaving_regions", x => x.Region);
                });

            migrationBuilder.CreateIndex(
                name: "IX_streaming_leaving_items_Region_LeavesAtUtc",
                table: "streaming_leaving_items",
                columns: new[] { "Region", "LeavesAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_streaming_leaving_items_Region_ServiceId_MediaType_ContentId",
                table: "streaming_leaving_items",
                columns: new[] { "Region", "ServiceId", "MediaType", "ContentId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "streaming_api_usage");

            migrationBuilder.DropTable(
                name: "streaming_leaving_items");

            migrationBuilder.DropTable(
                name: "streaming_leaving_regions");
        }
    }
}
