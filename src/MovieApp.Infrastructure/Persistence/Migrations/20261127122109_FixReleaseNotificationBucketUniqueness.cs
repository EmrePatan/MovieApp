using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MovieApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixReleaseNotificationBucketUniqueness : Migration
    {
        // The old index never rejected anything, so duplicates may exist and would block the new
        // indexes. One row per bucket is kept: pushed first, then most event links, then oldest.
        // Links move to the kept row; deleting the others cascades to their push deliveries.
        internal const string MergeDuplicateNotificationsSql = """
            CREATE TEMP TABLE duplicate_release_notifications ON COMMIT DROP AS
            SELECT ranked."Id", ranked.keeper_id
            FROM (
                SELECT n."Id",
                       FIRST_VALUE(n."Id") OVER (
                           PARTITION BY n."UserId", n."TvShowId", n."MovieId",
                                        n."NotificationType", n."AggregationWindowKey"
                           ORDER BY
                               EXISTS (SELECT 1 FROM push_notification_deliveries d
                                       WHERE d."UserReleaseNotificationId" = n."Id") DESC,
                               (SELECT COUNT(*) FROM user_release_notification_events e
                                WHERE e."UserReleaseNotificationId" = n."Id") DESC,
                               n."CreatedAtUtc",
                               n."Id") AS keeper_id
                FROM user_release_notifications n
            ) ranked
            WHERE ranked."Id" <> ranked.keeper_id;

            UPDATE user_release_notification_events e
            SET "UserReleaseNotificationId" = duplicate.keeper_id
            FROM duplicate_release_notifications duplicate
            WHERE e."UserReleaseNotificationId" = duplicate."Id";

            DELETE FROM user_release_notifications n
            USING duplicate_release_notifications duplicate
            WHERE n."Id" = duplicate."Id";
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_user_release_notifications_UserId_TvShowId_MovieId_Notifica~",
                table: "user_release_notifications");

            migrationBuilder.Sql(MergeDuplicateNotificationsSql);

            migrationBuilder.CreateIndex(
                name: "IX_user_release_notifications_UserId_MovieId_NotificationType_~",
                table: "user_release_notifications",
                columns: new[] { "UserId", "MovieId", "NotificationType", "AggregationWindowKey" },
                unique: true,
                filter: "\"MovieId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_user_release_notifications_UserId_TvShowId_NotificationType~",
                table: "user_release_notifications",
                columns: new[] { "UserId", "TvShowId", "NotificationType", "AggregationWindowKey" },
                unique: true,
                filter: "\"TvShowId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_user_release_notifications_UserId_MovieId_NotificationType_~",
                table: "user_release_notifications");

            migrationBuilder.DropIndex(
                name: "IX_user_release_notifications_UserId_TvShowId_NotificationType~",
                table: "user_release_notifications");

            migrationBuilder.CreateIndex(
                name: "IX_user_release_notifications_UserId_TvShowId_MovieId_Notifica~",
                table: "user_release_notifications",
                columns: new[] { "UserId", "TvShowId", "MovieId", "NotificationType", "AggregationWindowKey" },
                unique: true);
        }
    }
}
