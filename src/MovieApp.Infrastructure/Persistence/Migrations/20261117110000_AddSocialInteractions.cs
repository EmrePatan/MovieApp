using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MovieApp.Infrastructure.Persistence;

#nullable disable

namespace MovieApp.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261117110000_AddSocialInteractions")]
public partial class AddSocialInteractions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "social_activity_likes",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ActivityId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_social_activity_likes", x => x.Id);
                table.ForeignKey("FK_social_activity_likes_social_activities_ActivityId", x => x.ActivityId,
                    "social_activities", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_social_activity_likes_users_UserId", x => x.UserId,
                    "users", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "social_comments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ActivityId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                ParentCommentId = table.Column<Guid>(type: "uuid", nullable: true),
                Content = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_social_comments", x => x.Id);
                table.ForeignKey("FK_social_comments_social_activities_ActivityId", x => x.ActivityId,
                    "social_activities", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_social_comments_users_UserId", x => x.UserId,
                    "users", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_social_comments_social_comments_ParentCommentId", x => x.ParentCommentId,
                    "social_comments", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "social_reports",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ReporterId = table.Column<Guid>(type: "uuid", nullable: false),
                TargetType = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                Reason = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Details = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_social_reports", x => x.Id);
                table.ForeignKey("FK_social_reports_users_ReporterId", x => x.ReporterId,
                    "users", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "social_comment_likes",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CommentId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_social_comment_likes", x => x.Id);
                table.ForeignKey("FK_social_comment_likes_social_comments_CommentId", x => x.CommentId,
                    "social_comments", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_social_comment_likes_users_UserId", x => x.UserId,
                    "users", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex("IX_social_activity_likes_ActivityId_UserId", "social_activity_likes",
            new[] { "ActivityId", "UserId" }, unique: true);
        migrationBuilder.CreateIndex("IX_social_activity_likes_UserId", "social_activity_likes", "UserId");
        migrationBuilder.CreateIndex("IX_social_comments_ActivityId_CreatedAt_Id", "social_comments",
            new[] { "ActivityId", "CreatedAt", "Id" });
        migrationBuilder.CreateIndex("IX_social_comments_ParentCommentId_CreatedAt_Id", "social_comments",
            new[] { "ParentCommentId", "CreatedAt", "Id" });
        migrationBuilder.CreateIndex("IX_social_comments_UserId", "social_comments", "UserId");
        migrationBuilder.CreateIndex("IX_social_comment_likes_CommentId_UserId", "social_comment_likes",
            new[] { "CommentId", "UserId" }, unique: true);
        migrationBuilder.CreateIndex("IX_social_comment_likes_UserId", "social_comment_likes", "UserId");
        migrationBuilder.CreateIndex("IX_social_reports_ReporterId_CreatedAt", "social_reports",
            new[] { "ReporterId", "CreatedAt" });
        migrationBuilder.CreateIndex("IX_social_reports_TargetType_TargetId_CreatedAt", "social_reports",
            new[] { "TargetType", "TargetId", "CreatedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("social_comment_likes");
        migrationBuilder.DropTable("social_activity_likes");
        migrationBuilder.DropTable("social_reports");
        migrationBuilder.DropTable("social_comments");
    }
}
