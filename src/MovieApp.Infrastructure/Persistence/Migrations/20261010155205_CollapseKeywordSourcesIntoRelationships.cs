using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MovieApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CollapseKeywordSourcesIntoRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Sources",
                table: "tv_show_keywords",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "Sources",
                table: "movie_keywords",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            // Keep the relationship tables as the single source of truth, but preserve every
            // provider membership before dropping the old provenance tables. INSERT ... ON
            // CONFLICT also protects against a legacy/inconsistent database where a source row
            // exists but its materialized relationship row is missing.
            migrationBuilder.Sql(
                """
                INSERT INTO movie_keywords ("MovieId", "KeywordId", "Sources")
                SELECT DISTINCT s."MovieId", s."KeywordId", '[]'::jsonb
                FROM movie_keyword_sources AS s
                ON CONFLICT ("MovieId", "KeywordId") DO NOTHING;

                UPDATE movie_keywords AS mk
                SET "Sources" = aggregated."Sources"
                FROM (
                    SELECT
                        s."MovieId",
                        s."KeywordId",
                        to_jsonb(array_agg(DISTINCT s."Provider" ORDER BY s."Provider")) AS "Sources"
                    FROM movie_keyword_sources AS s
                    GROUP BY s."MovieId", s."KeywordId"
                ) AS aggregated
                WHERE mk."MovieId" = aggregated."MovieId"
                  AND mk."KeywordId" = aggregated."KeywordId";

                INSERT INTO tv_show_keywords ("TvShowId", "KeywordId", "Sources")
                SELECT DISTINCT s."TvShowId", s."KeywordId", '[]'::jsonb
                FROM tv_show_keyword_sources AS s
                ON CONFLICT ("TvShowId", "KeywordId") DO NOTHING;

                UPDATE tv_show_keywords AS tk
                SET "Sources" = aggregated."Sources"
                FROM (
                    SELECT
                        s."TvShowId",
                        s."KeywordId",
                        to_jsonb(array_agg(DISTINCT s."Provider" ORDER BY s."Provider")) AS "Sources"
                    FROM tv_show_keyword_sources AS s
                    GROUP BY s."TvShowId", s."KeywordId"
                ) AS aggregated
                WHERE tk."TvShowId" = aggregated."TvShowId"
                  AND tk."KeywordId" = aggregated."KeywordId";

                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM movie_keywords WHERE "Sources" = '[]'::jsonb) THEN
                        RAISE EXCEPTION 'Cannot collapse movie keyword sources: at least one movie_keywords relationship has no provider source.';
                    END IF;

                    IF EXISTS (SELECT 1 FROM tv_show_keywords WHERE "Sources" = '[]'::jsonb) THEN
                        RAISE EXCEPTION 'Cannot collapse TV keyword sources: at least one tv_show_keywords relationship has no provider source.';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.DropTable(
                name: "movie_keyword_sources");

            migrationBuilder.DropTable(
                name: "tv_show_keyword_sources");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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

            // Timestamp history was intentionally retired by the forward migration. A rollback
            // can faithfully reconstruct provider membership, but not historical first/last-seen
            // timestamps, so those nullable legacy columns remain null.
            migrationBuilder.Sql(
                """
                INSERT INTO movie_keyword_sources ("MovieId", "KeywordId", "Provider", "FirstSeenAtUtc", "LastSeenAtUtc")
                SELECT mk."MovieId", mk."KeywordId", source.value, NULL, NULL
                FROM movie_keywords AS mk
                CROSS JOIN LATERAL jsonb_array_elements_text(mk."Sources") AS source(value);

                INSERT INTO tv_show_keyword_sources ("TvShowId", "KeywordId", "Provider", "FirstSeenAtUtc", "LastSeenAtUtc")
                SELECT tk."TvShowId", tk."KeywordId", source.value, NULL, NULL
                FROM tv_show_keywords AS tk
                CROSS JOIN LATERAL jsonb_array_elements_text(tk."Sources") AS source(value);
                """);

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

            migrationBuilder.DropColumn(
                name: "Sources",
                table: "tv_show_keywords");

            migrationBuilder.DropColumn(
                name: "Sources",
                table: "movie_keywords");
        }
    }
}
