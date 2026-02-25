using Aarogya.Domain.Entities;
using Aarogya.Infrastructure.Persistence.Converters;
using Aarogya.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aarogya.Infrastructure.Persistence.Configurations;

internal sealed class DoctorProfileConfiguration(IPiiFieldEncryptionService encryptionService)
  : IEntityTypeConfiguration<DoctorProfile>
{
  private const string ByteaType = "bytea";
  private readonly EncryptedRequiredStringToBytesConverter _encryptedRequiredStringConverter = new(encryptionService);
  private readonly EncryptedNullableStringToBytesConverter _encryptedNullableStringConverter = new(encryptionService);

  public void Configure(EntityTypeBuilder<DoctorProfile> builder)
  {
    builder.ToTable("doctor_profiles");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasColumnName("id");

    builder.Property(x => x.UserId).HasColumnName("user_id");
    builder.HasIndex(x => x.UserId).IsUnique().HasDatabaseName("ix_doctor_profiles_user_id");

    builder.Property(x => x.MedicalLicenseNumber)
      .HasColumnName("medical_license_number_encrypted")
      .HasColumnType(ByteaType)
      .HasConversion(_encryptedRequiredStringConverter)
      .IsRequired();

    builder.Property(x => x.Specialization)
      .HasColumnName("specialization")
      .HasMaxLength(100)
      .IsRequired();

    builder.Property(x => x.ClinicOrHospitalName)
      .HasColumnName("clinic_or_hospital_name_encrypted")
      .HasColumnType(ByteaType)
      .HasConversion(_encryptedNullableStringConverter);

    builder.Property(x => x.ClinicAddress)
      .HasColumnName("clinic_address_encrypted")
      .HasColumnType(ByteaType)
      .HasConversion(_encryptedNullableStringConverter);

    builder.Property(x => x.CreatedAt).HasColumnName("created_at");
    builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

    builder.HasOne(x => x.User)
      .WithOne(x => x.DoctorProfile)
      .HasForeignKey<DoctorProfile>(x => x.UserId)
      .OnDelete(DeleteBehavior.Cascade);
  }
}
