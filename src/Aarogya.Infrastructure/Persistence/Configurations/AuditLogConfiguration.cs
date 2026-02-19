using Aarogya.Domain.Entities;
using Aarogya.Domain.Enums;
using Aarogya.Domain.ValueObjects;
using Aarogya.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aarogya.Infrastructure.Persistence.Configurations;

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
  public void Configure(EntityTypeBuilder<AuditLog> builder)
  {
    builder.ToTable("audit_logs", t =>
      t.HasCheckConstraint("audit_logs_result_status_chk", "result_status IS NULL OR (result_status BETWEEN 100 AND 599)"));

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasColumnName("id");

    builder.Property(x => x.OccurredAt).HasColumnName("occurred_at");

    builder.Property(x => x.ActorUserId).HasColumnName("actor_user_id");

    builder.Property(x => x.ActorRole)
      .HasColumnName("actor_role")
      .HasColumnType("user_role")
      .HasConversion(
        v => v.HasValue ? EnumSnakeCaseConverter.Create<UserRole>().ConvertToProviderExpression.Compile()(v.Value) : null,
        v => string.IsNullOrWhiteSpace(v) ? null : EnumSnakeCaseConverter.Create<UserRole>().ConvertFromProviderExpression.Compile()(v)!);

    builder.Property(x => x.Action).HasColumnName("action").IsRequired();
    builder.Property(x => x.EntityType).HasColumnName("entity_type").IsRequired();
    builder.Property(x => x.EntityId).HasColumnName("entity_id");
    builder.Property(x => x.CorrelationId).HasColumnName("correlation_id");
    builder.Property(x => x.RequestPath).HasColumnName("request_path");
    builder.Property(x => x.RequestMethod).HasColumnName("request_method");
    builder.Property(x => x.ClientIp).HasColumnName("client_ip").HasColumnType("inet");
    builder.Property(x => x.UserAgent).HasColumnName("user_agent");
    builder.Property(x => x.ResultStatus).HasColumnName("result_status");

    builder.Property(x => x.Details)
      .HasColumnName("details")
      .HasColumnType("jsonb")
      .HasConversion(JsonbValueConverter.CreateConverter<AuditLogDetails>(), JsonbValueConverter.CreateComparer<AuditLogDetails>());

    builder.HasOne(x => x.ActorUser)
      .WithMany(x => x.AuditLogs)
      .HasForeignKey(x => x.ActorUserId)
      .OnDelete(DeleteBehavior.SetNull);

    builder.HasIndex(x => x.OccurredAt).HasDatabaseName("ix_audit_logs_occurred_at");
    builder.HasIndex(x => new { x.ActorUserId, x.OccurredAt }).HasDatabaseName("ix_audit_logs_actor_time");
    builder.HasIndex(x => new { x.EntityType, x.EntityId, x.OccurredAt }).HasDatabaseName("ix_audit_logs_entity_time");

    builder.HasIndex(x => x.Details)
      .HasMethod("gin")
      .HasOperators("jsonb_path_ops")
      .HasDatabaseName("ix_audit_logs_details_gin");

  }
}
