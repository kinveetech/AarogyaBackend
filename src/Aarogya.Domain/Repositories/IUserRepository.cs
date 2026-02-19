using Aarogya.Domain.Entities;

namespace Aarogya.Domain.Repositories;

public interface IUserRepository : IRepository<User>
{
  public Task<User?> GetByExternalAuthIdAsync(string externalAuthId, CancellationToken cancellationToken = default);
}
