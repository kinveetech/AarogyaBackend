using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aarogya.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmergencyContactEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "email_encrypted",
                table: "emergency_contacts",
                type: "bytea",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "email_encrypted",
                table: "emergency_contacts");
        }
    }
}
