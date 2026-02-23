using Aarogya.Domain.Entities;
using Aarogya.Domain.Repositories;

namespace Aarogya.Infrastructure.Persistence.Repositories;

internal sealed class ApiKeyRepository(AarogyaDbContext dbContext)
  : Repository<ApiKey>(dbContext), IApiKeyRepository
{
}
