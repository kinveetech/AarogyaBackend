using Aarogya.Domain.Entities;
using Aarogya.Domain.Enums;
using Aarogya.Infrastructure.Persistence.Converters;
using Aarogya.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aarogya.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration(IPiiFieldEncryptionService encryptionService) : IEntityTypeConfiguration<User>
{
  private const string ByteaType = "bytea";
  private readonly EncryptedRequiredStringToBytesConverter _encryptedRequiredStringConverter = new(encryptionService);
  private readonly EncryptedNullableStringToBytesConverter _encryptedNullableStringConverter = new(encryptionService);

  public void Configure(EntityTypeBuilder<User> builder)
  {
    builder.ToTable("users", t =>
      t.HasCheckConstraint("users_gender_chk", "gender IS NULL OR gender IN ('male', 'female', 'other', 'unknown')"));

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasColumnName("id");

    builder.Property(x => x.ExternalAuthId).HasColumnName("external_auth_id");
    builder.HasIndex(x => x.ExternalAuthId).IsUnique();

    builder.Property(x => x.Role)
      .HasColumnName("role")
      .HasConversion(DescriptionEnumConverter.Create<UserRole>())
      .HasMaxLength(20);

    builder.Property(x => x.FirstName).HasColumnName("first_name_encrypted").HasColumnType(ByteaType).HasConversion(_encryptedRequiredStringConverter).IsRequired();
    builder.Property(x => x.LastName).HasColumnName("last_name_encrypted").HasColumnType(ByteaType).HasConversion(_encryptedRequiredStringConverter).IsRequired();
    builder.Property(x => x.Email).HasColumnName("email_encrypted").HasColumnType(ByteaType).HasConversion(_encryptedRequiredStringConverter).IsRequired();
    builder.Property(x => x.Phone).HasColumnName("phone_encrypted").HasColumnType(ByteaType).HasConversion(_encryptedNullableStringConverter);
    builder.Property(x => x.Address).HasColumnName("address_encrypted").HasColumnType(ByteaType).HasConversion(_encryptedNullableStringConverter);
    builder.Property(x => x.BloodGroup).HasColumnName("blood_group_encrypted").HasColumnType(ByteaType).HasConversion(_encryptedNullableStringConverter);

    builder.Property(x => x.DateOfBirth).HasColumnName("date_of_birth");
    builder.Property(x => x.Gender).HasColumnName("gender");

    builder.Property(x => x.EmailHash).HasColumnName("email_hash").HasColumnType(ByteaType);
    builder.Property(x => x.PhoneHash).HasColumnName("phone_hash").HasColumnType(ByteaType);
    builder.Property(x => x.AadhaarRefToken).HasColumnName("aadhaar_ref_token");
    builder.Property(x => x.AadhaarSha256).HasColumnName("aadhaar_sha256").HasColumnType(ByteaType);

    builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
    builder.Property(x => x.CreatedAt).HasColumnName("created_at");
    builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

    builder.HasIndex(x => x.EmailHash).HasDatabaseName("ix_users_email_hash");
    builder.HasIndex(x => x.PhoneHash).HasDatabaseName("ix_users_phone_hash");
    builder.HasIndex(x => x.AadhaarSha256).HasDatabaseName("ix_users_aadhaar_sha256");
    builder.HasIndex(x => new { x.Role, x.IsActive }).HasDatabaseName("ix_users_role_active");

    builder.HasOne<AadhaarVaultRecord>()
      .WithMany()
      .HasForeignKey(x => x.AadhaarRefToken)
      .HasPrincipalKey(x => x.ReferenceToken)
      .OnDelete(DeleteBehavior.SetNull);

  }
}
