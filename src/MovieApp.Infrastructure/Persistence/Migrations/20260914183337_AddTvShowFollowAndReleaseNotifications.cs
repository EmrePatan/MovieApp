using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MovieApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTvShowFollowAndReleaseNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "catalog_release_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TvShowId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SeasonNumber = table.Column<int>(type: "integer", nullable: false),
                    EpisodeNumber = table.Column<int>(type: "integer", nullable: true),
                    ReleaseAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DetectedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DedupeKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalog_release_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_catalog_release_events_tv_shows_TvShowId",
                        column: x => x.TvShowId,
                        principalTable: "tv_shows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tmdb_tv_changes_sync_checkpoints",
                columns: table => new
                {
                    CheckpointKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LastCompletedEndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    LastCompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tmdb_tv_changes_sync_checkpoints", x => x.CheckpointKey);
                });

            migrationBuilder.CreateTable(
                name: "tv_show_catalog_sync_states",
                columns: table => new
                {
                    TvShowId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastRefreshedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastChangeSignalDate = table.Column<DateOnly>(type: "date", nullable: true),
                    LastRefreshReason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    NextHotCheckAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tv_show_catalog_sync_states", x => x.TvShowId);
                    table.ForeignKey(
                        name: "FK_tv_show_catalog_sync_states_tv_shows_TvShowId",
                        column: x => x.TvShowId,
                        principalTable: "tv_shows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tv_show_follows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TvShowId = table.Column<Guid>(type: "uuid", nullable: false),
                    NotifyNewSeasons = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    NotifyNewEpisodes = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    NotifyFromUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BaselineEstablishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tv_show_follows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tv_show_follows_tv_shows_TvShowId",
                        column: x => x.TvShowId,
                        principalTable: "tv_shows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tv_show_follows_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_release_notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TvShowId = table.Column<Guid>(type: "uuid", nullable: false),
                    NotificationType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AggregationWindowKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Body = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_release_notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_release_notifications_tv_shows_TvShowId",
                        column: x => x.TvShowId,
                        principalTable: "tv_shows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_release_notifications_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "AK_user_release_notifications_Id_UserId",
                table: "user_release_notifications",
                columns: new[] { "Id", "UserId" },
                unique: true);

            migrationBuilder.CreateTable(
                name: "user_release_notification_events",
                columns: table => new
                {
                    UserReleaseNotificationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CatalogReleaseEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_release_notification_events", x => new { x.UserReleaseNotificationId, x.CatalogReleaseEventId });
                    table.ForeignKey(
                        name: "FK_user_release_notification_events_catalog_release_events_Cat~",
                        column: x => x.CatalogReleaseEventId,
                        principalTable: "catalog_release_events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_release_notification_events_user_release_notifications~",
                        columns: x => new { x.UserReleaseNotificationId, x.UserId },
                        principalTable: "user_release_notifications",
                        principalColumns: new[] { "Id", "UserId" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_release_notification_events_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_catalog_release_events_DedupeKey",
                table: "catalog_release_events",
                column: "DedupeKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_catalog_release_events_EventType",
                table: "catalog_release_events",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_catalog_release_events_ReleaseAtUtc",
                table: "catalog_release_events",
                column: "ReleaseAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_catalog_release_events_TvShowId",
                table: "catalog_release_events",
                column: "TvShowId");

            migrationBuilder.CreateIndex(
                name: "IX_tv_show_follows_TvShowId",
                table: "tv_show_follows",
                column: "TvShowId");

            migrationBuilder.CreateIndex(
                name: "IX_tv_show_follows_UserId",
                table: "tv_show_follows",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_tv_show_follows_UserId_TvShowId",
                table: "tv_show_follows",
                columns: new[] { "UserId", "TvShowId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_release_notification_events_CatalogReleaseEventId",
                table: "user_release_notification_events",
                column: "CatalogReleaseEventId");

            migrationBuilder.CreateIndex(
                name: "IX_user_release_notification_events_UserId_CatalogReleaseEvent~",
                table: "user_release_notification_events",
                columns: new[] { "UserId", "CatalogReleaseEventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_release_notification_events_UserReleaseNotificationId_~",
                table: "user_release_notification_events",
                columns: new[] { "UserReleaseNotificationId", "CatalogReleaseEventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_release_notifications_Status",
                table: "user_release_notifications",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_user_release_notifications_TvShowId",
                table: "user_release_notifications",
                column: "TvShowId");

            migrationBuilder.CreateIndex(
                name: "IX_user_release_notifications_UserId",
                table: "user_release_notifications",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_release_notifications_UserId_TvShowId_NotificationType~",
                table: "user_release_notifications",
                columns: new[] { "UserId", "TvShowId", "NotificationType", "AggregationWindowKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_release_notification_events_UserReleaseNotificationId~1",
                table: "user_release_notification_events",
                columns: new[] { "UserReleaseNotificationId", "UserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tmdb_tv_changes_sync_checkpoints");

            migrationBuilder.DropTable(
                name: "tv_show_catalog_sync_states");

            migrationBuilder.DropTable(
                name: "tv_show_follows");

            migrationBuilder.DropTable(
                name: "user_release_notification_events");

            migrationBuilder.DropTable(
                name: "catalog_release_events");

            migrationBuilder.DropTable(
                name: "user_release_notifications");
        }
    }
}
