using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MovieApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnsureUserWatchlistShareUserIdIndexNonUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Legacy deploys created UNIQUE on UserId (one share per user). Drop as constraint and index.
            migrationBuilder.Sql(
                """
                ALTER TABLE user_watchlist_shares
                    DROP CONSTRAINT IF EXISTS "IX_user_watchlist_shares_UserId";

                DROP INDEX IF EXISTS "IX_user_watchlist_shares_UserId";

                CREATE INDEX IF NOT EXISTS "IX_user_watchlist_shares_UserId"
                    ON user_watchlist_shares ("UserId");
                """);

            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_user_watchlist_shares_WatchlistId"
                    ON user_watchlist_shares ("WatchlistId");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP INDEX IF EXISTS "IX_user_watchlist_shares_UserId";
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_user_watchlist_shares_UserId"
                    ON user_watchlist_shares ("UserId");
                """);
        }
    }
}
