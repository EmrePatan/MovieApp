using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MovieApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserReleaseNotificationReadAtUtc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_user_release_notifications_UserId",
                table: "user_release_notifications");

            migrationBuilder.AddColumn<DateTime>(
                name: "ReadAtUtc",
                table: "user_release_notifications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_release_notifications_UserId",
                table: "user_release_notifications",
                column: "UserId",
                filter: "\"ReadAtUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_user_release_notifications_UserId_CreatedAtUtc_Id",
                table: "user_release_notifications",
                columns: new[] { "UserId", "CreatedAtUtc", "Id" },
                descending: new[] { false, true, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_user_release_notifications_UserId",
                table: "user_release_notifications");

            migrationBuilder.DropIndex(
                name: "IX_user_release_notifications_UserId_CreatedAtUtc_Id",
                table: "user_release_notifications");

            migrationBuilder.DropColumn(
                name: "ReadAtUtc",
                table: "user_release_notifications");

            migrationBuilder.CreateIndex(
                name: "IX_user_release_notifications_UserId",
                table: "user_release_notifications",
                column: "UserId");
        }
    }
}
