using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MovieApp.Infrastructure.Persistence;

#nullable disable

namespace MovieApp.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261117100000_AddSocialActivities")]
public partial class AddSocialActivities : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "social_activities",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                ActivityType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                SourceEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                TargetContentId = table.Column<Guid>(type: "uuid", nullable: false),
                TargetContentType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_social_activities", x => x.Id);
                table.ForeignKey(
                    name: "FK_social_activities_users_ActorId",
                    column: x => x.ActorId,
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_social_activities_ActorId_ActivityType_SourceEntityId",
            table: "social_activities",
            columns: new[] { "ActorId", "ActivityType", "SourceEntityId" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_social_activities_CreatedAt_Id",
            table: "social_activities",
            columns: new[] { "CreatedAt", "Id" });
        migrationBuilder.CreateIndex(
            name: "IX_social_activities_ActorId_CreatedAt",
            table: "social_activities",
            columns: new[] { "ActorId", "CreatedAt" });
        migrationBuilder.CreateIndex(
            name: "IX_social_activities_TargetContentType_TargetContentId",
            table: "social_activities",
            columns: new[] { "TargetContentType", "TargetContentId" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "social_activities");
    }
}
