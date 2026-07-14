using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XanhNow.Auth.Login.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialAuthLoginSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "auth_audit_logs",
                schema: "auth",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    event_type = table.Column<string>(type: "text", nullable: false),
                    severity = table.Column<string>(type: "text", nullable: false),
                    phone_number_masked = table.Column<string>(type: "text", nullable: true),
                    session_id_hash = table.Column<string>(type: "text", nullable: true),
                    ip_hash = table.Column<string>(type: "text", nullable: true),
                    metadata_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    correlation_id = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_auth_audit_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "login_attempts",
                schema: "auth",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    phone_number_hash = table.Column<string>(type: "text", nullable: false),
                    phone_number_masked = table.Column<string>(type: "text", nullable: false),
                    ip_hash = table.Column<string>(type: "text", nullable: false),
                    client_info_hash = table.Column<string>(type: "text", nullable: true),
                    result = table.Column<string>(type: "text", nullable: false),
                    failure_reason_code = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    correlation_id = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_login_attempts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_events",
                schema: "auth",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "text", nullable: false),
                    aggregate_type = table.Column<string>(type: "text", nullable: false),
                    aggregate_id = table.Column<string>(type: "text", nullable: false),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    retry_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    available_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    correlation_id = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_events", x => x.event_id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                schema: "auth",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    phone_number = table.Column<string>(type: "text", nullable: false),
                    phone_number_normalized = table.Column<string>(type: "text", nullable: false),
                    phone_number_masked = table.Column<string>(type: "text", nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    password_algorithm = table.Column<string>(type: "text", nullable: false),
                    password_pepper_version = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    failed_login_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    locked_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_login_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.user_id);
                });

            migrationBuilder.CreateTable(
                name: "user_phone_histories",
                schema: "auth",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    old_phone_number_masked = table.Column<string>(type: "text", nullable: false),
                    old_phone_number_hash = table.Column<string>(type: "text", nullable: false),
                    new_phone_number_masked = table.Column<string>(type: "text", nullable: false),
                    new_phone_number_hash = table.Column<string>(type: "text", nullable: false),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    changed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason_code = table.Column<string>(type: "text", nullable: false),
                    correlation_id = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_phone_histories", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_phone_histories_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_auth_audit_logs_correlation_id",
                schema: "auth",
                table: "auth_audit_logs",
                column: "correlation_id");

            migrationBuilder.CreateIndex(
                name: "ix_login_attempts_created_at",
                schema: "auth",
                table: "login_attempts",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_correlation_id",
                schema: "auth",
                table: "outbox_events",
                column: "correlation_id");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_created_at",
                schema: "auth",
                table: "outbox_events",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_status_available_at",
                schema: "auth",
                table: "outbox_events",
                columns: new[] { "status", "available_at" });

            migrationBuilder.CreateIndex(
                name: "IX_user_phone_histories_user_id",
                schema: "auth",
                table: "user_phone_histories",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_last_login_at",
                schema: "auth",
                table: "users",
                column: "last_login_at");

            migrationBuilder.CreateIndex(
                name: "ix_users_status",
                schema: "auth",
                table: "users",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ux_users_phone_number_normalized",
                schema: "auth",
                table: "users",
                column: "phone_number_normalized",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "auth_audit_logs",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "login_attempts",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "outbox_events",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "user_phone_histories",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "users",
                schema: "auth");
        }
    }
}
