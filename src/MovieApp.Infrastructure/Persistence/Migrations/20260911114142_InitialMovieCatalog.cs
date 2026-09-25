using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MovieApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialMovieCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "genres",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_genres", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "movies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TmdbId = table.Column<int>(type: "integer", nullable: true),
                    TvdbId = table.Column<int>(type: "integer", nullable: true),
                    ImdbId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    OriginalTitle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Overview = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ReleaseDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RuntimeMinutes = table.Column<int>(type: "integer", nullable: true),
                    PosterPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    BackdropPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OriginalLanguage = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    VoteAverage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    VoteCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_movies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "people",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TmdbId = table.Column<int>(type: "integer", nullable: true),
                    TvdbId = table.Column<int>(type: "integer", nullable: true),
                    ImdbId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProfilePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_people", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tv_shows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TmdbId = table.Column<int>(type: "integer", nullable: true),
                    TvdbId = table.Column<int>(type: "integer", nullable: true),
                    ImdbId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    OriginalTitle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Overview = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    FirstAirDate = table.Column<DateOnly>(type: "date", nullable: true),
                    LastAirDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PosterPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    BackdropPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OriginalLanguage = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    VoteAverage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    VoteCount = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tv_shows", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "movie_genres",
                columns: table => new
                {
                    MovieId = table.Column<Guid>(type: "uuid", nullable: false),
                    GenreId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_movie_genres", x => new { x.MovieId, x.GenreId });
                    table.ForeignKey(
                        name: "FK_movie_genres_genres_GenreId",
                        column: x => x.GenreId,
                        principalTable: "genres",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_movie_genres_movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "movie_people",
                columns: table => new
                {
                    MovieId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Job = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, defaultValue: ""),
                    CreditType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Character = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Department = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_movie_people", x => new { x.MovieId, x.PersonId, x.CreditType, x.Job });
                    table.ForeignKey(
                        name: "FK_movie_people_movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_movie_people_people_PersonId",
                        column: x => x.PersonId,
                        principalTable: "people",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "seasons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TvShowId = table.Column<Guid>(type: "uuid", nullable: false),
                    TmdbId = table.Column<int>(type: "integer", nullable: true),
                    TvdbId = table.Column<int>(type: "integer", nullable: true),
                    SeasonNumber = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Overview = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    AirDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EpisodeCount = table.Column<int>(type: "integer", nullable: true),
                    PosterPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_seasons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_seasons_tv_shows_TvShowId",
                        column: x => x.TvShowId,
                        principalTable: "tv_shows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tv_show_genres",
                columns: table => new
                {
                    TvShowId = table.Column<Guid>(type: "uuid", nullable: false),
                    GenreId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tv_show_genres", x => new { x.TvShowId, x.GenreId });
                    table.ForeignKey(
                        name: "FK_tv_show_genres_genres_GenreId",
                        column: x => x.GenreId,
                        principalTable: "genres",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tv_show_genres_tv_shows_TvShowId",
                        column: x => x.TvShowId,
                        principalTable: "tv_shows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tv_show_people",
                columns: table => new
                {
                    TvShowId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Job = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, defaultValue: ""),
                    CreditType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Character = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Department = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tv_show_people", x => new { x.TvShowId, x.PersonId, x.CreditType, x.Job });
                    table.ForeignKey(
                        name: "FK_tv_show_people_people_PersonId",
                        column: x => x.PersonId,
                        principalTable: "people",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tv_show_people_tv_shows_TvShowId",
                        column: x => x.TvShowId,
                        principalTable: "tv_shows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "episodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    TmdbId = table.Column<int>(type: "integer", nullable: true),
                    TvdbId = table.Column<int>(type: "integer", nullable: true),
                    ImdbId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    EpisodeNumber = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Overview = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    AirDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RuntimeMinutes = table.Column<int>(type: "integer", nullable: true),
                    StillPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    VoteAverage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    VoteCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_episodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_episodes_seasons_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "seasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_episodes_ImdbId",
                table: "episodes",
                column: "ImdbId",
                unique: true,
                filter: "\"ImdbId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_episodes_SeasonId_EpisodeNumber",
                table: "episodes",
                columns: new[] { "SeasonId", "EpisodeNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_episodes_TmdbId",
                table: "episodes",
                column: "TmdbId",
                unique: true,
                filter: "\"TmdbId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_episodes_TvdbId",
                table: "episodes",
                column: "TvdbId",
                unique: true,
                filter: "\"TvdbId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_genres_Name",
                table: "genres",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_movie_genres_GenreId",
                table: "movie_genres",
                column: "GenreId");

            migrationBuilder.CreateIndex(
                name: "IX_movie_people_PersonId",
                table: "movie_people",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_movies_ImdbId",
                table: "movies",
                column: "ImdbId",
                unique: true,
                filter: "\"ImdbId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_movies_ReleaseDate",
                table: "movies",
                column: "ReleaseDate");

            migrationBuilder.CreateIndex(
                name: "IX_movies_Title",
                table: "movies",
                column: "Title");

            migrationBuilder.CreateIndex(
                name: "IX_movies_TmdbId",
                table: "movies",
                column: "TmdbId",
                unique: true,
                filter: "\"TmdbId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_movies_TvdbId",
                table: "movies",
                column: "TvdbId",
                unique: true,
                filter: "\"TvdbId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_people_ImdbId",
                table: "people",
                column: "ImdbId",
                unique: true,
                filter: "\"ImdbId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_people_Name",
                table: "people",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_people_TmdbId",
                table: "people",
                column: "TmdbId",
                unique: true,
                filter: "\"TmdbId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_people_TvdbId",
                table: "people",
                column: "TvdbId",
                unique: true,
                filter: "\"TvdbId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_seasons_TmdbId",
                table: "seasons",
                column: "TmdbId",
                unique: true,
                filter: "\"TmdbId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_seasons_TvdbId",
                table: "seasons",
                column: "TvdbId",
                unique: true,
                filter: "\"TvdbId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_seasons_TvShowId_SeasonNumber",
                table: "seasons",
                columns: new[] { "TvShowId", "SeasonNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tv_show_genres_GenreId",
                table: "tv_show_genres",
                column: "GenreId");

            migrationBuilder.CreateIndex(
                name: "IX_tv_show_people_PersonId",
                table: "tv_show_people",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_tv_shows_FirstAirDate",
                table: "tv_shows",
                column: "FirstAirDate");

            migrationBuilder.CreateIndex(
                name: "IX_tv_shows_ImdbId",
                table: "tv_shows",
                column: "ImdbId",
                unique: true,
                filter: "\"ImdbId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_tv_shows_Status",
                table: "tv_shows",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_tv_shows_Title",
                table: "tv_shows",
                column: "Title");

            migrationBuilder.CreateIndex(
                name: "IX_tv_shows_TmdbId",
                table: "tv_shows",
                column: "TmdbId",
                unique: true,
                filter: "\"TmdbId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_tv_shows_TvdbId",
                table: "tv_shows",
                column: "TvdbId",
                unique: true,
                filter: "\"TvdbId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "episodes");

            migrationBuilder.DropTable(
                name: "movie_genres");

            migrationBuilder.DropTable(
                name: "movie_people");

            migrationBuilder.DropTable(
                name: "tv_show_genres");

            migrationBuilder.DropTable(
                name: "tv_show_people");

            migrationBuilder.DropTable(
                name: "seasons");

            migrationBuilder.DropTable(
                name: "movies");

            migrationBuilder.DropTable(
                name: "genres");

            migrationBuilder.DropTable(
                name: "people");

            migrationBuilder.DropTable(
                name: "tv_shows");
        }
    }
}
