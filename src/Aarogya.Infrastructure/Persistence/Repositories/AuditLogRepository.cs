using Aarogya.Domain.Entities;
using Aarogya.Domain.Repositories;
using Aarogya.Domain.Specifications;

namespace Aarogya.Infrastructure.Persistence.Repositories;

internal sealed class AuditLogRepository(AarogyaDbContext dbContext)
  : Repository<AuditLog>(dbContext), IAuditLogRepository
{
  public Task<IReadOnlyList<AuditLog>> ListByActorAsync(Guid actorUserId, CancellationToken cancellationToken = default)
    => ListAsync(new AuditLogsByActorSpecification(actorUserId), cancellationToken);

  public override void Update(AuditLog entity)
    => throw new NotSupportedException("Audit logs are append-only and cannot be updated.");

  public override void Delete(AuditLog entity)
    => throw new NotSupportedException("Audit logs are append-only and cannot be deleted.");
}
