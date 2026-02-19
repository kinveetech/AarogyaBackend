using Aarogya.Domain.Entities;
using Aarogya.Domain.Repositories;
using Aarogya.Domain.Specifications;

namespace Aarogya.Infrastructure.Persistence.Repositories;

internal sealed class AccessGrantRepository(AarogyaDbContext dbContext)
  : Repository<AccessGrant>(dbContext), IAccessGrantRepository
{
  public Task<AccessGrant?> GetActiveGrantAsync(
    Guid patientId,
    Guid grantedToUserId,
    CancellationToken cancellationToken = default)
    => FirstOrDefaultAsync(
      new ActiveAccessGrantSpecification(patientId, grantedToUserId, DateTimeOffset.UtcNow),
      cancellationToken);
}
