using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MovieApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddKeywordLocalizationGovernance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReviewStatus",
                table: "keyword_localizations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SourceTextHash",
                table: "keyword_localizations",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "TranslationSource",
                table: "keyword_localizations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Rows that existed before governance metadata must not become stale Machine+Unreviewed
            // with an empty SourceTextHash and get overwritten by automated backfill.
            migrationBuilder.Sql(
                """
                UPDATE keyword_localizations
                SET "ReviewStatus" = 1
                WHERE "ReviewStatus" = 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReviewStatus",
                table: "keyword_localizations");

            migrationBuilder.DropColumn(
                name: "SourceTextHash",
                table: "keyword_localizations");

            migrationBuilder.DropColumn(
                name: "TranslationSource",
                table: "keyword_localizations");
        }
    }
}
