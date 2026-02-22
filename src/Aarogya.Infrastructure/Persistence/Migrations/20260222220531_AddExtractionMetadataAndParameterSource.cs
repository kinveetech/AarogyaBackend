using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aarogya.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExtractionMetadataAndParameterSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "extraction",
                table: "reports",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "confidence",
                table: "report_parameters",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source",
                table: "report_parameters",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_reports_extraction_gin",
                table: "reports",
                column: "extraction",
                filter: "extraction IS NOT NULL")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "jsonb_path_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_report_parameters_source",
                table: "report_parameters",
                column: "source",
                filter: "source IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_reports_extraction_gin",
                table: "reports");

            migrationBuilder.DropIndex(
                name: "ix_report_parameters_source",
                table: "report_parameters");

            migrationBuilder.DropColumn(
                name: "extraction",
                table: "reports");

            migrationBuilder.DropColumn(
                name: "confidence",
                table: "report_parameters");

            migrationBuilder.DropColumn(
                name: "source",
                table: "report_parameters");
        }
    }
}
