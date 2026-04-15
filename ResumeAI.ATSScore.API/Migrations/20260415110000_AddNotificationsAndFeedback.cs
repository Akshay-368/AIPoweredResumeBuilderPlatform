using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResumeAI.ATSScore.API.Migrations
{
    public partial class AddNotificationsAndFeedback : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "feedback",
                schema: "projects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    rating = table.Column<int>(type: "integer", nullable: false),
                    feedback_text = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feedback", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                schema: "projects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sender_user_id = table.Column<int>(type: "integer", nullable: false),
                    recipient_user_id = table.Column<int>(type: "integer", nullable: false),
                    subject = table.Column<string>(type: "text", nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notification_user_states",
                schema: "projects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    notification_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    is_read = table.Column<bool>(type: "boolean", nullable: false),
                    read_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted_for_user = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_user_states", x => x.id);
                    table.ForeignKey(
                        name: "FK_notification_user_states_notifications_notification_id",
                        column: x => x.notification_id,
                        principalSchema: "projects",
                        principalTable: "notifications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_feedback_created_at",
                schema: "projects",
                table: "feedback",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_feedback_user_id",
                schema: "projects",
                table: "feedback",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_created_at",
                schema: "projects",
                table: "notifications",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_recipient_user_id",
                schema: "projects",
                table: "notifications",
                column: "recipient_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_sender_user_id",
                schema: "projects",
                table: "notifications",
                column: "sender_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_notification_user_states_notification_id_user_id",
                schema: "projects",
                table: "notification_user_states",
                columns: new[] { "notification_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notification_user_states_user_id",
                schema: "projects",
                table: "notification_user_states",
                column: "user_id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "feedback",
                schema: "projects");

            migrationBuilder.DropTable(
                name: "notification_user_states",
                schema: "projects");

            migrationBuilder.DropTable(
                name: "notifications",
                schema: "projects");
        }
    }
}
