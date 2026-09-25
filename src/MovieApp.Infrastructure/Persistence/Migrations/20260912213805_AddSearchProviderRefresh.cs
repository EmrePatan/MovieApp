using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MovieApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSearchProviderRefresh : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "search_provider_refreshes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NormalizedQuery = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Page = table.Column<int>(type: "integer", nullable: false),
                    LastRefreshedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_search_provider_refreshes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_search_provider_refreshes_NormalizedQuery_ContentType_Page",
                table: "search_provider_refreshes",
                columns: new[] { "NormalizedQuery", "ContentType", "Page" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "search_provider_refreshes");
        }
    }
}
