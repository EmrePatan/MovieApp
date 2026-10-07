using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MovieApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWatchedEpisodesUserCoveringIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Additive covering index. The existing UserId and (UserId, EpisodeId) indexes stay.
            // CONCURRENTLY keeps writes unblocked on a live Postgres; it cannot run inside a transaction.
            migrationBuilder.Sql(
                """
                CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_watched_episodes_UserId_EpisodeId_WatchedAt"
                    ON watched_episodes ("UserId") INCLUDE ("EpisodeId", "WatchedAt");
                """,
                suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP INDEX CONCURRENTLY IF EXISTS "IX_watched_episodes_UserId_EpisodeId_WatchedAt";
                """,
                suppressTransaction: true);
        }
    }
}
