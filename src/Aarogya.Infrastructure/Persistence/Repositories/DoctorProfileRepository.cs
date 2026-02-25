using Aarogya.Domain.Entities;
using Aarogya.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Aarogya.Infrastructure.Persistence.Repositories;

internal sealed class DoctorProfileRepository(AarogyaDbContext dbContext)
  : Repository<DoctorProfile>(dbContext), IDoctorProfileRepository
{
  public Task<DoctorProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    => dbContext.DoctorProfiles
      .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
}
