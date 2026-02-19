using Aarogya.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aarogya.Infrastructure.Persistence.Configurations;

internal sealed class AadhaarVaultAccessLogConfiguration : IEntityTypeConfiguration<AadhaarVaultAccessLog>
{
  public void Configure(EntityTypeBuilder<AadhaarVaultAccessLog> builder)
  {
    builder.ToTable("access_audit_logs", "aadhaar_vault");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasColumnName("id");

    builder.Property(x => x.ReferenceToken).HasColumnName("reference_token").IsRequired();
    builder.Property(x => x.OccurredAt).HasColumnName("occurred_at");
    builder.Property(x => x.ActorUserId).HasColumnName("actor_user_id");
    builder.Property(x => x.Action).HasColumnName("action").IsRequired();
    builder.Property(x => x.RequestPath).HasColumnName("request_path");
    builder.Property(x => x.RequestMethod).HasColumnName("request_method");
    builder.Property(x => x.ClientIp).HasColumnName("client_ip").HasColumnType("inet");
    builder.Property(x => x.ResultStatus).HasColumnName("result_status");
    builder.Property(x => x.Details).HasColumnName("details").HasColumnType("text");

    builder.HasIndex(x => x.ReferenceToken).HasDatabaseName("ix_aadhaar_access_logs_reference_token");
    builder.HasIndex(x => x.OccurredAt).HasDatabaseName("ix_aadhaar_access_logs_occurred_at");
    builder.HasIndex(x => new { x.ReferenceToken, x.OccurredAt }).HasDatabaseName("ix_aadhaar_access_logs_reference_token_time");
  }
}
