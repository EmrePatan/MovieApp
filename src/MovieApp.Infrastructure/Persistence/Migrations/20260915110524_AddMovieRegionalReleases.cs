using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MovieApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMovieRegionalReleases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "movie_regional_releases",
                columns: table => new
                {
                    MovieId = table.Column<Guid>(type: "uuid", nullable: false),
                    Region = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    EffectiveReleaseDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EffectiveReleaseType = table.Column<int>(type: "integer", nullable: true),
                    Certification = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    IsFallbackGlobal = table.Column<bool>(type: "boolean", nullable: false),
                    SyncedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_movie_regional_releases", x => new { x.MovieId, x.Region });
                    table.ForeignKey(
                        name: "FK_movie_regional_releases_movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_movie_regional_releases_Region_EffectiveReleaseDate",
                table: "movie_regional_releases",
                columns: new[] { "Region", "EffectiveReleaseDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "movie_regional_releases");
        }
    }
}
