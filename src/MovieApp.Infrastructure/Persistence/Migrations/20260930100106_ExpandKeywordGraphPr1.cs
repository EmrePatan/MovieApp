using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MovieApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExpandKeywordGraphPr1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "MdbListKeywordsSyncedAtUtc",
                table: "tv_shows",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MdbListKeywordsSyncedAtUtc",
                table: "movies",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CanonicalName",
                table: "keywords",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ClassificationStatus",
                table: "keywords",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedName",
                table: "keywords",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SemanticCategory",
                table: "keywords",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "keyword_external_references",
                columns: table => new
                {
                    Provider = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    KeywordId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_keyword_external_references", x => new { x.Provider, x.ExternalId });
                    table.ForeignKey(
                        name: "FK_keyword_external_references_keywords_KeywordId",
                        column: x => x.KeywordId,
                        principalTable: "keywords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "movie_keyword_sources",
                columns: table => new
                {
                    MovieId = table.Column<Guid>(type: "uuid", nullable: false),
                    KeywordId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    FirstSeenAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSeenAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_movie_keyword_sources", x => new { x.MovieId, x.KeywordId, x.Provider });
                    table.ForeignKey(
                        name: "FK_movie_keyword_sources_keywords_KeywordId",
                        column: x => x.KeywordId,
                        principalTable: "keywords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_movie_keyword_sources_movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tv_show_keyword_sources",
                columns: table => new
                {
                    TvShowId = table.Column<Guid>(type: "uuid", nullable: false),
                    KeywordId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    FirstSeenAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSeenAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tv_show_keyword_sources", x => new { x.TvShowId, x.KeywordId, x.Provider });
                    table.ForeignKey(
                        name: "FK_tv_show_keyword_sources_keywords_KeywordId",
                        column: x => x.KeywordId,
                        principalTable: "keywords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tv_show_keyword_sources_tv_shows_TvShowId",
                        column: x => x.TvShowId,
                        principalTable: "tv_shows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_keywords_NormalizedName",
                table: "keywords",
                column: "NormalizedName");

            migrationBuilder.CreateIndex(
                name: "IX_keyword_external_references_KeywordId",
                table: "keyword_external_references",
                column: "KeywordId");

            migrationBuilder.CreateIndex(
                name: "IX_keyword_external_references_Provider_KeywordId",
                table: "keyword_external_references",
                columns: new[] { "Provider", "KeywordId" });

            migrationBuilder.CreateIndex(
                name: "IX_movie_keyword_sources_KeywordId",
                table: "movie_keyword_sources",
                column: "KeywordId");

            migrationBuilder.CreateIndex(
                name: "IX_movie_keyword_sources_MovieId_Provider",
                table: "movie_keyword_sources",
                columns: new[] { "MovieId", "Provider" });

            migrationBuilder.CreateIndex(
                name: "IX_tv_show_keyword_sources_KeywordId",
                table: "tv_show_keyword_sources",
                column: "KeywordId");

            migrationBuilder.CreateIndex(
                name: "IX_tv_show_keyword_sources_TvShowId_Provider",
                table: "tv_show_keyword_sources",
                columns: new[] { "TvShowId", "Provider" });

            migrationBuilder.Sql(
                """
                UPDATE keywords
                SET
                    "CanonicalName" = "Name",
                    "NormalizedName" = trim(both ' ' from regexp_replace(
                        replace(replace(lower(normalize("Name", NFKC)), '-', ' '), '_', ' '),
                        '\s+',
                        ' ',
                        'g')),
                    "SemanticCategory" = 0,
                    "ClassificationStatus" = 0;
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO keyword_external_references ("Provider", "ExternalId", "KeywordId", "ExternalName", "CreatedAt")
                SELECT 'Tmdb', "TmdbKeywordId"::text, "Id", "Name", "CreatedAt"
                FROM keywords;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "keyword_external_references");

            migrationBuilder.DropTable(
                name: "movie_keyword_sources");

            migrationBuilder.DropTable(
                name: "tv_show_keyword_sources");

            migrationBuilder.DropIndex(
                name: "IX_keywords_NormalizedName",
                table: "keywords");

            migrationBuilder.DropColumn(
                name: "MdbListKeywordsSyncedAtUtc",
                table: "tv_shows");

            migrationBuilder.DropColumn(
                name: "MdbListKeywordsSyncedAtUtc",
                table: "movies");

            migrationBuilder.DropColumn(
                name: "CanonicalName",
                table: "keywords");

            migrationBuilder.DropColumn(
                name: "ClassificationStatus",
                table: "keywords");

            migrationBuilder.DropColumn(
                name: "NormalizedName",
                table: "keywords");

            migrationBuilder.DropColumn(
                name: "SemanticCategory",
                table: "keywords");
        }
    }
}
