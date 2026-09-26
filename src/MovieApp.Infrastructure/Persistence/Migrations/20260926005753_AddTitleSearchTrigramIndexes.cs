using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MovieApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTitleSearchTrigramIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE EXTENSION IF NOT EXISTS pg_trgm;
                """);

            migrationBuilder.Sql(
                """
                CREATE INDEX "IX_movies_Title_trgm_gin"
                    ON movies USING gin ("Title" gin_trgm_ops);
                """);

            migrationBuilder.Sql(
                """
                CREATE INDEX "IX_movies_OriginalTitle_trgm_gin"
                    ON movies USING gin ("OriginalTitle" gin_trgm_ops);
                """);

            migrationBuilder.Sql(
                """
                CREATE INDEX "IX_tv_shows_Title_trgm_gin"
                    ON tv_shows USING gin ("Title" gin_trgm_ops);
                """);

            migrationBuilder.Sql(
                """
                CREATE INDEX "IX_tv_shows_OriginalTitle_trgm_gin"
                    ON tv_shows USING gin ("OriginalTitle" gin_trgm_ops);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP INDEX IF EXISTS "IX_tv_shows_OriginalTitle_trgm_gin";
                """);

            migrationBuilder.Sql(
                """
                DROP INDEX IF EXISTS "IX_tv_shows_Title_trgm_gin";
                """);

            migrationBuilder.Sql(
                """
                DROP INDEX IF EXISTS "IX_movies_OriginalTitle_trgm_gin";
                """);

            migrationBuilder.Sql(
                """
                DROP INDEX IF EXISTS "IX_movies_Title_trgm_gin";
                """);
        }
    }
}
