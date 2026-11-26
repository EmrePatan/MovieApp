using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MovieApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogDetailExtras : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DetailExtrasJson",
                table: "tv_shows",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DetailExtrasJson",
                table: "movies",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DetailExtrasJson",
                table: "tv_shows");

            migrationBuilder.DropColumn(
                name: "DetailExtrasJson",
                table: "movies");
        }
    }
}
