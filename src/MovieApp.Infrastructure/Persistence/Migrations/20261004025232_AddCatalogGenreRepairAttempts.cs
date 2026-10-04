using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MovieApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogGenreRepairAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "catalog_genre_repair_attempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CatalogId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    LastAttemptAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastOutcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    NextEligibleAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalog_genre_repair_attempts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_catalog_genre_repair_attempts_CatalogId_ContentType",
                table: "catalog_genre_repair_attempts",
                columns: new[] { "CatalogId", "ContentType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_catalog_genre_repair_attempts_ContentType_NextEligibleAtUtc",
                table: "catalog_genre_repair_attempts",
                columns: new[] { "ContentType", "NextEligibleAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "catalog_genre_repair_attempts");
        }
    }
}
