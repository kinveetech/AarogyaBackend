using Aarogya.Domain.Entities;
using Aarogya.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Aarogya.Infrastructure.Persistence.Repositories;

internal sealed class LabTechnicianProfileRepository(AarogyaDbContext dbContext)
  : Repository<LabTechnicianProfile>(dbContext), ILabTechnicianProfileRepository
{
  public Task<LabTechnicianProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    => dbContext.LabTechnicianProfiles
      .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
}
