using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MovieApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPushNotificationDeliveries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "push_notification_deliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserReleaseNotificationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PushDeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ExpoTicketId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    LastErrorCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    LastErrorMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeliveredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClaimedUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClaimToken = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_push_notification_deliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_push_notification_deliveries_push_devices_PushDeviceId",
                        column: x => x.PushDeviceId,
                        principalTable: "push_devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_push_notification_deliveries_user_release_notifications_Use~",
                        column: x => x.UserReleaseNotificationId,
                        principalTable: "user_release_notifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_push_notification_deliveries_ClaimedUntilUtc",
                table: "push_notification_deliveries",
                column: "ClaimedUntilUtc");

            migrationBuilder.CreateIndex(
                name: "IX_push_notification_deliveries_ExpoTicketId",
                table: "push_notification_deliveries",
                column: "ExpoTicketId");

            migrationBuilder.CreateIndex(
                name: "IX_push_notification_deliveries_PushDeviceId",
                table: "push_notification_deliveries",
                column: "PushDeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_push_notification_deliveries_Status",
                table: "push_notification_deliveries",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_push_notification_deliveries_Status_NextAttemptAtUtc",
                table: "push_notification_deliveries",
                columns: new[] { "Status", "NextAttemptAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_push_notification_deliveries_UserReleaseNotificationId_Push~",
                table: "push_notification_deliveries",
                columns: new[] { "UserReleaseNotificationId", "PushDeviceId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "push_notification_deliveries");
        }
    }
}
