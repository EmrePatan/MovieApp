using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MovieApp.Infrastructure.Persistence;

#nullable disable

namespace MovieApp.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261117130000_AddSocialNotifications")]
public partial class AddSocialNotifications : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "social_notifications",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                RecipientId = table.Column<Guid>(type: "uuid", nullable: false),
                ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                NotificationType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                ActivityId = table.Column<Guid>(type: "uuid", nullable: true),
                CommentId = table.Column<Guid>(type: "uuid", nullable: true),
                FollowId = table.Column<Guid>(type: "uuid", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_social_notifications", x => x.Id);
                table.ForeignKey("FK_social_notifications_users_RecipientId", x => x.RecipientId, "users", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_social_notifications_users_ActorId", x => x.ActorId, "users", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_social_notifications_social_activities_ActivityId", x => x.ActivityId, "social_activities", "Id", onDelete: ReferentialAction.SetNull);
                table.ForeignKey("FK_social_notifications_social_comments_CommentId", x => x.CommentId, "social_comments", "Id", onDelete: ReferentialAction.SetNull);
                table.ForeignKey("FK_social_notifications_user_follows_FollowId", x => x.FollowId, "user_follows", "Id", onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateIndex("IX_social_notifications_RecipientId_CreatedAt_Id", "social_notifications",
            new[] { "RecipientId", "CreatedAt", "Id" });
        migrationBuilder.CreateIndex("IX_social_notifications_ActorId", "social_notifications", "ActorId");
        migrationBuilder.CreateIndex("IX_social_notifications_ActivityId", "social_notifications", "ActivityId");
        migrationBuilder.CreateIndex("IX_social_notifications_CommentId", "social_notifications", "CommentId");
        migrationBuilder.CreateIndex("IX_social_notifications_FollowId", "social_notifications", "FollowId");
        migrationBuilder.CreateIndex("IX_social_notifications_RecipientId", "social_notifications", "RecipientId")
            .Annotation("Npgsql:IndexFilter", "\"ReadAt\" IS NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("social_notifications");
    }
}
