using Aarogya.Domain.Entities;

namespace Aarogya.Domain.Repositories;

public interface IDoctorProfileRepository : IRepository<DoctorProfile>
{
  public Task<DoctorProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
}
