using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MovieApp.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261010170000_CollapseKeywordSourcesIntoRelationships")]
    public partial class CollapseKeywordSourcesIntoRelationships : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Sources",
                table: "movie_keywords",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "Sources",
                table: "tv_show_keywords",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.Sql(
                """
                INSERT INTO movie_keywords ("MovieId", "KeywordId", "Sources")
                SELECT DISTINCT source."MovieId", source."KeywordId", '[]'::jsonb
                FROM movie_keyword_sources AS source
                ON CONFLICT ("MovieId", "KeywordId") DO NOTHING;

                UPDATE movie_keywords AS relationship
                SET "Sources" = COALESCE(
                    (
                        SELECT jsonb_agg(provider."Provider" ORDER BY provider."Provider")
                        FROM (
                            SELECT DISTINCT source."Provider"
                            FROM movie_keyword_sources AS source
                            WHERE source."MovieId" = relationship."MovieId"
                              AND source."KeywordId" = relationship."KeywordId"
                        ) AS provider
                    ),
                    '[]'::jsonb);

                INSERT INTO tv_show_keywords ("TvShowId", "KeywordId", "Sources")
                SELECT DISTINCT source."TvShowId", source."KeywordId", '[]'::jsonb
                FROM tv_show_keyword_sources AS source
                ON CONFLICT ("TvShowId", "KeywordId") DO NOTHING;

                UPDATE tv_show_keywords AS relationship
                SET "Sources" = COALESCE(
                    (
                        SELECT jsonb_agg(provider."Provider" ORDER BY provider."Provider")
                        FROM (
                            SELECT DISTINCT source."Provider"
                            FROM tv_show_keyword_sources AS source
                            WHERE source."TvShowId" = relationship."TvShowId"
                              AND source."KeywordId" = relationship."KeywordId"
                        ) AS provider
                    ),
                    '[]'::jsonb);
                """);

            migrationBuilder.DropTable(name: "movie_keyword_sources");
            migrationBuilder.DropTable(name: "tv_show_keyword_sources");
        }

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
                INSERT INTO movie_keyword_sources ("MovieId", "KeywordId", "Provider", "FirstSeenAtUtc", "LastSeenAtUtc")
                SELECT relationship."MovieId", relationship."KeywordId", source.value, NULL, NULL
                FROM movie_keywords AS relationship
                CROSS JOIN LATERAL jsonb_array_elements_text(relationship."Sources") AS source(value);

                INSERT INTO tv_show_keyword_sources ("TvShowId", "KeywordId", "Provider", "FirstSeenAtUtc", "LastSeenAtUtc")
                SELECT relationship."TvShowId", relationship."KeywordId", source.value, NULL, NULL
                FROM tv_show_keywords AS relationship
                CROSS JOIN LATERAL jsonb_array_elements_text(relationship."Sources") AS source(value);
                """);

            migrationBuilder.DropColumn(name: "Sources", table: "movie_keywords");
            migrationBuilder.DropColumn(name: "Sources", table: "tv_show_keywords");
        }
    }
}
