using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MovieApp.Infrastructure.Persistence;

#nullable disable

namespace MovieApp.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261117090000_AddSocialGraphFoundation")]
public partial class AddSocialGraphFoundation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "user_privacy_settings",
            columns: table => new
            {
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                IsPrivate = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                ShowWatched = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                ShowRatings = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                ShowFavorites = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                ShowReviews = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_user_privacy_settings", x => x.UserId);
                table.ForeignKey(
                    name: "FK_user_privacy_settings_users_UserId",
                    column: x => x.UserId,
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "user_follows",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                FollowerId = table.Column<Guid>(type: "uuid", nullable: false),
                FollowedUserId = table.Column<Guid>(type: "uuid", nullable: false),
                Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                RespondedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_user_follows", x => x.Id);
                table.CheckConstraint("CK_user_follows_distinct_users", "\"FollowerId\" <> \"FollowedUserId\"");
                table.ForeignKey(
                    name: "FK_user_follows_users_FollowedUserId",
                    column: x => x.FollowedUserId,
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_user_follows_users_FollowerId",
                    column: x => x.FollowerId,
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "user_blocks",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                BlockerId = table.Column<Guid>(type: "uuid", nullable: false),
                BlockedUserId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_user_blocks", x => x.Id);
                table.CheckConstraint("CK_user_blocks_distinct_users", "\"BlockerId\" <> \"BlockedUserId\"");
                table.ForeignKey(
                    name: "FK_user_blocks_users_BlockedUserId",
                    column: x => x.BlockedUserId,
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_user_blocks_users_BlockerId",
                    column: x => x.BlockerId,
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_user_follows_FollowerId_FollowedUserId",
            table: "user_follows",
            columns: new[] { "FollowerId", "FollowedUserId" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_user_follows_FollowerId_Status_CreatedAt",
            table: "user_follows",
            columns: new[] { "FollowerId", "Status", "CreatedAt" });
        migrationBuilder.CreateIndex(
            name: "IX_user_follows_FollowedUserId_Status_CreatedAt",
            table: "user_follows",
            columns: new[] { "FollowedUserId", "Status", "CreatedAt" });
        migrationBuilder.CreateIndex(
            name: "IX_user_blocks_BlockerId_BlockedUserId",
            table: "user_blocks",
            columns: new[] { "BlockerId", "BlockedUserId" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_user_blocks_BlockedUserId",
            table: "user_blocks",
            column: "BlockedUserId");

        migrationBuilder.Sql("""
            INSERT INTO user_privacy_settings (
                "UserId", "IsPrivate", "ShowWatched", "ShowRatings",
                "ShowFavorites", "ShowReviews", "UpdatedAt")
            SELECT
                "Id", FALSE, TRUE, TRUE, TRUE, TRUE, CURRENT_TIMESTAMP
            FROM users
            ON CONFLICT ("UserId") DO NOTHING;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "user_blocks");
        migrationBuilder.DropTable(name: "user_follows");
        migrationBuilder.DropTable(name: "user_privacy_settings");
    }
}
