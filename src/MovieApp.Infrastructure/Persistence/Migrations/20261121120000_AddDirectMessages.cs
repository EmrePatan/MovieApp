using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MovieApp.Infrastructure.Persistence;

#nullable disable

namespace MovieApp.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261121120000_AddDirectMessages")]
public partial class AddDirectMessages : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "direct_conversations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                LastMessageAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_direct_conversations", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "direct_conversation_participants",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                LastReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_direct_conversation_participants", x => x.Id);
                table.ForeignKey(
                    name: "FK_direct_conversation_participants_direct_conversations_ConversationId",
                    column: x => x.ConversationId,
                    principalTable: "direct_conversations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_direct_conversation_participants_users_UserId",
                    column: x => x.UserId,
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "direct_messages",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                SenderId = table.Column<Guid>(type: "uuid", nullable: false),
                Body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_direct_messages", x => x.Id);
                table.ForeignKey(
                    name: "FK_direct_messages_direct_conversations_ConversationId",
                    column: x => x.ConversationId,
                    principalTable: "direct_conversations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_direct_messages_users_SenderId",
                    column: x => x.SenderId,
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_direct_conversations_LastMessageAt",
            table: "direct_conversations",
            column: "LastMessageAt");

        migrationBuilder.CreateIndex(
            name: "IX_direct_conversation_participants_ConversationId_UserId",
            table: "direct_conversation_participants",
            columns: new[] { "ConversationId", "UserId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_direct_conversation_participants_UserId",
            table: "direct_conversation_participants",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_direct_messages_ConversationId_CreatedAt_Id",
            table: "direct_messages",
            columns: new[] { "ConversationId", "CreatedAt", "Id" });

        migrationBuilder.CreateIndex(
            name: "IX_direct_messages_SenderId",
            table: "direct_messages",
            column: "SenderId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "direct_messages");
        migrationBuilder.DropTable(name: "direct_conversation_participants");
        migrationBuilder.DropTable(name: "direct_conversations");
    }
}
