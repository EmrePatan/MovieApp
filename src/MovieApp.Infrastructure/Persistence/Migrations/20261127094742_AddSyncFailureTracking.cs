using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MovieApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSyncFailureTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastUpcomingEpisodeSyncFailedAtUtc",
                table: "tv_show_catalog_sync_states",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "tmdb_changes_failed_targets",
                columns: table => new
                {
                    CheckpointKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangeDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    LastFailedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tmdb_changes_failed_targets", x => new { x.CheckpointKey, x.TargetId });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tmdb_changes_failed_targets");

            migrationBuilder.DropColumn(
                name: "LastUpcomingEpisodeSyncFailedAtUtc",
                table: "tv_show_catalog_sync_states");
        }
    }
}
