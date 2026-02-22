using Aarogya.Domain.Entities;
using Aarogya.Domain.Enums;
using Aarogya.Domain.ValueObjects;
using Aarogya.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aarogya.Infrastructure.Persistence.Configurations;

internal sealed class AccessGrantConfiguration : IEntityTypeConfiguration<AccessGrant>
{
  public void Configure(EntityTypeBuilder<AccessGrant> builder)
  {
    builder.ToTable("access_grants", t =>
    {
      t.HasCheckConstraint("access_grants_time_window_chk", "expires_at IS NULL OR expires_at > starts_at");
      t.HasCheckConstraint("access_grants_revoked_time_chk", "revoked_at IS NULL OR revoked_at >= starts_at");
      t.HasCheckConstraint("access_grants_not_self_chk", "patient_id <> granted_to_user_id");
    });

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasColumnName("id");

    builder.Property(x => x.PatientId).HasColumnName("patient_id");
    builder.Property(x => x.GrantedToUserId).HasColumnName("granted_to_user_id");
    builder.Property(x => x.GrantedByUserId).HasColumnName("granted_by_user_id");

    builder.Property(x => x.GrantReason).HasColumnName("grant_reason");

    builder.Property(x => x.Scope)
      .HasColumnName("scope")
      .HasColumnType("jsonb")
      .HasConversion(JsonbValueConverter.CreateConverter<AccessGrantScope>(), JsonbValueConverter.CreateComparer<AccessGrantScope>());

    builder.Property(x => x.Status)
      .HasColumnName("status")
      .HasConversion(DescriptionEnumConverter.Create<AccessGrantStatus>())
      .HasMaxLength(20)
      .HasDefaultValue(AccessGrantStatus.Active);

    builder.Property(x => x.StartsAt).HasColumnName("starts_at");
    builder.Property(x => x.ExpiresAt).HasColumnName("expires_at");
    builder.Property(x => x.RevokedAt).HasColumnName("revoked_at");
    builder.Property(x => x.CreatedAt).HasColumnName("created_at");

    builder.HasOne(x => x.Patient)
      .WithMany(x => x.PatientAccessGrants)
      .HasForeignKey(x => x.PatientId)
      .OnDelete(DeleteBehavior.Cascade);

    builder.HasOne(x => x.GrantedToUser)
      .WithMany(x => x.GrantedAccessGrants)
      .HasForeignKey(x => x.GrantedToUserId)
      .OnDelete(DeleteBehavior.Cascade);

    builder.HasOne(x => x.GrantedByUser)
      .WithMany(x => x.CreatedAccessGrants)
      .HasForeignKey(x => x.GrantedByUserId)
      .OnDelete(DeleteBehavior.Restrict);

    builder.HasIndex(x => new { x.PatientId, x.Status }).HasDatabaseName("ix_access_grants_patient_status");
    builder.HasIndex(x => new { x.GrantedToUserId, x.Status }).HasDatabaseName("ix_access_grants_granted_to_status");

    builder.HasIndex(x => x.Scope)
      .HasMethod("gin")
      .HasOperators("jsonb_path_ops")
      .HasDatabaseName("ix_access_grants_scope_gin");

    builder.HasIndex(x => new { x.PatientId, x.GrantedToUserId })
      .IsUnique()
      .HasFilter("status = 'active'")
      .HasDatabaseName("ux_access_grants_active");

  }
}
