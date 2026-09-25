using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MovieApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWatchHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "watched_episodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EpisodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    WatchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_watched_episodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_watched_episodes_episodes_EpisodeId",
                        column: x => x.EpisodeId,
                        principalTable: "episodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_watched_episodes_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "watched_movies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MovieId = table.Column<Guid>(type: "uuid", nullable: false),
                    WatchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_watched_movies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_watched_movies_movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_watched_movies_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_watched_episodes_EpisodeId",
                table: "watched_episodes",
                column: "EpisodeId");

            migrationBuilder.CreateIndex(
                name: "IX_watched_episodes_UserId",
                table: "watched_episodes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_watched_episodes_UserId_EpisodeId",
                table: "watched_episodes",
                columns: new[] { "UserId", "EpisodeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_watched_episodes_UserId_WatchedAt",
                table: "watched_episodes",
                columns: new[] { "UserId", "WatchedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_watched_episodes_WatchedAt",
                table: "watched_episodes",
                column: "WatchedAt");

            migrationBuilder.CreateIndex(
                name: "IX_watched_movies_MovieId",
                table: "watched_movies",
                column: "MovieId");

            migrationBuilder.CreateIndex(
                name: "IX_watched_movies_UserId",
                table: "watched_movies",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_watched_movies_UserId_MovieId",
                table: "watched_movies",
                columns: new[] { "UserId", "MovieId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_watched_movies_UserId_WatchedAt",
                table: "watched_movies",
                columns: new[] { "UserId", "WatchedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_watched_movies_WatchedAt",
                table: "watched_movies",
                column: "WatchedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "watched_episodes");

            migrationBuilder.DropTable(
                name: "watched_movies");
        }
    }
}
