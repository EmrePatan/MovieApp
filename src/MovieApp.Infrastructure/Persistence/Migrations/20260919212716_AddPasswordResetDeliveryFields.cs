using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MovieApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordResetDeliveryFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContentLocale",
                table: "password_reset_tokens",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "en-US");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeliveryCompletedAtUtc",
                table: "password_reset_tokens",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProtectedDeliverySecret",
                table: "password_reset_tokens",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContentLocale",
                table: "password_reset_tokens");

            migrationBuilder.DropColumn(
                name: "DeliveryCompletedAtUtc",
                table: "password_reset_tokens");

            migrationBuilder.DropColumn(
                name: "ProtectedDeliverySecret",
                table: "password_reset_tokens");
        }
    }
}
