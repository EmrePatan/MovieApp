using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MovieApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTmdbCatalogMetadataFreshness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "TmdbMetadataUpdatedAtUtc",
                table: "tv_shows",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TmdbProviderDiscoverySeenAtUtc",
                table: "tv_shows",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TmdbMetadataUpdatedAtUtc",
                table: "movies",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TmdbProviderDiscoverySeenAtUtc",
                table: "movies",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_tv_shows_TmdbMetadataUpdatedAtUtc",
                table: "tv_shows",
                column: "TmdbMetadataUpdatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_tv_shows_TmdbProviderDiscoverySeenAtUtc",
                table: "tv_shows",
                column: "TmdbProviderDiscoverySeenAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_movies_TmdbMetadataUpdatedAtUtc",
                table: "movies",
                column: "TmdbMetadataUpdatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_movies_TmdbProviderDiscoverySeenAtUtc",
                table: "movies",
                column: "TmdbProviderDiscoverySeenAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_tv_shows_TmdbMetadataUpdatedAtUtc",
                table: "tv_shows");

            migrationBuilder.DropIndex(
                name: "IX_tv_shows_TmdbProviderDiscoverySeenAtUtc",
                table: "tv_shows");

            migrationBuilder.DropIndex(
                name: "IX_movies_TmdbMetadataUpdatedAtUtc",
                table: "movies");

            migrationBuilder.DropIndex(
                name: "IX_movies_TmdbProviderDiscoverySeenAtUtc",
                table: "movies");

            migrationBuilder.DropColumn(
                name: "TmdbMetadataUpdatedAtUtc",
                table: "tv_shows");

            migrationBuilder.DropColumn(
                name: "TmdbProviderDiscoverySeenAtUtc",
                table: "tv_shows");

            migrationBuilder.DropColumn(
                name: "TmdbMetadataUpdatedAtUtc",
                table: "movies");

            migrationBuilder.DropColumn(
                name: "TmdbProviderDiscoverySeenAtUtc",
                table: "movies");
        }
    }
}
