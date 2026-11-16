using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MovieApp.Infrastructure.Persistence;

#nullable disable

namespace MovieApp.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261117120000_AddSocialComments")]
public partial class AddSocialComments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "social_comments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ActivityId = table.Column<Guid>(type: "uuid", nullable: false),
                AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                ParentCommentId = table.Column<Guid>(type: "uuid", nullable: true),
                Content = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_social_comments", x => x.Id);
                table.CheckConstraint("CK_social_comments_not_self_parent", "\"ParentCommentId\" IS NULL OR \"ParentCommentId\" <> \"Id\"");
                table.ForeignKey("FK_social_comments_social_activities_ActivityId", x => x.ActivityId, "social_activities", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_social_comments_users_AuthorId", x => x.AuthorId, "users", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_social_comments_social_comments_ParentCommentId", x => x.ParentCommentId, "social_comments", "Id", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex("IX_social_comments_ActivityId_CreatedAt_Id", "social_comments", new[] { "ActivityId", "CreatedAt", "Id" });
        migrationBuilder.CreateIndex("IX_social_comments_ParentCommentId", "social_comments", "ParentCommentId");
        migrationBuilder.CreateIndex("IX_social_comments_AuthorId_CreatedAt", "social_comments", new[] { "AuthorId", "CreatedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "social_comments");
}