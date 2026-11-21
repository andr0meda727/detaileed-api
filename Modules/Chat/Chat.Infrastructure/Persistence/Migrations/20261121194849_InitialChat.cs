using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialChat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "chat");

            migrationBuilder.CreateTable(
                name: "conversation",
                schema: "chat",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    studio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    studio_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    studio_name_as_of = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    customer_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    last_message_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_message_preview = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    customer_read_up_to = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    studio_read_up_to = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("conversation_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "message",
                schema: "chat",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    studio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    side = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    sender_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    client_message_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("message_pkey", x => x.id);
                    table.ForeignKey(
                        name: "message_conversation_id_fkey",
                        column: x => x.conversation_id,
                        principalSchema: "chat",
                        principalTable: "conversation",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "conversation_studio_id_customer_user_id_key",
                schema: "chat",
                table: "conversation",
                columns: new[] { "studio_id", "customer_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_conversation_customer_user_id_last_message_at_id",
                schema: "chat",
                table: "conversation",
                columns: new[] { "customer_user_id", "last_message_at", "id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_conversation_studio_id_last_message_at_id",
                schema: "chat",
                table: "conversation",
                columns: new[] { "studio_id", "last_message_at", "id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_message_conversation_id_sent_at_id",
                schema: "chat",
                table: "message",
                columns: new[] { "conversation_id", "sent_at", "id" });

            migrationBuilder.CreateIndex(
                name: "message_conversation_id_sender_user_id_client_message_id_key",
                schema: "chat",
                table: "message",
                columns: new[] { "conversation_id", "sender_user_id", "client_message_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "message",
                schema: "chat");

            migrationBuilder.DropTable(
                name: "conversation",
                schema: "chat");
        }
    }
}
