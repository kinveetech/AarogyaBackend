using Aarogya.Domain.Entities;

namespace Aarogya.Domain.Repositories;

public interface IEmergencyContactRepository : IRepository<EmergencyContact>
{
  public Task<IReadOnlyList<EmergencyContact>> ListByUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
