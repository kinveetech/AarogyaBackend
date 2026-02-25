using Aarogya.Domain.Entities;

namespace Aarogya.Domain.Repositories;

public interface ILabTechnicianProfileRepository : IRepository<LabTechnicianProfile>
{
  public Task<LabTechnicianProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
}
