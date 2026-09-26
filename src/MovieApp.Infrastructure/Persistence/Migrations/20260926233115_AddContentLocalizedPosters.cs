using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MovieApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddContentLocalizedPosters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "content_localized_posters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentType = table.Column<int>(type: "integer", nullable: false),
                    ContentId = table.Column<Guid>(type: "uuid", nullable: false),
                    LanguageKey = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    PosterPath = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_content_localized_posters", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_content_localized_posters_LanguageKey_ContentType_ContentId",
                table: "content_localized_posters",
                columns: new[] { "LanguageKey", "ContentType", "ContentId" });

            migrationBuilder.CreateIndex(
                name: "UX_content_localized_posters_ContentType_ContentId_LanguageKey",
                table: "content_localized_posters",
                columns: new[] { "ContentType", "ContentId", "LanguageKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "content_localized_posters");
        }
    }
}
