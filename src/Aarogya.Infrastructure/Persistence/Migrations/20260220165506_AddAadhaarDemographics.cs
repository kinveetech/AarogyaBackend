using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aarogya.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAadhaarDemographics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "demographics_encrypted",
                schema: "aadhaar_vault",
                table: "aadhaar_records",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "verification_provider",
                schema: "aadhaar_vault",
                table: "aadhaar_records",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "demographics_encrypted",
                schema: "aadhaar_vault",
                table: "aadhaar_records");

            migrationBuilder.DropColumn(
                name: "verification_provider",
                schema: "aadhaar_vault",
                table: "aadhaar_records");
        }
    }
}
