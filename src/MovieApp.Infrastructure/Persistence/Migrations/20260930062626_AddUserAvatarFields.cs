using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MovieApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserAvatarFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomAvatarStorageKey",
                table: "users",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderPictureUrl",
                table: "user_external_logins",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomAvatarStorageKey",
                table: "users");

            migrationBuilder.DropColumn(
                name: "ProviderPictureUrl",
                table: "user_external_logins");
        }
    }
}
