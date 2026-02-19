using Aarogya.Domain.Entities;
using Aarogya.Infrastructure.Persistence.Converters;
using Aarogya.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aarogya.Infrastructure.Persistence.Configurations;

internal sealed class AadhaarVaultRecordConfiguration(IPiiFieldEncryptionService encryptionService)
  : IEntityTypeConfiguration<AadhaarVaultRecord>
{
  private readonly EncryptedRequiredStringToBytesConverter _encryptedStringConverter = new(encryptionService);

  public void Configure(EntityTypeBuilder<AadhaarVaultRecord> builder)
  {
    builder.ToTable("aadhaar_records", "aadhaar_vault");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasColumnName("id");

    builder.Property(x => x.ReferenceToken).HasColumnName("reference_token").IsRequired();
    builder.Property(x => x.AadhaarNumber)
      .HasColumnName("aadhaar_encrypted")
      .HasColumnType("bytea")
      .HasConversion(_encryptedStringConverter)
      .IsRequired();

    builder.Property(x => x.AadhaarSha256).HasColumnName("aadhaar_sha256").HasColumnType("bytea").IsRequired();
    builder.Property(x => x.ProviderRequestId).HasColumnName("provider_request_id");
    builder.Property(x => x.CreatedAt).HasColumnName("created_at");
    builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

    builder.HasIndex(x => x.ReferenceToken).IsUnique().HasDatabaseName("ux_aadhaar_records_reference_token");
    builder.HasIndex(x => x.AadhaarSha256).IsUnique().HasDatabaseName("ux_aadhaar_records_sha256");

    builder.HasMany(x => x.AccessLogs)
      .WithOne(x => x.AadhaarVaultRecord)
      .HasForeignKey(x => x.ReferenceToken)
      .HasPrincipalKey(x => x.ReferenceToken)
      .OnDelete(DeleteBehavior.Cascade);
  }
}
