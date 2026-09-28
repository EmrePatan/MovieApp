using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MovieApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWatchlistIdToUserWatchlistShare : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "WatchlistId",
                table: "user_watchlist_shares",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_user_watchlist_shares_WatchlistId",
                table: "user_watchlist_shares",
                column: "WatchlistId");

            migrationBuilder.AddForeignKey(
                name: "FK_user_watchlist_shares_watchlists_WatchlistId",
                table: "user_watchlist_shares",
                column: "WatchlistId",
                principalTable: "watchlists",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_user_watchlist_shares_watchlists_WatchlistId",
                table: "user_watchlist_shares");

            migrationBuilder.DropIndex(
                name: "IX_user_watchlist_shares_WatchlistId",
                table: "user_watchlist_shares");

            migrationBuilder.DropColumn(
                name: "WatchlistId",
                table: "user_watchlist_shares");
        }
    }
}
