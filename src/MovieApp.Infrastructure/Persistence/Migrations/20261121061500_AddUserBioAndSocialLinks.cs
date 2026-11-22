using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MovieApp.Infrastructure.Persistence;

#nullable disable

namespace MovieApp.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261121061500_AddUserBioAndSocialLinks")]
public partial class AddUserBioAndSocialLinks : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Bio",
            table: "users",
            type: "character varying(300)",
            maxLength: 300,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SocialX",
            table: "users",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SocialInstagram",
            table: "users",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SocialSnapchat",
            table: "users",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SocialTiktok",
            table: "users",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SocialYoutube",
            table: "users",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SocialTwitch",
            table: "users",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SocialThreads",
            table: "users",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SocialWebsite",
            table: "users",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "Bio", table: "users");
        migrationBuilder.DropColumn(name: "SocialX", table: "users");
        migrationBuilder.DropColumn(name: "SocialInstagram", table: "users");
        migrationBuilder.DropColumn(name: "SocialSnapchat", table: "users");
        migrationBuilder.DropColumn(name: "SocialTiktok", table: "users");
        migrationBuilder.DropColumn(name: "SocialYoutube", table: "users");
        migrationBuilder.DropColumn(name: "SocialTwitch", table: "users");
        migrationBuilder.DropColumn(name: "SocialThreads", table: "users");
        migrationBuilder.DropColumn(name: "SocialWebsite", table: "users");
    }
}
