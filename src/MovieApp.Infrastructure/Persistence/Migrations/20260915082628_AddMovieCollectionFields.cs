using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MovieApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMovieCollectionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CollectionBackdropPath",
                table: "movies",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CollectionName",
                table: "movies",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CollectionPosterPath",
                table: "movies",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TmdbCollectionId",
                table: "movies",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CollectionBackdropPath",
                table: "movies");

            migrationBuilder.DropColumn(
                name: "CollectionName",
                table: "movies");

            migrationBuilder.DropColumn(
                name: "CollectionPosterPath",
                table: "movies");

            migrationBuilder.DropColumn(
                name: "TmdbCollectionId",
                table: "movies");
        }
    }
}
