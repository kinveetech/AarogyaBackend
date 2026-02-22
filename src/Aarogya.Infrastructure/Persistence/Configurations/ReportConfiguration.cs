using Aarogya.Domain.Entities;
using Aarogya.Domain.Enums;
using Aarogya.Domain.ValueObjects;
using Aarogya.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aarogya.Infrastructure.Persistence.Configurations;

internal sealed class ReportConfiguration : IEntityTypeConfiguration<Report>
{
  public void Configure(EntityTypeBuilder<Report> builder)
  {
    builder.ToTable("reports", t =>
      t.HasCheckConstraint("reports_time_order_chk", "reported_at IS NULL OR collected_at IS NULL OR reported_at >= collected_at"));

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasColumnName("id");

    builder.Property(x => x.ReportNumber).HasColumnName("report_number").IsRequired();
    builder.HasIndex(x => x.ReportNumber).IsUnique();

    builder.Property(x => x.PatientId).HasColumnName("patient_id");
    builder.Property(x => x.DoctorId).HasColumnName("doctor_id");
    builder.Property(x => x.UploadedByUserId).HasColumnName("uploaded_by_user_id");

    builder.Property(x => x.ReportType)
      .HasColumnName("report_type")
      .HasConversion(DescriptionEnumConverter.Create<ReportType>())
      .HasMaxLength(20);

    builder.Property(x => x.Status)
      .HasColumnName("status")
      .HasConversion(DescriptionEnumConverter.Create<ReportStatus>())
      .HasMaxLength(20)
      .HasDefaultValue(ReportStatus.Uploaded);

    builder.Property(x => x.SourceSystem).HasColumnName("source_system");
    builder.Property(x => x.CollectedAt).HasColumnName("collected_at");
    builder.Property(x => x.ReportedAt).HasColumnName("reported_at");
    builder.Property(x => x.UploadedAt).HasColumnName("uploaded_at");

    builder.Property(x => x.FileStorageKey).HasColumnName("file_storage_key");
    builder.Property(x => x.ChecksumSha256).HasColumnName("checksum_sha256");
    builder.Property(x => x.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
    builder.Property(x => x.DeletedAt).HasColumnName("deleted_at");
    builder.Property(x => x.HardDeletedAt).HasColumnName("hard_deleted_at");

    builder.Property(x => x.Results)
      .HasColumnName("results")
      .HasColumnType("jsonb")
      .HasConversion(JsonbValueConverter.CreateConverter<ReportResults>(), JsonbValueConverter.CreateComparer<ReportResults>());

    builder.Property(x => x.Metadata)
      .HasColumnName("metadata")
      .HasColumnType("jsonb")
      .HasConversion(JsonbValueConverter.CreateConverter<ReportMetadata>(), JsonbValueConverter.CreateComparer<ReportMetadata>());

    builder.Property(x => x.Extraction)
      .HasColumnName("extraction")
      .HasColumnType("jsonb")
      .HasConversion(JsonbValueConverter.CreateNullableConverter<ExtractionMetadata>(), JsonbValueConverter.CreateComparer<ExtractionMetadata>())
      .IsRequired(false);

    builder.Property(x => x.CreatedAt).HasColumnName("created_at");
    builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

    builder.HasOne(x => x.Patient)
      .WithMany(x => x.PatientReports)
      .HasForeignKey(x => x.PatientId)
      .OnDelete(DeleteBehavior.Restrict);

    builder.HasOne(x => x.Doctor)
      .WithMany(x => x.DoctorReports)
      .HasForeignKey(x => x.DoctorId)
      .OnDelete(DeleteBehavior.SetNull);

    builder.HasOne(x => x.UploadedByUser)
      .WithMany(x => x.UploadedReports)
      .HasForeignKey(x => x.UploadedByUserId)
      .OnDelete(DeleteBehavior.Restrict);

    builder.HasMany(x => x.Parameters)
      .WithOne(x => x.Report)
      .HasForeignKey(x => x.ReportId)
      .OnDelete(DeleteBehavior.Cascade);

    builder.HasIndex(x => new { x.PatientId, x.UploadedAt }).HasDatabaseName("ix_reports_patient_uploaded_at");
    builder.HasIndex(x => new { x.Status, x.UploadedAt }).HasDatabaseName("ix_reports_status_uploaded_at");
    builder.HasIndex(x => new { x.IsDeleted, x.DeletedAt }).HasDatabaseName("ix_reports_deleted_at");
    builder.HasIndex(x => new { x.ReportType, x.ReportedAt }).HasDatabaseName("ix_reports_type_reported_at");

    builder.HasIndex(x => x.Results)
      .HasMethod("gin")
      .HasOperators("jsonb_path_ops")
      .HasDatabaseName("ix_reports_results_gin");

    builder.HasIndex(x => x.Metadata)
      .HasMethod("gin")
      .HasOperators("jsonb_path_ops")
      .HasDatabaseName("ix_reports_metadata_gin");

    builder.HasIndex(x => x.Extraction)
      .HasMethod("gin")
      .HasOperators("jsonb_path_ops")
      .HasDatabaseName("ix_reports_extraction_gin")
      .HasFilter("extraction IS NOT NULL");

  }
}
