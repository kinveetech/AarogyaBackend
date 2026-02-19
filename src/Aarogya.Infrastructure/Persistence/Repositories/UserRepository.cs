using Aarogya.Domain.Entities;
using Aarogya.Domain.Repositories;
using Aarogya.Domain.Specifications;

namespace Aarogya.Infrastructure.Persistence.Repositories;

internal sealed class UserRepository(AarogyaDbContext dbContext)
  : Repository<User>(dbContext), IUserRepository
{
  public Task<User?> GetByExternalAuthIdAsync(string externalAuthId, CancellationToken cancellationToken = default)
    => FirstOrDefaultAsync(new UserByExternalAuthIdSpecification(externalAuthId), cancellationToken);
}
