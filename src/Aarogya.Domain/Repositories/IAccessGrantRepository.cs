using Aarogya.Domain.Entities;

namespace Aarogya.Domain.Repositories;

public interface IAccessGrantRepository : IRepository<AccessGrant>
{
  public Task<AccessGrant?> GetActiveGrantAsync(
    Guid patientId,
    Guid grantedToUserId,
    CancellationToken cancellationToken = default);
}
