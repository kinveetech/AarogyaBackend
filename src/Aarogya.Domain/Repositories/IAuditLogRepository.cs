using Aarogya.Domain.Entities;

namespace Aarogya.Domain.Repositories;

public interface IAuditLogRepository : IRepository<AuditLog>
{
  public Task<IReadOnlyList<AuditLog>> ListByActorAsync(Guid actorUserId, CancellationToken cancellationToken = default);
}
