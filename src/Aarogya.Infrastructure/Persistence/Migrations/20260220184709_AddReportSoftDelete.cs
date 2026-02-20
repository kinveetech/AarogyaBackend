using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aarogya.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReportSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deleted_at",
                table: "reports",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "hard_deleted_at",
                table: "reports",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "reports",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_reports_deleted_at",
                table: "reports",
                columns: new[] { "is_deleted", "deleted_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_reports_deleted_at",
                table: "reports");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "reports");

            migrationBuilder.DropColumn(
                name: "hard_deleted_at",
                table: "reports");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "reports");
        }
    }
}
