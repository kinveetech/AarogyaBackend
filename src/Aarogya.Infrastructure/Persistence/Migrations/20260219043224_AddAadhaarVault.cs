using System;
using System.Net;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aarogya.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAadhaarVault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "aadhaar_vault");

            migrationBuilder.CreateTable(
                name: "aadhaar_records",
                schema: "aadhaar_vault",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reference_token = table.Column<Guid>(type: "uuid", nullable: false),
                    aadhaar_encrypted = table.Column<byte[]>(type: "bytea", nullable: false),
                    aadhaar_sha256 = table.Column<byte[]>(type: "bytea", nullable: false),
                    provider_request_id = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_aadhaar_records", x => x.id);
                    table.UniqueConstraint("AK_aadhaar_records_reference_token", x => x.reference_token);
                });

            migrationBuilder.CreateTable(
                name: "access_audit_logs",
                schema: "aadhaar_vault",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reference_token = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action = table.Column<string>(type: "text", nullable: false),
                    request_path = table.Column<string>(type: "text", nullable: true),
                    request_method = table.Column<string>(type: "text", nullable: true),
                    client_ip = table.Column<IPAddress>(type: "inet", nullable: true),
                    result_status = table.Column<int>(type: "integer", nullable: true),
                    details = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_access_audit_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_access_audit_logs_aadhaar_records_reference_token",
                        column: x => x.reference_token,
                        principalSchema: "aadhaar_vault",
                        principalTable: "aadhaar_records",
                        principalColumn: "reference_token",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_users_aadhaar_ref_token",
                table: "users",
                column: "aadhaar_ref_token");

            migrationBuilder.CreateIndex(
                name: "ux_aadhaar_records_reference_token",
                schema: "aadhaar_vault",
                table: "aadhaar_records",
                column: "reference_token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_aadhaar_records_sha256",
                schema: "aadhaar_vault",
                table: "aadhaar_records",
                column: "aadhaar_sha256",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_aadhaar_access_logs_occurred_at",
                schema: "aadhaar_vault",
                table: "access_audit_logs",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "ix_aadhaar_access_logs_reference_token",
                schema: "aadhaar_vault",
                table: "access_audit_logs",
                column: "reference_token");

            migrationBuilder.CreateIndex(
                name: "ix_aadhaar_access_logs_reference_token_time",
                schema: "aadhaar_vault",
                table: "access_audit_logs",
                columns: new[] { "reference_token", "occurred_at" });

            migrationBuilder.AddForeignKey(
                name: "FK_users_aadhaar_records_aadhaar_ref_token",
                table: "users",
                column: "aadhaar_ref_token",
                principalSchema: "aadhaar_vault",
                principalTable: "aadhaar_records",
                principalColumn: "reference_token",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_users_aadhaar_records_aadhaar_ref_token",
                table: "users");

            migrationBuilder.DropTable(
                name: "access_audit_logs",
                schema: "aadhaar_vault");

            migrationBuilder.DropTable(
                name: "aadhaar_records",
                schema: "aadhaar_vault");

            migrationBuilder.DropIndex(
                name: "IX_users_aadhaar_ref_token",
                table: "users");
        }
    }
}
