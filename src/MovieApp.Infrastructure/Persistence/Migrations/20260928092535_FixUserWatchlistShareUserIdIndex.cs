using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MovieApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixUserWatchlistShareUserIdIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Early deploys used a unique UserId index (one share per user).
            migrationBuilder.Sql(
                """
                DROP INDEX IF EXISTS "IX_user_watchlist_shares_UserId";
                CREATE INDEX "IX_user_watchlist_shares_UserId"
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
                CREATE UNIQUE INDEX "IX_user_watchlist_shares_UserId"
                    ON user_watchlist_shares ("UserId");
                """);
        }
    }
}
