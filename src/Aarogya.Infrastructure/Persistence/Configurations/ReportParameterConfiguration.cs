using Aarogya.Domain.Entities;
using Aarogya.Domain.ValueObjects;
using Aarogya.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aarogya.Infrastructure.Persistence.Configurations;

internal sealed class ReportParameterConfiguration : IEntityTypeConfiguration<ReportParameter>
{
  public void Configure(EntityTypeBuilder<ReportParameter> builder)
  {
    builder.ToTable("report_parameters");

    builder.HasKey(x => x.Id);

    builder.Property(x => x.Id).HasColumnName("id");
    builder.Property(x => x.ReportId).HasColumnName("report_id");
    builder.Property(x => x.ParameterCode).HasColumnName("parameter_code").IsRequired();
    builder.Property(x => x.ParameterName).HasColumnName("parameter_name").IsRequired();
    builder.Property(x => x.MeasuredValueText).HasColumnName("measured_value_text");
    builder.Property(x => x.MeasuredValueNumeric).HasColumnName("measured_value_numeric").HasPrecision(18, 6);
    builder.Property(x => x.Unit).HasColumnName("unit");
    builder.Property(x => x.ReferenceRangeText).HasColumnName("reference_range_text");
    builder.Property(x => x.IsAbnormal).HasColumnName("is_abnormal");
    builder.Property(x => x.CreatedAt).HasColumnName("created_at");

    builder.Property(x => x.RawParameter)
      .HasColumnName("raw_parameter")
      .HasColumnType("jsonb")
      .HasConversion(JsonbValueConverter.CreateConverter<ReportParameterRaw>(), JsonbValueConverter.CreateComparer<ReportParameterRaw>());

    builder.HasOne(x => x.Report)
      .WithMany(x => x.Parameters)
      .HasForeignKey(x => x.ReportId)
      .OnDelete(DeleteBehavior.Cascade);

    builder.HasIndex(x => x.ReportId).HasDatabaseName("ix_report_parameters_report_id");
    builder.HasIndex(x => x.ParameterName).HasDatabaseName("ix_report_parameters_name");

    builder.HasIndex(x => x.MeasuredValueNumeric)
      .HasDatabaseName("ix_report_parameters_value_numeric")
      .HasFilter("measured_value_numeric IS NOT NULL");

    builder.HasIndex(x => x.RawParameter)
      .HasMethod("gin")
      .HasOperators("jsonb_path_ops")
      .HasDatabaseName("ix_report_parameters_raw_parameter_gin");

    builder.HasIndex(x => new { x.ReportId, x.ParameterCode }).IsUnique();
  }
}
