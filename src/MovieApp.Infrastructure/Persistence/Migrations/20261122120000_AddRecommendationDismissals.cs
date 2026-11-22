using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MovieApp.Infrastructure.Persistence;

#nullable disable

namespace MovieApp.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261122120000_AddRecommendationDismissals")]
public partial class AddRecommendationDismissals : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "recommendation_dismissals",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                MovieId = table.Column<Guid>(type: "uuid", nullable: true),
                TvShowId = table.Column<Guid>(type: "uuid", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_recommendation_dismissals", x => x.Id);
                table.ForeignKey(
                    name: "FK_recommendation_dismissals_movies_MovieId",
                    column: x => x.MovieId,
                    principalTable: "movies",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_recommendation_dismissals_tv_shows_TvShowId",
                    column: x => x.TvShowId,
                    principalTable: "tv_shows",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_recommendation_dismissals_users_UserId",
                    column: x => x.UserId,
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_recommendation_dismissals_MovieId",
            table: "recommendation_dismissals",
            column: "MovieId");

        migrationBuilder.CreateIndex(
            name: "IX_recommendation_dismissals_TvShowId",
            table: "recommendation_dismissals",
            column: "TvShowId");

        migrationBuilder.CreateIndex(
            name: "IX_recommendation_dismissals_UserId",
            table: "recommendation_dismissals",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_recommendation_dismissals_UserId_MovieId",
            table: "recommendation_dismissals",
            columns: new[] { "UserId", "MovieId" },
            unique: true,
            filter: "\"MovieId\" IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_recommendation_dismissals_UserId_TvShowId",
            table: "recommendation_dismissals",
            columns: new[] { "UserId", "TvShowId" },
            unique: true,
            filter: "\"TvShowId\" IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "recommendation_dismissals");
    }
}
