using Aarogya.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aarogya.Infrastructure.Persistence.Configurations;

internal sealed class ConsentRecordConfiguration : IEntityTypeConfiguration<ConsentRecord>
{
  public void Configure(EntityTypeBuilder<ConsentRecord> builder)
  {
    builder.ToTable("consent_records", t =>
      t.HasCheckConstraint("consent_records_purpose_chk", "char_length(trim(purpose)) > 0"));

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasColumnName("id");
    builder.Property(x => x.UserId).HasColumnName("user_id");
    builder.Property(x => x.Purpose).HasColumnName("purpose").HasMaxLength(120).IsRequired();
    builder.Property(x => x.IsGranted).HasColumnName("is_granted").IsRequired();
    builder.Property(x => x.Source).HasColumnName("source").HasMaxLength(80).IsRequired();
    builder.Property(x => x.OccurredAt).HasColumnName("occurred_at");
    builder.Property(x => x.CreatedAt).HasColumnName("created_at");
    builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

    builder.HasOne(x => x.User)
      .WithMany(x => x.ConsentRecords)
      .HasForeignKey(x => x.UserId)
      .OnDelete(DeleteBehavior.Cascade);

    builder.HasIndex(x => new { x.UserId, x.Purpose, x.OccurredAt })
      .HasDatabaseName("ix_consent_records_user_purpose_time");
    builder.HasIndex(x => new { x.UserId, x.OccurredAt })
      .HasDatabaseName("ix_consent_records_user_time");
  }
}
