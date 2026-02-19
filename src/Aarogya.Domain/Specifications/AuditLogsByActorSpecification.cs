using Aarogya.Domain.Entities;

namespace Aarogya.Domain.Specifications;

public sealed class AuditLogsByActorSpecification : BaseSpecification<AuditLog>
{
  public AuditLogsByActorSpecification(Guid actorUserId)
    : base(log => log.ActorUserId == actorUserId)
  {
    ApplyOrderByDescending(log => log.OccurredAt);
    ApplyAsNoTracking();
  }
}
