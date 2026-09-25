using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MovieApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogFollows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_user_release_notifications_UserId_TvShowId_NotificationType~",
                table: "user_release_notifications");

            migrationBuilder.AlterColumn<Guid>(
                name: "TvShowId",
                table: "user_release_notifications",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "MovieId",
                table: "user_release_notifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TvShowId",
                table: "catalog_release_events",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "MovieId",
                table: "catalog_release_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "catalog_follows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ContentId = table.Column<Guid>(type: "uuid", nullable: false),
                    NotifyMovieRelease = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    NotifyNewSeasons = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    NotifyNewEpisodes = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    NotifyFromUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BaselineEstablishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalog_follows", x => x.Id);
                    table.CheckConstraint("CK_catalog_follows_content_type", "\"ContentType\" IN ('Movie', 'Tv')");
                    table.ForeignKey(
                        name: "FK_catalog_follows_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                INSERT INTO catalog_follows (
                    "Id",
                    "UserId",
                    "ContentType",
                    "ContentId",
                    "NotifyMovieRelease",
                    "NotifyNewSeasons",
                    "NotifyNewEpisodes",
                    "NotifyFromUtc",
                    "BaselineEstablishedAtUtc",
                    "CreatedAt",
                    "UpdatedAt")
                SELECT
                    "Id",
                    "UserId",
                    'Tv',
                    "TvShowId",
                    FALSE,
                    "NotifyNewSeasons",
                    "NotifyNewEpisodes",
                    "NotifyFromUtc",
                    "BaselineEstablishedAtUtc",
                    "CreatedAt",
                    "UpdatedAt"
                FROM tv_show_follows
                """);

            migrationBuilder.DropTable(
                name: "tv_show_follows");

            migrationBuilder.CreateIndex(
                name: "IX_user_release_notifications_MovieId",
                table: "user_release_notifications",
                column: "MovieId");

            migrationBuilder.CreateIndex(
                name: "IX_user_release_notifications_UserId_TvShowId_MovieId_Notifica~",
                table: "user_release_notifications",
                columns: new[] { "UserId", "TvShowId", "MovieId", "NotificationType", "AggregationWindowKey" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_user_release_notifications_content_ref",
                table: "user_release_notifications",
                sql: "(\"TvShowId\" IS NOT NULL AND \"MovieId\" IS NULL) OR (\"TvShowId\" IS NULL AND \"MovieId\" IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_catalog_release_events_MovieId",
                table: "catalog_release_events",
                column: "MovieId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_catalog_release_events_content_ref",
                table: "catalog_release_events",
                sql: "(\"TvShowId\" IS NOT NULL AND \"MovieId\" IS NULL) OR (\"TvShowId\" IS NULL AND \"MovieId\" IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_catalog_follows_ContentType_ContentId",
                table: "catalog_follows",
                columns: new[] { "ContentType", "ContentId" });

            migrationBuilder.CreateIndex(
                name: "IX_catalog_follows_UserId",
                table: "catalog_follows",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_catalog_follows_UserId_ContentType_ContentId",
                table: "catalog_follows",
                columns: new[] { "UserId", "ContentType", "ContentId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_catalog_release_events_movies_MovieId",
                table: "catalog_release_events",
                column: "MovieId",
                principalTable: "movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_user_release_notifications_movies_MovieId",
                table: "user_release_notifications",
                column: "MovieId",
                principalTable: "movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_catalog_release_events_movies_MovieId",
                table: "catalog_release_events");

            migrationBuilder.DropForeignKey(
                name: "FK_user_release_notifications_movies_MovieId",
                table: "user_release_notifications");

            migrationBuilder.CreateTable(
                name: "tv_show_follows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TvShowId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BaselineEstablishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NotifyFromUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NotifyNewEpisodes = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    NotifyNewSeasons = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
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

            migrationBuilder.Sql("""
                INSERT INTO tv_show_follows (
                    "Id",
                    "UserId",
                    "TvShowId",
                    "NotifyNewSeasons",
                    "NotifyNewEpisodes",
                    "NotifyFromUtc",
                    "BaselineEstablishedAtUtc",
                    "CreatedAt",
                    "UpdatedAt")
                SELECT
                    "Id",
                    "UserId",
                    "ContentId",
                    "NotifyNewSeasons",
                    "NotifyNewEpisodes",
                    "NotifyFromUtc",
                    "BaselineEstablishedAtUtc",
                    "CreatedAt",
                    "UpdatedAt"
                FROM catalog_follows
                WHERE "ContentType" = 'Tv'
                """);

            migrationBuilder.DropTable(
                name: "catalog_follows");

            migrationBuilder.DropIndex(
                name: "IX_user_release_notifications_MovieId",
                table: "user_release_notifications");

            migrationBuilder.DropIndex(
                name: "IX_user_release_notifications_UserId_TvShowId_MovieId_Notifica~",
                table: "user_release_notifications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_user_release_notifications_content_ref",
                table: "user_release_notifications");

            migrationBuilder.DropIndex(
                name: "IX_catalog_release_events_MovieId",
                table: "catalog_release_events");

            migrationBuilder.DropCheckConstraint(
                name: "CK_catalog_release_events_content_ref",
                table: "catalog_release_events");

            migrationBuilder.DropColumn(
                name: "MovieId",
                table: "user_release_notifications");

            migrationBuilder.DropColumn(
                name: "MovieId",
                table: "catalog_release_events");

            migrationBuilder.Sql("""
                DELETE FROM catalog_release_events WHERE "MovieId" IS NOT NULL
                """);

            migrationBuilder.Sql("""
                DELETE FROM user_release_notifications WHERE "MovieId" IS NOT NULL
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "TvShowId",
                table: "user_release_notifications",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TvShowId",
                table: "catalog_release_events",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_release_notifications_UserId_TvShowId_NotificationType~",
                table: "user_release_notifications",
                columns: new[] { "UserId", "TvShowId", "NotificationType", "AggregationWindowKey" },
                unique: true);

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
        }
    }
}
