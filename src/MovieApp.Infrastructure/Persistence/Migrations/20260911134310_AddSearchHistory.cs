using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MovieApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSearchHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "search_histories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Query = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NormalizedQuery = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SearchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_search_histories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_search_histories_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_search_histories_UserId_NormalizedQuery",
                table: "search_histories",
                columns: new[] { "UserId", "NormalizedQuery" });

            migrationBuilder.CreateIndex(
                name: "IX_search_histories_UserId_SearchedAt",
                table: "search_histories",
                columns: new[] { "UserId", "SearchedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "search_histories");
        }
    }
}
