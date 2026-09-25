using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MovieApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTvUpcomingEpisodeSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastUpcomingEpisodeSyncAtUtc",
                table: "tv_show_catalog_sync_states",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_episodes_AirDate",
                table: "episodes",
                column: "AirDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_episodes_AirDate",
                table: "episodes");

            migrationBuilder.DropColumn(
                name: "LastUpcomingEpisodeSyncAtUtc",
                table: "tv_show_catalog_sync_states");
        }
    }
}
