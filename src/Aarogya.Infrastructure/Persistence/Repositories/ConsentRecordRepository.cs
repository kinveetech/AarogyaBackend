using Aarogya.Domain.Entities;
using Aarogya.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Aarogya.Infrastructure.Persistence.Repositories;

internal sealed class ConsentRecordRepository(AarogyaDbContext dbContext)
  : Repository<ConsentRecord>(dbContext), IConsentRecordRepository
{
  public async Task<IReadOnlyList<ConsentRecord>> ListLatestByUserAsync(Guid userId, CancellationToken cancellationToken = default)
  {
    var history = await dbContext.ConsentRecords
      .AsNoTracking()
      .Where(x => x.UserId == userId)
      .OrderByDescending(x => x.OccurredAt)
      .ThenByDescending(x => x.CreatedAt)
      .ToListAsync(cancellationToken);

    return history
      .GroupBy(x => x.Purpose, StringComparer.OrdinalIgnoreCase)
      .Select(group => group.First())
      .OrderBy(x => x.Purpose, StringComparer.OrdinalIgnoreCase)
      .ToArray();
  }

  public Task<ConsentRecord?> GetLatestByUserAndPurposeAsync(
    Guid userId,
    string purpose,
    CancellationToken cancellationToken = default)
  {
    var normalizedPurpose = purpose.Trim();
    return dbContext.ConsentRecords
      .AsNoTracking()
      .Where(x => x.UserId == userId)
      .Where(x => x.Purpose == normalizedPurpose)
      .OrderByDescending(x => x.OccurredAt)
      .ThenByDescending(x => x.CreatedAt)
      .FirstOrDefaultAsync(cancellationToken);
  }

  public async Task<bool> IsGrantedAsync(Guid userId, string purpose, CancellationToken cancellationToken = default)
  {
    var latest = await GetLatestByUserAndPurposeAsync(userId, purpose, cancellationToken);
    return latest?.IsGranted == true;
  }
}
