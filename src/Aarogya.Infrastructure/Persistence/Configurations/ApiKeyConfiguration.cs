using Aarogya.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aarogya.Infrastructure.Persistence.Configurations;

internal sealed class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
  public void Configure(EntityTypeBuilder<ApiKey> builder)
  {
    builder.ToTable("api_keys");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasColumnName("id");

    builder.Property(x => x.KeyHash)
      .HasColumnName("key_hash")
      .HasMaxLength(64)
      .IsRequired();

    builder.Property(x => x.KeyPrefix)
      .HasColumnName("key_prefix")
      .HasMaxLength(64)
      .IsRequired();

    builder.Property(x => x.PartnerId)
      .HasColumnName("partner_id")
      .HasMaxLength(128)
      .IsRequired();

    builder.Property(x => x.PartnerName)
      .HasColumnName("partner_name")
      .HasMaxLength(256)
      .IsRequired();

    builder.Property(x => x.ExpiresAt).HasColumnName("expires_at");
    builder.Property(x => x.IsRevoked).HasColumnName("is_revoked").HasDefaultValue(false);
    builder.Property(x => x.RevokedAt).HasColumnName("revoked_at");
    builder.Property(x => x.OverlapExpiresAt).HasColumnName("overlap_expires_at");
    builder.Property(x => x.CreatedAt).HasColumnName("created_at");
    builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

    builder.HasIndex(x => x.KeyHash)
      .IsUnique()
      .HasDatabaseName("ix_api_keys_key_hash");

    builder.HasIndex(x => x.PartnerId)
      .HasDatabaseName("ix_api_keys_partner_id");
  }
}
