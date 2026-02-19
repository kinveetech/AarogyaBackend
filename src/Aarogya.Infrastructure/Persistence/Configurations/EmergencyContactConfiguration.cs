using Aarogya.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aarogya.Infrastructure.Persistence.Configurations;

internal sealed class EmergencyContactConfiguration : IEntityTypeConfiguration<EmergencyContact>
{
  public void Configure(EntityTypeBuilder<EmergencyContact> builder)
  {
    builder.ToTable("emergency_contacts");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasColumnName("id");

    builder.Property(x => x.UserId).HasColumnName("user_id");
    builder.Property(x => x.NameEncrypted).HasColumnName("name_encrypted").HasColumnType("bytea").IsRequired();
    builder.Property(x => x.Relationship).HasColumnName("relationship").IsRequired();
    builder.Property(x => x.PhoneEncrypted).HasColumnName("phone_encrypted").HasColumnType("bytea").IsRequired();
    builder.Property(x => x.PhoneHash).HasColumnName("phone_hash").HasColumnType("bytea");
    builder.Property(x => x.IsPrimary).HasColumnName("is_primary").HasDefaultValue(false);
    builder.Property(x => x.CreatedAt).HasColumnName("created_at");
    builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

    builder.HasOne(x => x.User)
      .WithMany(x => x.EmergencyContacts)
      .HasForeignKey(x => x.UserId)
      .OnDelete(DeleteBehavior.Cascade);

    builder.HasIndex(x => x.UserId).HasDatabaseName("ix_emergency_contacts_user_id");
    builder.HasIndex(x => x.PhoneHash).HasDatabaseName("ix_emergency_contacts_phone_hash");

    builder.HasIndex(x => x.UserId)
      .IsUnique()
      .HasFilter("is_primary")
      .HasDatabaseName("ux_emergency_contacts_primary_per_user");
  }
}
