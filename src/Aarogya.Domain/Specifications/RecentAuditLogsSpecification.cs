using Aarogya.Domain.Entities;

namespace Aarogya.Domain.Specifications;

public sealed class RecentAuditLogsSpecification : BaseSpecification<AuditLog>
{
  public RecentAuditLogsSpecification(DateTimeOffset fromUtcInclusive, DateTimeOffset toUtcInclusive)
    : base(log =>
      log.OccurredAt >= fromUtcInclusive
      && log.OccurredAt <= toUtcInclusive
      && log.ActorUserId != null
      && log.ResultStatus != null
      && log.ResultStatus >= 200
      && log.ResultStatus < 300)
  {
    ApplyOrderByDescending(log => log.OccurredAt);
    ApplyAsNoTracking();
  }
}
