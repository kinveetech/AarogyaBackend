using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aarogya.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserProfileFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "address_encrypted",
                table: "users",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "blood_group_encrypted",
                table: "users",
                type: "bytea",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "address_encrypted",
                table: "users");

            migrationBuilder.DropColumn(
                name: "blood_group_encrypted",
                table: "users");
        }
    }
}
