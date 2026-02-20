using Aarogya.Domain.Entities;

namespace Aarogya.Domain.Repositories;

public interface IConsentRecordRepository : IRepository<ConsentRecord>
{
  public Task<IReadOnlyList<ConsentRecord>> ListLatestByUserAsync(Guid userId, CancellationToken cancellationToken = default);

  public Task<ConsentRecord?> GetLatestByUserAndPurposeAsync(
    Guid userId,
    string purpose,
    CancellationToken cancellationToken = default);

  public Task<bool> IsGrantedAsync(Guid userId, string purpose, CancellationToken cancellationToken = default);
}
