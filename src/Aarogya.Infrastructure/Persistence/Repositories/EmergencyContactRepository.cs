using Aarogya.Domain.Entities;
using Aarogya.Domain.Repositories;
using Aarogya.Domain.Specifications;

namespace Aarogya.Infrastructure.Persistence.Repositories;

internal sealed class EmergencyContactRepository(AarogyaDbContext dbContext)
  : Repository<EmergencyContact>(dbContext), IEmergencyContactRepository
{
  public Task<IReadOnlyList<EmergencyContact>> ListByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    => ListAsync(new EmergencyContactsByUserSpecification(userId), cancellationToken);
}
