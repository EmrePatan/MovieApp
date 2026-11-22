using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MovieApp.Infrastructure.Persistence;

#nullable disable

namespace MovieApp.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261122090000_AddDirectConversationHistoryClearedAt")]
public partial class AddDirectConversationHistoryClearedAt : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "HistoryClearedAt",
            table: "direct_conversation_participants",
            type: "timestamp with time zone",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "HistoryClearedAt",
            table: "direct_conversation_participants");
    }
}
