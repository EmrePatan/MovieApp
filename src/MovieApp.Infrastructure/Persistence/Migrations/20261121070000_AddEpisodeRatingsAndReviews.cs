using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MovieApp.Infrastructure.Persistence;

#nullable disable

namespace MovieApp.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261121070000_AddEpisodeRatingsAndReviews")]
public partial class AddEpisodeRatingsAndReviews : Migration
{
    private const string CatalogReferenceSql =
        "(\"MovieId\" IS NOT NULL AND \"TvShowId\" IS NULL AND \"EpisodeId\" IS NULL) OR (\"MovieId\" IS NULL AND \"TvShowId\" IS NOT NULL AND \"EpisodeId\" IS NULL) OR (\"MovieId\" IS NULL AND \"TvShowId\" IS NULL AND \"EpisodeId\" IS NOT NULL)";

    private const string PreviousCatalogReferenceSql =
        "(\"MovieId\" IS NOT NULL AND \"TvShowId\" IS NULL) OR (\"MovieId\" IS NULL AND \"TvShowId\" IS NOT NULL)";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_ratings_catalog_reference",
            table: "ratings");

        migrationBuilder.DropCheckConstraint(
            name: "CK_reviews_catalog_reference",
            table: "reviews");

        migrationBuilder.AddColumn<Guid>(
            name: "EpisodeId",
            table: "ratings",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "EpisodeId",
            table: "reviews",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_ratings_EpisodeId",
            table: "ratings",
            column: "EpisodeId");

        migrationBuilder.CreateIndex(
            name: "IX_ratings_UserId_EpisodeId",
            table: "ratings",
            columns: new[] { "UserId", "EpisodeId" },
            unique: true,
            filter: "\"EpisodeId\" IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_reviews_EpisodeId",
            table: "reviews",
            column: "EpisodeId");

        migrationBuilder.CreateIndex(
            name: "IX_reviews_UserId_EpisodeId",
            table: "reviews",
            columns: new[] { "UserId", "EpisodeId" },
            unique: true,
            filter: "\"EpisodeId\" IS NOT NULL");

        migrationBuilder.AddForeignKey(
            name: "FK_ratings_episodes_EpisodeId",
            table: "ratings",
            column: "EpisodeId",
            principalTable: "episodes",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_reviews_episodes_EpisodeId",
            table: "reviews",
            column: "EpisodeId",
            principalTable: "episodes",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddCheckConstraint(
            name: "CK_ratings_catalog_reference",
            table: "ratings",
            sql: CatalogReferenceSql);

        migrationBuilder.AddCheckConstraint(
            name: "CK_reviews_catalog_reference",
            table: "reviews",
            sql: CatalogReferenceSql);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_ratings_episodes_EpisodeId",
            table: "ratings");

        migrationBuilder.DropForeignKey(
            name: "FK_reviews_episodes_EpisodeId",
            table: "reviews");

        migrationBuilder.DropIndex(
            name: "IX_ratings_EpisodeId",
            table: "ratings");

        migrationBuilder.DropIndex(
            name: "IX_ratings_UserId_EpisodeId",
            table: "ratings");

        migrationBuilder.DropIndex(
            name: "IX_reviews_EpisodeId",
            table: "reviews");

        migrationBuilder.DropIndex(
            name: "IX_reviews_UserId_EpisodeId",
            table: "reviews");

        migrationBuilder.DropCheckConstraint(
            name: "CK_ratings_catalog_reference",
            table: "ratings");

        migrationBuilder.DropCheckConstraint(
            name: "CK_reviews_catalog_reference",
            table: "reviews");

        migrationBuilder.DropColumn(
            name: "EpisodeId",
            table: "ratings");

        migrationBuilder.DropColumn(
            name: "EpisodeId",
            table: "reviews");

        migrationBuilder.AddCheckConstraint(
            name: "CK_ratings_catalog_reference",
            table: "ratings",
            sql: PreviousCatalogReferenceSql);

        migrationBuilder.AddCheckConstraint(
            name: "CK_reviews_catalog_reference",
            table: "reviews",
            sql: PreviousCatalogReferenceSql);
    }
}
