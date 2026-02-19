using Aarogya.Domain.Entities;

namespace Aarogya.Domain.Repositories;

public interface IAadhaarVaultRepository : IRepository<AadhaarVaultRecord>
{
  public Task<AadhaarVaultRecord?> GetByReferenceTokenAsync(Guid referenceToken, CancellationToken cancellationToken = default);

  public Task<AadhaarVaultRecord?> GetBySha256Async(byte[] aadhaarSha256, CancellationToken cancellationToken = default);
}
