using Aarogya.Domain.Entities;
using Aarogya.Infrastructure.Persistence.Converters;
using Aarogya.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aarogya.Infrastructure.Persistence.Configurations;

internal sealed class LabTechnicianProfileConfiguration(IPiiFieldEncryptionService encryptionService)
  : IEntityTypeConfiguration<LabTechnicianProfile>
{
  private const string ByteaType = "bytea";
  private readonly EncryptedRequiredStringToBytesConverter _encryptedRequiredStringConverter = new(encryptionService);
  private readonly EncryptedNullableStringToBytesConverter _encryptedNullableStringConverter = new(encryptionService);

  public void Configure(EntityTypeBuilder<LabTechnicianProfile> builder)
  {
    builder.ToTable("lab_technician_profiles");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasColumnName("id");

    builder.Property(x => x.UserId).HasColumnName("user_id");
    builder.HasIndex(x => x.UserId).IsUnique().HasDatabaseName("ix_lab_technician_profiles_user_id");

    builder.Property(x => x.LabName)
      .HasColumnName("lab_name_encrypted")
      .HasColumnType(ByteaType)
      .HasConversion(_encryptedRequiredStringConverter)
      .IsRequired();

    builder.Property(x => x.LabLicenseNumber)
      .HasColumnName("lab_license_number_encrypted")
      .HasColumnType(ByteaType)
      .HasConversion(_encryptedNullableStringConverter);

    builder.Property(x => x.NablAccreditationId)
      .HasColumnName("nabl_accreditation_id")
      .HasMaxLength(50);

    builder.Property(x => x.CreatedAt).HasColumnName("created_at");
    builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

    builder.HasOne(x => x.User)
      .WithOne(x => x.LabTechnicianProfile)
      .HasForeignKey<LabTechnicianProfile>(x => x.UserId)
      .OnDelete(DeleteBehavior.Cascade);
  }
}
