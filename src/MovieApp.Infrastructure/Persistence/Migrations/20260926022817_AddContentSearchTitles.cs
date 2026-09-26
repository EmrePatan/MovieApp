using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MovieApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddContentSearchTitles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "content_search_titles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentType = table.Column<int>(type: "integer", nullable: false),
                    ContentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    NormalizedTitle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    TitleKind = table.Column<int>(type: "integer", nullable: false),
                    LanguageCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CountryCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    ProviderTitleType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ProviderUpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_content_search_titles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_content_search_titles_ContentType_ContentId",
                table: "content_search_titles",
                columns: new[] { "ContentType", "ContentId" });

            migrationBuilder.CreateIndex(
                name: "UX_content_search_titles_ContentType_ContentId_NormalizedTitle",
                table: "content_search_titles",
                columns: new[] { "ContentType", "ContentId", "NormalizedTitle" },
                unique: true);

            migrationBuilder.Sql(
                """
                CREATE EXTENSION IF NOT EXISTS pg_trgm;
                """);

            migrationBuilder.Sql(
                """
                CREATE INDEX "IX_content_search_titles_NormalizedTitle_trgm_gin"
                    ON content_search_titles USING gin ("NormalizedTitle" gin_trgm_ops);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP INDEX IF EXISTS "IX_content_search_titles_NormalizedTitle_trgm_gin";
                """);

            migrationBuilder.DropTable(
                name: "content_search_titles");
        }
    }
}
