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
            migrationBuilder.DropTable(
                name: "movie_keyword_sources");

            migrationBuilder.DropTable(
                name: "tv_show_keyword_sources");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Sources",
                table: "tv_show_keywords");

            migrationBuilder.DropColumn(
                name: "Sources",
                table: "movie_keywords");

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
        }
    }
}
