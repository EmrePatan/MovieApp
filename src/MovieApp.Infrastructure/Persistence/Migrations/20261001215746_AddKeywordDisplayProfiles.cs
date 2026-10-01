using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MovieApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddKeywordDisplayProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "keyword_display_profiles",
                columns: table => new
                {
                    KeywordId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentFrequency = table.Column<int>(type: "integer", nullable: false),
                    MovieTitleCount = table.Column<int>(type: "integer", nullable: false),
                    TvTitleCount = table.Column<int>(type: "integer", nullable: false),
                    DisplayRank = table.Column<int>(type: "integer", nullable: false),
                    Displayable = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_keyword_display_profiles", x => x.KeywordId);
                    table.ForeignKey(
                        name: "FK_keyword_display_profiles_keywords_KeywordId",
                        column: x => x.KeywordId,
                        principalTable: "keywords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_keyword_display_profiles_Displayable_DisplayRank",
                table: "keyword_display_profiles",
                columns: new[] { "Displayable", "DisplayRank" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "keyword_display_profiles");
        }
    }
}
