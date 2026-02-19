using Aarogya.Domain.Entities;
using Aarogya.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Aarogya.Infrastructure.Persistence.Repositories;

internal sealed class AadhaarVaultRepository : Repository<AadhaarVaultRecord>, IAadhaarVaultRepository
{
  private readonly AarogyaDbContext _dbContext;

  public AadhaarVaultRepository(AarogyaDbContext dbContext)
    : base(dbContext)
  {
    _dbContext = dbContext;
  }

  public Task<AadhaarVaultRecord?> GetByReferenceTokenAsync(Guid referenceToken, CancellationToken cancellationToken = default)
    => _dbContext.AadhaarVaultRecords
      .AsNoTracking()
      .FirstOrDefaultAsync(x => x.ReferenceToken == referenceToken, cancellationToken);

  public Task<AadhaarVaultRecord?> GetBySha256Async(byte[] aadhaarSha256, CancellationToken cancellationToken = default)
    => _dbContext.AadhaarVaultRecords
      .AsNoTracking()
      .FirstOrDefaultAsync(x => x.AadhaarSha256.SequenceEqual(aadhaarSha256), cancellationToken);
}
