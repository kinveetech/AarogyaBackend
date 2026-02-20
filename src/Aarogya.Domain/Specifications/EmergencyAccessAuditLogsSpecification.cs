using Aarogya.Domain.Entities;

namespace Aarogya.Domain.Specifications;

public sealed class EmergencyAccessAuditLogsSpecification : BaseSpecification<AuditLog>
{
  public EmergencyAccessAuditLogsSpecification(DateTimeOffset? fromUtc, DateTimeOffset? toUtc)
    : base(log =>
      log.Action.StartsWith("emergency_access")
      && (!fromUtc.HasValue || log.OccurredAt >= fromUtc.Value)
      && (!toUtc.HasValue || log.OccurredAt <= toUtc.Value))
  {
    ApplyOrderByDescending(log => log.OccurredAt);
    ApplyAsNoTracking();
  }
}
